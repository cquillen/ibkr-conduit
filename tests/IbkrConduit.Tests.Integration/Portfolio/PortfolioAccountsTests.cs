using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using IbkrConduit.Portfolio;
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
    public async Task GetAccountsAsync_WithMockedServer_ReturnsAccountList()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet()
                .WithHeader("Authorization", "*"))
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

        var api = CreateRefitClient<IIbkrPortfolioApi>();

        var accounts = await api.GetAccountsAsync(TestContext.Current.CancellationToken);

        accounts.ShouldNotBeNull();
        accounts.Count.ShouldBe(1);
        accounts[0].Id.ShouldBe("DU1234567");
        accounts[0].AccountTitle.ShouldBe("Paper Trading");
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
        services.AddIbkrClient(creds);

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IIbkrClient>();

        var accounts = await client.Portfolio.GetAccountsAsync(TestContext.Current.CancellationToken);

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

        var positions = await api.GetPositionsAsync("DU1234567", cancellationToken: TestContext.Current.CancellationToken);

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

        var summary = await api.GetAccountSummaryAsync("DU1234567", TestContext.Current.CancellationToken);

        summary.ShouldNotBeNull();
        summary.ShouldContainKey("netliquidationvalue");
        summary["netliquidationvalue"].Amount.ShouldBe(100000.50m);
        summary["netliquidationvalue"].Currency.ShouldBe("USD");
        summary.ShouldContainKey("totalcashvalue");
        summary["totalcashvalue"].Amount.ShouldBe(55000.00m);
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
