using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Http;
using IbkrConduit.Watchlists;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.Watchlists;

public class WatchlistEndpointTests : IDisposable
{
    private readonly WireMockServer _server;

    public WatchlistEndpointTests()
    {
        _server = WireMockServer.Start();
    }

    [Fact]
    public async Task CreateWatchlistAsync_ReturnsCreatedWatchlist()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/watchlist")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"id":"99999","hash":"1775319529009","name":"My Watchlist","readOnly":false,"instruments":[]}"""));

        var api = CreateRefitClient<IIbkrWatchlistApi>();
        var request = new CreateWatchlistRequest(
            Id: "My Watchlist",
            Name: "My Watchlist",
            Rows: new List<WatchlistRow>
            {
                new(C: 265598, H: "AAPL"),
            });

        var result = await api.CreateWatchlistAsync(request, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Id.ShouldBe("99999");
        result.Hash.ShouldBe("1775319529009");
        result.Name.ShouldBe("My Watchlist");
    }

    [Fact]
    public async Task GetWatchlistsAsync_ReturnsWatchlistList()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/watchlists")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "data": {
                                "scanners_only": false,
                                "show_scanners": false,
                                "bulk_delete": false,
                                "user_lists": [
                                    {
                                        "id": "wl1",
                                        "name": "Tech Stocks",
                                        "modified": 1700000000,
                                        "is_open": false,
                                        "read_only": false,
                                        "type": "watchlist"
                                    }
                                ]
                            },
                            "action": "content",
                            "MID": "1"
                        }
                        """));

        var api = CreateRefitClient<IIbkrWatchlistApi>();

        var result = await api.GetWatchlistsAsync(cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Data.UserLists.Count.ShouldBe(1);
        result.Data.UserLists[0].Id.ShouldBe("wl1");
        result.Data.UserLists[0].Name.ShouldBe("Tech Stocks");
        result.Data.UserLists[0].Modified.ShouldBe(1700000000);
    }

    [Fact]
    public async Task GetWatchlistAsync_ReturnsWatchlistDetail()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/watchlist")
                .WithParam("id", "wl1")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "id": "wl1",
                            "hash": "123456789",
                            "name": "Tech Stocks",
                            "readOnly": false,
                            "instruments": [
                                { "ST": "STK", "C": "265598", "conid": 265598, "name": "APPLE INC", "fullName": "AAPL", "assetClass": "STK", "ticker": "AAPL", "chineseName": null },
                                { "ST": "STK", "C": "272093", "conid": 272093, "name": "MICROSOFT CORP", "fullName": "MSFT", "assetClass": "STK", "ticker": "MSFT", "chineseName": null }
                            ]
                        }
                        """));

        var api = CreateRefitClient<IIbkrWatchlistApi>();

        var result = await api.GetWatchlistAsync("wl1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Id.ShouldBe("wl1");
        result.Name.ShouldBe("Tech Stocks");
        result.Instruments.Count.ShouldBe(2);
        result.Instruments[0].Conid.ShouldBe(265598);
        result.Instruments[0].Ticker.ShouldBe("AAPL");
    }

    [Fact]
    public async Task DeleteWatchlistAsync_ReturnsDeleteResult()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/watchlist")
                .WithParam("id", "wl1")
                .UsingDelete())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"data":{"deleted":"wl1"},"action":"context","MID":"2"}"""));

        var api = CreateRefitClient<IIbkrWatchlistApi>();

        var result = await api.DeleteWatchlistAsync("wl1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Data.Deleted.ShouldBe("wl1");
        result.Action.ShouldBe("context");
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
