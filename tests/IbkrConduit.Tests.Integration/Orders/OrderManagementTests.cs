using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Contracts;
using IbkrConduit.Orders;
using IbkrConduit.Portfolio;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.Orders;

public class OrderManagementTests : IDisposable
{
    private readonly WireMockServer _server;

    public OrderManagementTests()
    {
        _server = WireMockServer.Start();
    }

    [Fact]
    public async Task PlaceOrderAsync_DirectConfirmation_ReturnsOrderResult()
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

        var result = await ops.PlaceOrderAsync("DU1234567", order);

        result.OrderId.ShouldBe("12345");
        result.OrderStatus.ShouldBe("PreSubmitted");
    }

    [Fact]
    public async Task PlaceOrderAsync_WithQuestion_AutoConfirmsAndReturnsOrderResult()
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

        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/reply/reply-abc")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        [
                            {
                                "order_id": "67890",
                                "order_status": "Submitted"
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

        var result = await ops.PlaceOrderAsync("DU1234567", order);

        result.OrderId.ShouldBe("67890");
        result.OrderStatus.ShouldBe("Submitted");
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

        var result = await ops.CancelOrderAsync("DU1234567", "12345");

        result.OrderId.ShouldBe("12345");
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

        var result = await ops.SearchBySymbolAsync("AAPL");

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

        var client = new IbkrClient(portfolio, contracts, orders, sessionManager);

        var accounts = await client.Portfolio.GetAccountsAsync();
        accounts.Count.ShouldBe(1);
        accounts[0].Id.ShouldBe("DU1234567");

        var searchResults = await client.Contracts.SearchBySymbolAsync("MSFT");
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

        using var lstHttpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
        })
        {
            BaseAddress = new Uri("https://api.ibkr.com/v1/api/"),
        };
        var lstClient = new LiveSessionTokenClient(lstHttpClient);
        var tokenProvider = new SessionTokenProvider(creds, lstClient);

        var signingHandler = new OAuthSigningHandler(tokenProvider, creds.ConsumerKey, creds.AccessToken)
        {
            InnerHandler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
            },
        };

        using var httpClient = new HttpClient(signingHandler)
        {
            BaseAddress = new Uri("https://api.ibkr.com"),
        };

        var portfolioApi = Refit.RestService.For<IIbkrPortfolioApi>(httpClient);
        var contractApi = Refit.RestService.For<IIbkrContractApi>(httpClient);
        var orderApi = Refit.RestService.For<IIbkrOrderApi>(httpClient);

        var portfolio = new PortfolioOperations(portfolioApi);
        var contracts = new ContractOperations(contractApi);
        var orders = new OrderOperations(orderApi, NullLogger<OrderOperations>.Instance);
        var sessionManager = new FakeSessionManager();

        var client = new IbkrClient(portfolio, contracts, orders, sessionManager);

        var accounts = await client.Portfolio.GetAccountsAsync();
        accounts.ShouldNotBeNull();
        accounts.ShouldNotBeEmpty();
        accounts[0].Id.ShouldNotBeNullOrWhiteSpace();

        var searchResults = await client.Contracts.SearchBySymbolAsync("AAPL");
        searchResults.ShouldNotBeNull();
        searchResults.ShouldNotBeEmpty();
        searchResults[0].Conid.ShouldBeGreaterThan(0);
        searchResults[0].Symbol.ShouldBe("AAPL");
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
