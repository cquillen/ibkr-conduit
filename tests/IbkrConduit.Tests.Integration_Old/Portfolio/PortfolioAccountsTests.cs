using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using IbkrConduit.Portfolio;
using IbkrConduit.Tests.Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.Portfolio;

[Collection("IBKR E2E")]
public class PortfolioAccountsTests : IDisposable
{
    private readonly WireMockServer _server;

    public PortfolioAccountsTests()
    {
        _server = WireMockServer.Start();
    }

    [Fact]
    public async Task GetAccountsAsync_WithFixture_DeserializesAllFields()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Portfolio", "GET-portfolio-accounts")));

        var api = CreateRefitClient<IIbkrPortfolioApi>();

        var accounts = await api.GetAccountsAsync(TestContext.Current.CancellationToken).Content!;

        accounts.ShouldNotBeNull();
        accounts.Count.ShouldBe(1);

        var account = accounts[0];
        account.Id.ShouldBe("U1234567");
        account.AccountId.ShouldBe("U1234567");
        account.AccountTitle.ShouldBe("Test User");
        account.DisplayName.ShouldBe("Test User");
        account.Type.ShouldBe("DEMO");
        account.Currency.ShouldBe("USD");
        account.TradingType.ShouldBe("STKNOPT");
        account.BusinessType.ShouldBe("INDEPENDENT");
        account.IbEntity.ShouldBe("IBLLC-US");
        account.BrokerageAccess.ShouldBeTrue();
        account.Faclient.ShouldBeFalse();
        account.ClearingStatus.ShouldBe("O");
        account.Parent.ShouldNotBeNull();
        account.Parent!.IsMParent.ShouldBeFalse();
        account.Parent!.IsMultiplex.ShouldBeFalse();
    }

    [Fact]
    public async Task GetAccountsAsync_Unauthorized_ThrowsApiException()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        var api = CreateRefitClient<IIbkrPortfolioApi>();

        await Should.ThrowAsync<Refit.ApiException>(() => api.GetAccountsAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// End-to-end test against a real IBKR paper account.
    /// Runs automatically when IBKR_CONSUMER_KEY environment variable is set.
    /// </summary>
    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task PortfolioAccounts_WithPaperAccount_ReturnsAccountList()
    {
        using var creds = OAuthCredentialsFactory.FromEnvironment();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIbkrClient(opts => opts.Credentials = creds);

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IIbkrClient>();

        var accounts = (await client.Portfolio.GetAccountsAsync(TestContext.Current.CancellationToken)).Value;

        accounts.ShouldNotBeNull();
        accounts.ShouldNotBeEmpty();
        accounts[0].Id.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetPositionsAsync_WithMockedServer_ReturnsPositionList()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/DU1234567/positions/0")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        [
                            {
                                "acctId": "DU1234567",
                                "conid": 265598,
                                "contractDesc": "SPY",
                                "position": 100.0,
                                "mktPrice": 450.25,
                                "mktValue": 45025.00,
                                "avgCost": 440.00,
                                "avgPrice": 440.00,
                                "realizedPnl": 0.0,
                                "unrealizedPnl": 1025.00,
                                "currency": "USD",
                                "name": "SPDR S&P 500 ETF Trust",
                                "assetClass": "STK",
                                "sector": null,
                                "ticker": "SPY",
                                "multiplier": null,
                                "isUS": true
                            }
                        ]
                        """));

        var api = CreateRefitClient<IIbkrPortfolioApi>();

        var positions = await api.GetPositionsAsync("DU1234567", cancellationToken: TestContext.Current.CancellationToken).Content!;

        positions.ShouldNotBeNull();
        positions.Count.ShouldBe(1);
        positions[0].AccountId.ShouldBe("DU1234567");
        positions[0].Conid.ShouldBe(265598);
        positions[0].Ticker.ShouldBe("SPY");
        positions[0].Quantity.ShouldBe(100m);
        positions[0].MarketPrice.ShouldBe(450.25m);
        positions[0].UnrealizedPnl.ShouldBe(1025.00m);
        positions[0].IsUs.ShouldBe(true);
    }

    [Fact]
    public async Task GetAccountSummaryAsync_WithMockedServer_ReturnsSummary()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/DU1234567/summary")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "netliquidationvalue": {
                                "amount": 100000.50,
                                "currency": "USD",
                                "isNull": false,
                                "timestamp": 1702334859712,
                                "value": "100000.50"
                            },
                            "totalcashvalue": {
                                "amount": 55000.00,
                                "currency": "USD",
                                "isNull": false,
                                "timestamp": 1702334859712,
                                "value": "55000.00"
                            }
                        }
                        """));

        var api = CreateRefitClient<IIbkrPortfolioApi>();

        var summary = await api.GetAccountSummaryAsync("DU1234567", TestContext.Current.CancellationToken).Content!;

        summary.ShouldNotBeNull();
        summary.ShouldContainKey("netliquidationvalue");
        summary["netliquidationvalue"].Amount.ShouldBe(100000.50m);
        summary["netliquidationvalue"].Currency.ShouldBe("USD");
        summary.ShouldContainKey("totalcashvalue");
        summary["totalcashvalue"].Amount.ShouldBe(55000.00m);
    }

    [Fact]
    public async Task GetConsolidatedAllocationAsync_WithMockedServer_ReturnsAllocation()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/allocation")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "assetClass": {
                                "long": { "STK": 316441.23 },
                                "short": { "OPT": -30.0 }
                            },
                            "sector": {
                                "long": { "Technology": 237014.73 }
                            },
                            "group": {
                                "long": { "Computers": 121222.53 }
                            }
                        }
                        """));

        var api = CreateRefitClient<IIbkrPortfolioApi>();

        var result = await api.GetConsolidatedAllocationAsync(
            new ConsolidatedAllocationRequest(["DU1234567"]),
            TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.AssetClass.ShouldNotBeNull();
        result.AssetClass!["long"]["STK"].ShouldBe(316441.23m);
        result.Sector.ShouldNotBeNull();
        result.Sector!["long"]["Technology"].ShouldBe(237014.73m);
    }

    [Fact]
    public async Task GetComboPositionsAsync_WithMockedServer_ReturnsComboPositions()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/DU1234567/combo/positions")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        [
                            {
                                "name": "CP.CP66a00d50",
                                "description": "1*708474422-1*710225103",
                                "legs": [
                                    { "conid": "708474422", "ratio": 1 },
                                    { "conid": "710225103", "ratio": -1 }
                                ],
                                "positions": []
                            }
                        ]
                        """));

        var api = CreateRefitClient<IIbkrPortfolioApi>();

        var result = await api.GetComboPositionsAsync("DU1234567",
            cancellationToken: TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("CP.CP66a00d50");
        result[0].Legs.ShouldNotBeNull();
        result[0].Legs!.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetRealTimePositionsAsync_WithMockedServer_ReturnsPositions()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio2/DU1234567/positions")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        [
                            {
                                "acctId": "DU1234567",
                                "conid": 265598,
                                "contractDesc": "SPY",
                                "position": 100.0,
                                "mktPrice": 450.25,
                                "mktValue": 45025.00,
                                "avgCost": 440.00,
                                "avgPrice": 440.00,
                                "realizedPnl": 0.0,
                                "unrealizedPnl": 1025.00,
                                "currency": "USD",
                                "name": "SPDR S&P 500 ETF Trust",
                                "assetClass": "STK",
                                "sector": null,
                                "ticker": "SPY",
                                "multiplier": null,
                                "isUS": true
                            }
                        ]
                        """));

        var api = CreateRefitClient<IIbkrPortfolioApi>();

        var result = await api.GetRealTimePositionsAsync("DU1234567",
            cancellationToken: TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].AccountId.ShouldBe("DU1234567");
        result[0].Ticker.ShouldBe("SPY");
    }

    [Fact]
    public async Task GetSubAccountsAsync_WithMockedServer_ReturnsSubAccounts()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/subaccounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        [
                            {
                                "id": "U1234567",
                                "accountId": "U1234567",
                                "accountTitle": "Paper Trading",
                                "type": "INDIVIDUAL",
                                "desc": "U1234567"
                            }
                        ]
                        """));

        var api = CreateRefitClient<IIbkrPortfolioApi>();

        var result = await api.GetSubAccountsAsync(TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe("U1234567");
        result[0].AccountTitle.ShouldBe("Paper Trading");
    }

    [Fact]
    public async Task GetSubAccountsPagedAsync_WithMockedServer_ReturnsSubAccounts()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/subaccounts2")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        [
                            {
                                "id": "U1234567",
                                "accountId": "U1234567",
                                "accountTitle": "Paper Trading",
                                "type": "INDIVIDUAL",
                                "desc": "U1234567"
                            }
                        ]
                        """));

        var api = CreateRefitClient<IIbkrPortfolioApi>();

        var result = await api.GetSubAccountsPagedAsync(0, TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe("U1234567");
    }

    [Fact]
    public async Task GetAllPeriodsPerformanceAsync_WithMockedServer_ReturnsPerformance()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/pa/allperiods")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "currencyType": "base",
                            "rc": 0,
                            "view": ["U1234567"],
                            "nd": 366,
                            "id": "getPerformanceAllPeriods",
                            "pm": "TWR"
                        }
                        """));

        var api = CreateRefitClient<IIbkrPortfolioApi>();

        var result = await api.GetAllPeriodsPerformanceAsync(
            new AllPeriodsRequest(["U1234567"]),
            TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.CurrencyType.ShouldBe("base");
        result.Rc.ShouldBe(0);
    }

    [Fact]
    public async Task GetPartitionedPnlAsync_WithMockedServer_ReturnsPnl()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/pnl/partitioned")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "upnl": {
                                "U1234567.Core": {
                                    "rowType": 1,
                                    "dpl": 15.7,
                                    "nl": 10000.0,
                                    "upl": 607.0,
                                    "el": 10000.0,
                                    "mv": 0.0
                                }
                            }
                        }
                        """));

        var api = CreateRefitClient<IIbkrPortfolioApi>();

        var result = await api.GetPartitionedPnlAsync(TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.Upnl.ShouldNotBeNull();
        result.Upnl!.ShouldContainKey("U1234567.Core");
        result.Upnl["U1234567.Core"].Dpl.ShouldBe(15.7m);
        result.Upnl["U1234567.Core"].Nl.ShouldBe(10000.0m);
        result.Upnl["U1234567.Core"].Upl.ShouldBe(607.0m);
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
}
