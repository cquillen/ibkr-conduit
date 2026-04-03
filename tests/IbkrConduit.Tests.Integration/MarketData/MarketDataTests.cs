using System;
using System.Threading.Tasks;
using IbkrConduit.MarketData;
using IbkrConduit.Tests.Integration.Fixtures;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.MarketData;

public class MarketDataTests : IAsyncLifetime, IDisposable
{
    private TestHarness _harness = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = await TestHarness.CreateAsync();
    }

    [Fact]
    public async Task GetSnapshot_ReturnsFields()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/marketdata/snapshot",
            FixtureLoader.LoadBody("MarketData", "GET-snapshot"));

        var snapshots = await _harness.Client.MarketData.GetSnapshotAsync(
            [756733], ["31", "84", "86"], TestContext.Current.CancellationToken);

        snapshots.ShouldNotBeEmpty();
        var snap = snapshots[0];
        snap.Conid.ShouldBe(756733);
        snap.LastPrice.ShouldBe("655.89");
        snap.BidPrice.ShouldBe("655.88");
        snap.AskPrice.ShouldBe("655.90");
        snap.MarketDataAvailability.ShouldBe("S");
        snap.AllFields.ShouldNotBeNull();

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetSnapshot_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/marketdata/snapshot")
                .UsingGet())
            .InScenario("snapshot-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/marketdata/snapshot")
                .UsingGet())
            .InScenario("snapshot-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("MarketData", "GET-snapshot")));

        var snapshots = await _harness.Client.MarketData.GetSnapshotAsync(
            [756733], ["31", "84", "86"], TestContext.Current.CancellationToken);

        snapshots.ShouldNotBeEmpty();
        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetHistory_ReturnsBarData()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/marketdata/history",
            FixtureLoader.LoadBody("MarketData", "GET-history"));

        var history = await _harness.Client.MarketData.GetHistoryAsync(
            756733, "1d", "1min", cancellationToken: TestContext.Current.CancellationToken);

        history.ShouldNotBeNull();
        history.Symbol.ShouldBe("SPY");
        history.TimePeriod.ShouldBe("1d");
        history.BarLength.ShouldBe(60);
        history.Data.ShouldNotBeNull();
        history.Data!.Count.ShouldBe(3);
        var bar = history.Data![0];
        bar.Open.ShouldBe(646.42m);
        bar.Close.ShouldBe(646.86m);
        bar.High.ShouldBe(647.47m);
        bar.Low.ShouldBe(646.38m);
        bar.Volume.ShouldBe(17219.125m);
        bar.Timestamp.ShouldBe(1775136600000L);

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetHistory_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/marketdata/history")
                .UsingGet())
            .InScenario("history-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/marketdata/history")
                .UsingGet())
            .InScenario("history-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("MarketData", "GET-history")));

        var history = await _harness.Client.MarketData.GetHistoryAsync(
            756733, "1d", "1min", cancellationToken: TestContext.Current.CancellationToken);

        history.Symbol.ShouldBe("SPY");
        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetScannerParameters_ReturnsParams()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/scanner/params",
            FixtureLoader.LoadBody("MarketData", "GET-scanner-params"));

        var parameters = await _harness.Client.MarketData.GetScannerParametersAsync(
            TestContext.Current.CancellationToken);

        parameters.ShouldNotBeNull();
        parameters.ScanTypeList.ShouldNotBeNull();
        parameters.ScanTypeList!.Count.ShouldBe(2);
        parameters.ScanTypeList![0].Code.ShouldBe("TOP_PERC_GAIN");
        parameters.ScanTypeList![0].DisplayName.ShouldBe("Top % Gainers");
        parameters.InstrumentList.ShouldNotBeNull();
        parameters.FilterList.ShouldNotBeNull();
        parameters.LocationTree.ShouldNotBeNull();

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task RunScanner_ReturnsContracts()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/iserver/scanner/run",
            FixtureLoader.LoadBody("MarketData", "POST-scanner-run"));

        var result = await _harness.Client.MarketData.RunScannerAsync(
            new ScannerRequest("STK", "TOP_PERC_GAIN", "STK.US.MAJOR", []),
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Contracts.ShouldNotBeNull();
        result.Contracts!.Count.ShouldBe(3);
        result.ScanDataColumnName.ShouldBe("Chg%");
        var first = result.Contracts![0];
        first.Symbol.ShouldBe("GV");
        first.ConId.ShouldBe(706149062);
        first.CompanyName.ShouldBe("VISIONARY HOLDINGS INC");
        first.ListingExchange.ShouldBe("NASDAQ.SCM");
        first.SecType.ShouldBe("STK");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task RunScanner_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/scanner/run")
                .UsingPost())
            .InScenario("scanner-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/scanner/run")
                .UsingPost())
            .InScenario("scanner-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("MarketData", "POST-scanner-run")));

        var result = await _harness.Client.MarketData.RunScannerAsync(
            new ScannerRequest("STK", "TOP_PERC_GAIN", "STK.US.MAJOR", []),
            TestContext.Current.CancellationToken);

        result.Contracts.ShouldNotBeNull();
        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task UnsubscribeAll_ReturnsConfirmation()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/marketdata/unsubscribeall",
            FixtureLoader.LoadBody("MarketData", "GET-unsubscribeall"));

        var result = await _harness.Client.MarketData.UnsubscribeAllAsync(
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Unsubscribed.ShouldBeTrue();

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task Unsubscribe_ReturnsResult()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/iserver/marketdata/unsubscribe",
            FixtureLoader.LoadBody("MarketData", "POST-unsubscribe"));

        var result = await _harness.Client.MarketData.UnsubscribeAsync(
            756733, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task Unsubscribe_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/marketdata/unsubscribe")
                .UsingPost())
            .InScenario("unsubscribe-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/marketdata/unsubscribe")
                .UsingPost())
            .InScenario("unsubscribe-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("MarketData", "POST-unsubscribe")));

        var result = await _harness.Client.MarketData.UnsubscribeAsync(
            756733, TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
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
