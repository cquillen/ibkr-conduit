using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using IbkrConduit.Tests.Integration.Fixtures;
using IbkrConduit.Watchlists;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Watchlists;

public class WatchlistTests : IAsyncLifetime, IDisposable
{
    private TestHarness _harness = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = await TestHarness.CreateAsync();
    }

    [Fact]
    public async Task CreateWatchlist_Success_ReturnsWatchlistWithIdAndHash()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/iserver/watchlist",
            FixtureLoader.LoadBody("Watchlists", "POST-create-watchlist"));

        var request = new CreateWatchlistRequest("99999", "Capture Test", new List<WatchlistRow>());
        var result = await _harness.Client.Watchlists.CreateWatchlistAsync(request, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Id.ShouldBe("99999");
        result.Hash.ShouldBe("1775319529009");
        result.Name.ShouldBe("Capture Test");
        result.ReadOnly.ShouldBeFalse();
        result.Instruments.ShouldBeEmpty();

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetWatchlists_Success_ReturnsUserLists()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/watchlists",
            FixtureLoader.LoadBody("Watchlists", "GET-watchlists"));

        var result = await _harness.Client.Watchlists.GetWatchlistsAsync(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Action.ShouldBe("content");
        result.Mid.ShouldBe("1");
        result.Data.ShouldNotBeNull();
        result.Data.ScannersOnly.ShouldBeFalse();
        result.Data.ShowScanners.ShouldBeFalse();
        result.Data.BulkDelete.ShouldBeFalse();
        result.Data.UserLists.ShouldNotBeEmpty();
        result.Data.UserLists.Count.ShouldBe(1);

        var summary = result.Data.UserLists[0];
        summary.Id.ShouldBe("99999");
        summary.Name.ShouldBe("Capture Test");
        summary.Modified.ShouldBe(1775319529009);
        summary.IsOpen.ShouldBeFalse();
        summary.ReadOnly.ShouldBeFalse();
        summary.Type.ShouldBe("watchlist");

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetWatchlist_Success_ReturnsInstrumentDetails()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/watchlist",
            FixtureLoader.LoadBody("Watchlists", "GET-watchlist-by-id"));

        var result = await _harness.Client.Watchlists.GetWatchlistAsync("99999", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Id.ShouldBe("99999");
        result.Hash.ShouldBe("1775319529009");
        result.Name.ShouldBe("Capture Test");
        result.ReadOnly.ShouldBeFalse();
        result.Instruments.ShouldNotBeEmpty();
        result.Instruments.Count.ShouldBe(1);

        var instrument = result.Instruments[0];
        instrument.St.ShouldBe("STK");
        instrument.C.ShouldBe("756733");
        instrument.Conid.ShouldBe(756733);
        instrument.Name.ShouldBe("SS SPDR S&P 500 ETF TRUST-US");
        instrument.FullName.ShouldBe("SPY");
        instrument.AssetClass.ShouldBe("STK");
        instrument.Ticker.ShouldBe("SPY");
        instrument.ChineseName.ShouldNotBeNull();

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task DeleteWatchlist_Success_ReturnsDeletedId()
    {
        _harness.StubAuthenticated(
            HttpMethod.Delete,
            "/v1/api/iserver/watchlist",
            FixtureLoader.LoadBody("Watchlists", "DELETE-watchlist"));

        var result = await _harness.Client.Watchlists.DeleteWatchlistAsync("99999", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Data.ShouldNotBeNull();
        result.Data.Deleted.ShouldBe("99999");
        result.Action.ShouldBe("context");
        result.Mid.ShouldBe("2");

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task CreateWatchlist_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/watchlist")
                .UsingPost())
            .InScenario("create-watchlist-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/watchlist")
                .UsingPost())
            .InScenario("create-watchlist-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Watchlists", "POST-create-watchlist")));

        var request = new CreateWatchlistRequest("99999", "Capture Test", new List<WatchlistRow>());
        var result = await _harness.Client.Watchlists.CreateWatchlistAsync(request, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Id.ShouldBe("99999");
        result.Name.ShouldBe("Capture Test");

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetWatchlists_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/watchlists")
                .UsingGet())
            .InScenario("get-watchlists-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/watchlists")
                .UsingGet())
            .InScenario("get-watchlists-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Watchlists", "GET-watchlists")));

        var result = await _harness.Client.Watchlists.GetWatchlistsAsync(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Data.UserLists.ShouldNotBeEmpty();

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetWatchlist_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/watchlist")
                .UsingGet())
            .InScenario("get-watchlist-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/watchlist")
                .UsingGet())
            .InScenario("get-watchlist-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Watchlists", "GET-watchlist-by-id")));

        var result = await _harness.Client.Watchlists.GetWatchlistAsync("99999", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Id.ShouldBe("99999");
        result.Instruments.ShouldNotBeEmpty();

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task DeleteWatchlist_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/watchlist")
                .UsingDelete())
            .InScenario("delete-watchlist-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/watchlist")
                .UsingDelete())
            .InScenario("delete-watchlist-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Watchlists", "DELETE-watchlist")));

        var result = await _harness.Client.Watchlists.DeleteWatchlistAsync("99999", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Data.Deleted.ShouldBe("99999");

        _harness.VerifyReauthenticationOccurred();
    }

    public async ValueTask DisposeAsync()
    {
        await _harness.DisposeAsync();
    }

    public void Dispose()
    {
        _harness.Dispose();
        GC.SuppressFinalize(this);
    }
}
