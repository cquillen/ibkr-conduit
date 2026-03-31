using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.MarketData;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.MarketData;

[Collection("IBKR E2E")]
public class MarketDataTests : IDisposable
{
    private readonly WireMockServer _server;

    public MarketDataTests()
    {
        _server = WireMockServer.Start();
    }

    [Fact]
    public async Task GetSnapshotAsync_WithMockedServer_ReturnsSnapshotData()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/marketdata/snapshot")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        [
                            {
                                "conid": 265598,
                                "31": "193.18",
                                "84": "193.06",
                                "86": "193.14",
                                "87": "50000000",
                                "6509": "R",
                                "_updated": 1702334859712
                            }
                        ]
                        """));

        var api = CreateRefitClient<IIbkrMarketDataApi>();

        var result = await api.GetSnapshotAsync("265598", "31,84,86,87,6509", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Conid.ShouldBe(265598);
        result[0].MarketDataAvailability.ShouldBe("R");
        result[0].Updated.ShouldBe(1702334859712);
        result[0].Fields.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetHistoryAsync_WithMockedServer_ReturnsHistoricalBars()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/marketdata/history")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "symbol": "AAPL",
                            "text": "Apple Inc",
                            "priceFactor": 100,
                            "startTime": "20231201-09:30:00",
                            "data": [
                                {
                                    "o": 192.00,
                                    "c": 193.50,
                                    "h": 195.00,
                                    "l": 191.50,
                                    "v": 50000000,
                                    "t": 1701432600000
                                },
                                {
                                    "o": 193.50,
                                    "c": 194.25,
                                    "h": 194.80,
                                    "l": 193.00,
                                    "v": 45000000,
                                    "t": 1701519000000
                                }
                            ],
                            "points": 2,
                            "travelTime": 10
                        }
                        """));

        var api = CreateRefitClient<IIbkrMarketDataApi>();

        var result = await api.GetHistoryAsync("265598", "2d", "1d", cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Symbol.ShouldBe("AAPL");
        result.Data.ShouldNotBeNull();
        result.Data!.Count.ShouldBe(2);
        result.Data[0].Open.ShouldBe(192.00m);
        result.Data[0].Close.ShouldBe(193.50m);
        result.Data[0].High.ShouldBe(195.00m);
        result.Data[0].Low.ShouldBe(191.50m);
        result.Data[0].Volume.ShouldBe(50000000m);
        result.Data[1].Open.ShouldBe(193.50m);
        result.Points.ShouldBe(2);
    }

    /// <summary>
    /// End-to-end test against real IBKR paper account.
    /// Verifies portfolio positions and market data snapshot for SPY.
    /// </summary>
    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task EndToEnd_PortfolioPositionsAndSnapshot_Succeeds()
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

        var sessionSigningHandler = new OAuthSigningHandler(tokenProvider, creds.ConsumerKey, creds.AccessToken)
        {
            InnerHandler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
            },
        };
        using var sessionHttpClient = new HttpClient(sessionSigningHandler)
        {
            BaseAddress = new Uri("https://api.ibkr.com"),
        };
        var sessionApi = Refit.RestService.For<IbkrConduit.Session.IIbkrSessionApi>(sessionHttpClient);

        var options = new IbkrConduit.Session.IbkrClientOptions { Compete = true };
        var tickleTimerFactory = new IbkrConduit.Session.TickleTimerFactory(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<IbkrConduit.Session.TickleTimer>.Instance);
        await using var sessionManager = new IbkrConduit.Session.SessionManager(
            tokenProvider, tickleTimerFactory, sessionApi, options,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<IbkrConduit.Session.SessionManager>.Instance);

        var consumerSigningHandler = new OAuthSigningHandler(
            tokenProvider, creds.ConsumerKey, creds.AccessToken, sessionManager)
        {
            InnerHandler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
            },
        };
        using var httpClient = new HttpClient(consumerSigningHandler)
        {
            BaseAddress = new Uri("https://api.ibkr.com"),
        };

        var portfolioApi = Refit.RestService.For<IbkrConduit.Portfolio.IIbkrPortfolioApi>(httpClient);
        var marketDataApi = Refit.RestService.For<IIbkrMarketDataApi>(httpClient);
        var contractApi = Refit.RestService.For<IbkrConduit.Contracts.IIbkrContractApi>(httpClient);

        var ct = TestContext.Current.CancellationToken;

        // Get account ID
        var accounts = await portfolioApi.GetAccountsAsync(ct);
        accounts.ShouldNotBeNull();
        accounts.ShouldNotBeEmpty();
        var accountId = accounts[0].Id;

        // Get positions — should have SPY from M3b order
        var positions = await portfolioApi.GetPositionsAsync(accountId, cancellationToken: ct);
        positions.ShouldNotBeNull();

        // Get account summary
        var summary = await portfolioApi.GetAccountSummaryAsync(accountId, ct);
        summary.ShouldNotBeNull();
        summary.ShouldNotBeEmpty();

        // Get ledger
        var ledger = await portfolioApi.GetLedgerAsync(accountId, ct);
        ledger.ShouldNotBeNull();
        ledger.ShouldNotBeEmpty();

        // Look up SPY conid for snapshot
        var spyResults = await contractApi.SearchBySymbolAsync("SPY", ct);
        spyResults.ShouldNotBeNull();
        spyResults.ShouldNotBeEmpty();
        var spyConid = spyResults[0].Conid;

        // Market data snapshot — pre-flight + data
        var snapshots = await marketDataApi.GetSnapshotAsync(
            spyConid.ToString(System.Globalization.CultureInfo.InvariantCulture),
            $"{MarketDataFields.LastPrice},{MarketDataFields.BidPrice},{MarketDataFields.AskPrice},{MarketDataFields.Volume}",
            ct);
        snapshots.ShouldNotBeNull();
        snapshots.ShouldNotBeEmpty();
        snapshots[0].Conid.ShouldBe(spyConid);
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
