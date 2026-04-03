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
                    .WithBody("""{"id":"my-watchlist-123"}"""));

        var api = CreateRefitClient<IIbkrWatchlistApi>();
        var request = new CreateWatchlistRequest(
            Id: "My Watchlist",
            Rows: new List<WatchlistRow>
            {
                new(C: 265598, H: "AAPL"),
            });

        var result = await api.CreateWatchlistAsync(request, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Id.ShouldBe("my-watchlist-123");
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
                        [
                            {
                                "id": "wl1",
                                "name": "Tech Stocks",
                                "modified": 1700000000,
                                "instruments": 5
                            }
                        ]
                        """));

        var api = CreateRefitClient<IIbkrWatchlistApi>();

        var result = await api.GetWatchlistsAsync(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe("wl1");
        result[0].Name.ShouldBe("Tech Stocks");
        result[0].Modified.ShouldBe(1700000000);
        result[0].Instruments.ShouldBe(5);
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
                            "name": "Tech Stocks",
                            "rows": [
                                { "C": 265598, "H": "AAPL", "sym": "AAPL" },
                                { "C": 272093, "H": "MSFT", "sym": "MSFT" }
                            ]
                        }
                        """));

        var api = CreateRefitClient<IIbkrWatchlistApi>();

        var result = await api.GetWatchlistAsync("wl1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Id.ShouldBe("wl1");
        result.Name.ShouldBe("Tech Stocks");
        result.Rows.Count.ShouldBe(2);
        result.Rows[0].C.ShouldBe(265598);
        result.Rows[0].Sym.ShouldBe("AAPL");
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
                    .WithBody("""{"deleted":true,"id":"wl1"}"""));

        var api = CreateRefitClient<IIbkrWatchlistApi>();

        var result = await api.DeleteWatchlistAsync("wl1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Deleted.ShouldBeTrue();
        result.Id.ShouldBe("wl1");
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
