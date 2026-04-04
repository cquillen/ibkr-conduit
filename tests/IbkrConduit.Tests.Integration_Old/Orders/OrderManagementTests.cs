using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Accounts;
using IbkrConduit.Alerts;
using IbkrConduit.Allocation;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Contracts;
using IbkrConduit.Flex;
using IbkrConduit.Fyi;
using IbkrConduit.Http;
using IbkrConduit.MarketData;
using IbkrConduit.Orders;
using IbkrConduit.Portfolio;
using IbkrConduit.Session;
using IbkrConduit.Streaming;
using IbkrConduit.Watchlists;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OneOf;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.Orders;

[Collection("IBKR E2E")]
public class OrderManagementTests : IDisposable
{
    private readonly WireMockServer _server;

    public OrderManagementTests()
    {
        _server = WireMockServer.Start();
    }

    [Fact]
    public async Task PlaceOrderAsync_DirectConfirmation_ReturnsOrderSubmitted()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/DU1234567/orders")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        [
                            {
                                "order_id": "12345",
                                "order_status": "PreSubmitted"
                            }
                        ]
                        """));

        var api = CreateRefitClient<IIbkrOrderApi>();
        var ops = new OrderOperations(api, NullLogger<OrderOperations>.Instance);

        var order = new OrderRequest
        {
            Conid = 265598,
            Side = "BUY",
            Quantity = 100,
            OrderType = "MKT",
            Tif = "DAY",
        };

        var result = await ops.PlaceOrderAsync("DU1234567", order, TestContext.Current.CancellationToken);

        result.AsT0.OrderId.ShouldBe("12345");
        result.AsT0.OrderStatus.ShouldBe("PreSubmitted");
    }

    [Fact]
    public async Task PlaceOrderAsync_WithQuestionResponse_ReturnsOrderConfirmationRequired()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/DU1234567/orders")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        [
                            {
                                "id": "reply-abc",
                                "message": ["Are you sure you want to submit this order?"],
                                "isSuppressed": false,
                                "messageIds": ["o123"]
                            }
                        ]
                        """));

        var api = CreateRefitClient<IIbkrOrderApi>();
        var ops = new OrderOperations(api, NullLogger<OrderOperations>.Instance);

        var order = new OrderRequest
        {
            Conid = 265598,
            Side = "BUY",
            Quantity = 50,
            OrderType = "LMT",
            Price = 150.00m,
            Tif = "GTC",
        };

        var result = await ops.PlaceOrderAsync("DU1234567", order, TestContext.Current.CancellationToken);

        var confirmation = result.AsT1;
        confirmation.ReplyId.ShouldBe("reply-abc");
        confirmation.Messages.ShouldContain("Are you sure you want to submit this order?");
        confirmation.MessageIds.ShouldContain("o123");
    }

    [Fact]
    public async Task ReplyAsync_ConfirmsAndReturnsOrderSubmitted()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/reply/reply-xyz")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "order_id": "99999",
                            "order_status": "Submitted"
                        }
                        """));

        var api = CreateRefitClient<IIbkrOrderApi>();
        var ops = new OrderOperations(api, NullLogger<OrderOperations>.Instance);

        var result = await ops.ReplyAsync("reply-xyz", true, TestContext.Current.CancellationToken);

        result.AsT0.OrderId.ShouldBe("99999");
        result.AsT0.OrderStatus.ShouldBe("Submitted");
    }

    [Fact]
    public async Task CancelOrderAsync_ReturnsResult()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/DU1234567/order/12345")
                .UsingDelete())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "msg": "Order 12345 cancelled",
                            "order_id": "12345",
                            "conid": 265598
                        }
                        """));

        var api = CreateRefitClient<IIbkrOrderApi>();
        var ops = new OrderOperations(api, NullLogger<OrderOperations>.Instance);

        var result = await ops.CancelOrderAsync("DU1234567", "12345", TestContext.Current.CancellationToken);

        result.OrderId.ShouldBe(12345);
        result.Conid.ShouldBe(265598);
        result.Message.ShouldContain("cancelled");
    }

    [Fact]
    public async Task SearchBySymbolAsync_ReturnsContractList()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/secdef/search")
                .WithParam("symbol", "AAPL")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        [
                            {
                                "conid": 265598,
                                "companyHeader": "Apple Inc - NASDAQ",
                                "companyName": "Apple Inc",
                                "description": "AAPL",
                                "symbol": "AAPL",
                                "conidEx": "265598@SMART",
                                "secType": "STK",
                                "listingExchange": "NASDAQ",
                                "sections": [
                                    {
                                        "secType": "STK",
                                        "exchange": "SMART"
                                    }
                                ]
                            }
                        ]
                        """));

        var api = CreateRefitClient<IIbkrContractApi>();
        var ops = new ContractOperations(api);

        var result = await ops.SearchBySymbolAsync("AAPL", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Conid.ShouldBe(265598);
        result[0].Symbol.ShouldBe("AAPL");
        result[0].CompanyName.ShouldBe("Apple Inc");
    }

    [Fact]
    public async Task Facade_DelegatesToUnderlyingApis()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        [
                            {
                                "id": "DU1234567",
                                "accountTitle": "Paper Trading",
                                "type": "INDIVIDUAL"
                            }
                        ]
                        """));

        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/secdef/search")
                .WithParam("symbol", "MSFT")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        [
                            {
                                "conid": 272093,
                                "companyHeader": "Microsoft Corp - NASDAQ",
                                "companyName": "Microsoft Corp",
                                "description": "MSFT",
                                "symbol": "MSFT",
                                "conidEx": "272093@SMART",
                                "secType": "STK",
                                "listingExchange": "NASDAQ"
                            }
                        ]
                        """));

        var portfolioApi = CreateRefitClient<IIbkrPortfolioApi>();
        var contractApi = CreateRefitClient<IIbkrContractApi>();
        var orderApi = CreateRefitClient<IIbkrOrderApi>();

        var portfolio = new PortfolioOperations(portfolioApi);
        var contracts = new ContractOperations(contractApi);
        var orders = new OrderOperations(orderApi, NullLogger<OrderOperations>.Instance);
        var sessionManager = new FakeSessionManager();

        var client = new IbkrClient(portfolio, contracts, orders, new FakeMarketDataOperations(), new FakeStreamingOperations(), new FakeFlexOperations(), new FakeAccountOperations(), new FakeAlertOperations(), new FakeWatchlistOperations(), new FakeFyiOperations(), new FakeAllocationOperations(), sessionManager);

        var accounts = await client.Portfolio.GetAccountsAsync(TestContext.Current.CancellationToken);
        accounts.Count.ShouldBe(1);
        accounts[0].Id.ShouldBe("DU1234567");

        var searchResults = await client.Contracts.SearchBySymbolAsync("MSFT", TestContext.Current.CancellationToken);
        searchResults.Count.ShouldBe(1);
        searchResults[0].Symbol.ShouldBe("MSFT");
    }

    /// <summary>
    /// End-to-end test against a real IBKR paper account.
    /// Runs only when IBKR_CONSUMER_KEY environment variable is set.
    /// Tests portfolio + contract search only — does NOT place real orders.
    /// </summary>
    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task EndToEnd_PortfolioAndContractSearch_Succeeds()
    {
        using var creds = OAuthCredentialsFactory.FromEnvironment();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIbkrClient(opts => opts.Credentials = creds);

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IIbkrClient>();

        var accounts = await client.Portfolio.GetAccountsAsync(TestContext.Current.CancellationToken);
        accounts.ShouldNotBeNull();
        accounts.ShouldNotBeEmpty();
        accounts[0].Id.ShouldNotBeNullOrWhiteSpace();

        var searchResults = await client.Contracts.SearchBySymbolAsync("AAPL", TestContext.Current.CancellationToken);
        searchResults.ShouldNotBeNull();
        searchResults.ShouldNotBeEmpty();
        searchResults[0].Conid.ShouldBeGreaterThan(0);
        searchResults[0].Symbol.ShouldBe("AAPL");
    }

    /// <summary>
    /// End-to-end test that places a real market order for 1 share of SPY on the paper account.
    /// Runs only when IBKR_CONSUMER_KEY environment variable is set.
    /// </summary>
    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task EndToEnd_BuyOneSpy_MarketOrder_Succeeds()
    {
        using var creds = OAuthCredentialsFactory.FromEnvironment();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIbkrClient(opts => opts.Credentials = creds);

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IIbkrClient>();

        // Get account ID
        var accounts = await client.Portfolio.GetAccountsAsync(TestContext.Current.CancellationToken);
        accounts.ShouldNotBeNull();
        accounts.ShouldNotBeEmpty();
        var accountId = accounts[0].Id;

        // Look up SPY conid
        var spyResults = await client.Contracts.SearchBySymbolAsync("SPY", TestContext.Current.CancellationToken);
        spyResults.ShouldNotBeNull();
        spyResults.ShouldNotBeEmpty();
        var spyConid = spyResults[0].Conid;
        spyConid.ShouldBeGreaterThan(0);

        // Place market order: buy 1 share of SPY
        var order = new OrderRequest
        {
            Conid = spyConid,
            Side = "BUY",
            Quantity = 1,
            OrderType = "MKT",
            Tif = "DAY",
        };

        var result = await client.Orders.PlaceOrderAsync(accountId, order, TestContext.Current.CancellationToken);

        // The result may be either a submitted order or a confirmation request
        result.Switch(
            submitted => submitted.OrderId.ShouldNotBeNullOrWhiteSpace(),
            confirmation => confirmation.ReplyId.ShouldNotBeNullOrWhiteSpace());
    }

    public void Dispose()
    {
        _server.Dispose();
    }

    private TApi CreateRefitClient<TApi>() where TApi : class
    {
        var lstBytes = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
                                     0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
                                     0x11, 0x12, 0x13, 0x14 };
        var tokenProvider = new FakeTokenProvider(
            new LiveSessionToken(lstBytes, DateTimeOffset.UtcNow.AddHours(24)));

        var signingHandler = new OAuthSigningHandler(tokenProvider, "TESTKEY01", "mytoken")
        {
            InnerHandler = new HttpClientHandler(),
        };

        var httpClient = new HttpClient(signingHandler)
        {
            BaseAddress = new Uri(_server.Url!),
        };

        return Refit.RestService.For<TApi>(httpClient);
    }

    private class FakeTokenProvider : ISessionTokenProvider
    {
        private readonly LiveSessionToken _token;

        public FakeTokenProvider(LiveSessionToken token) => _token = token;

        public Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_token);

        public Task<LiveSessionToken> RefreshAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_token);
    }

    private class FakeMarketDataOperations : IMarketDataOperations
    {
        public Task<List<MarketDataSnapshot>> GetSnapshotAsync(int[] conids, string[] fields,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<MarketDataSnapshot>());

        public Task<HistoricalDataResponse> GetHistoryAsync(int conid, string period, string bar,
            bool? outsideRth = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<MarketDataSnapshot> GetRegulatorySnapshotAsync(int conid,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<UnsubscribeResponse> UnsubscribeAsync(int conid,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<UnsubscribeAllResponse> UnsubscribeAllAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ScannerResponse> RunScannerAsync(ScannerRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ScannerParameters> GetScannerParametersAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<HmdsScannerResponse> RunHmdsScannerAsync(HmdsScannerRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private class FakeStreamingOperations : IStreamingOperations
    {
        public IObservable<MarketDataTick> MarketData(int conid, string[] fields, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IObservable<OrderUpdate> OrderUpdates(int? days = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IObservable<PnlUpdate> ProfitAndLoss(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IObservable<AccountSummaryUpdate> AccountSummary(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IObservable<AccountLedgerUpdate> AccountLedger(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private class FakeFlexOperations : IFlexOperations
    {
        public Task<FlexQueryResult> ExecuteQueryAsync(
            string queryId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<FlexQueryResult> ExecuteQueryAsync(
            string queryId, string fromDate, string toDate,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private class FakeAccountOperations : IAccountOperations
    {
        public Task<IserverAccountsResponse> GetAccountsAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<SwitchAccountResponse> SwitchAccountAsync(string accountId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

    }

    private class FakeAlertOperations : IAlertOperations
    {
        public Task<CreateAlertResponse> CreateOrModifyAlertAsync(string accountId, CreateAlertRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<AlertSummary>> GetAlertsAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AlertDetail> GetAlertDetailAsync(string alertId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<DeleteAlertResponse> DeleteAlertAsync(string accountId, string alertId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private class FakeWatchlistOperations : IWatchlistOperations
    {
        public Task<CreateWatchlistResponse> CreateWatchlistAsync(CreateWatchlistRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<GetWatchlistsResponse> GetWatchlistsAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<WatchlistDetail> GetWatchlistAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<DeleteWatchlistResponse> DeleteWatchlistAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private class FakeFyiOperations : IFyiOperations
    {
        public Task<UnreadBulletinCountResponse> GetUnreadCountAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<FyiSettingItem>> GetSettingsAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<FyiAcknowledgementResponse> UpdateSettingAsync(string typecode, bool enabled, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<FyiDisclaimerResponse> GetDisclaimerAsync(string typecode, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<FyiAcknowledgementResponse> MarkDisclaimerReadAsync(string typecode, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<FyiDeliveryOptionsResponse> GetDeliveryOptionsAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<FyiAcknowledgementResponse> SetEmailDeliveryAsync(bool enabled, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<FyiAcknowledgementResponse> RegisterDeviceAsync(FyiDeviceRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task DeleteDeviceAsync(string deviceId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<FyiNotification>> GetNotificationsAsync(string? max = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<FyiNotification>> GetMoreNotificationsAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<FyiNotificationReadResponse> MarkNotificationReadAsync(string notificationId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private class FakeAllocationOperations : IAllocationOperations
    {
        public Task<AllocationAccountsResponse> GetAccountsAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AllocationGroupListResponse> GetGroupsAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AllocationSuccessResponse> AddGroupAsync(AllocationGroupRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AllocationGroupDetail> GetGroupAsync(string name, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AllocationSuccessResponse> DeleteGroupAsync(string name, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AllocationSuccessResponse> ModifyGroupAsync(AllocationGroupRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AllocationPresetsResponse> GetPresetsAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AllocationSuccessResponse> SetPresetsAsync(AllocationPresetsRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private class FakeSessionManager : ISessionManager
    {
        public Task EnsureInitializedAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ReauthenticateAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() =>
            ValueTask.CompletedTask;
    }
}
