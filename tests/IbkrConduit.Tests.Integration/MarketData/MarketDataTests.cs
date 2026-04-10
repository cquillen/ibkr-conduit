using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using IbkrConduit.Errors;
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

        var snapshots = (await _harness.Client.MarketData.GetSnapshotAsync(
            [756733], ["31", "84", "86"], TestContext.Current.CancellationToken)).Value;

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

        var snapshots = (await _harness.Client.MarketData.GetSnapshotAsync(
            [756733], ["31", "84", "86"], TestContext.Current.CancellationToken)).Value;

        snapshots.ShouldNotBeEmpty();
        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetHistory_ReturnsBarData()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/marketdata/history",
            FixtureLoader.LoadBody("MarketData", "GET-history"));

        var history = (await _harness.Client.MarketData.GetHistoryAsync(
            756733, HistoryPeriod.Days(1), BarSize.Minutes(1), cancellationToken: TestContext.Current.CancellationToken)).Value;

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

        var history = (await _harness.Client.MarketData.GetHistoryAsync(
            756733, HistoryPeriod.Days(1), BarSize.Minutes(1), cancellationToken: TestContext.Current.CancellationToken)).Value;

        history.Symbol.ShouldBe("SPY");
        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetScannerParameters_ReturnsParams()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/scanner/params",
            FixtureLoader.LoadBody("MarketData", "GET-scanner-params"));

        var parameters = (await _harness.Client.MarketData.GetScannerParametersAsync(
            TestContext.Current.CancellationToken)).Value;

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

        var result = (await _harness.Client.MarketData.RunScannerAsync(
            new ScannerRequest("STK", "TOP_PERC_GAIN", "STK.US.MAJOR", []),
            TestContext.Current.CancellationToken)).Value;

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

        var result = (await _harness.Client.MarketData.RunScannerAsync(
            new ScannerRequest("STK", "TOP_PERC_GAIN", "STK.US.MAJOR", []),
            TestContext.Current.CancellationToken)).Value;

        result.Contracts.ShouldNotBeNull();
        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task UnsubscribeAll_ReturnsConfirmation()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/marketdata/unsubscribeall",
            FixtureLoader.LoadBody("MarketData", "GET-unsubscribeall"));

        var result = (await _harness.Client.MarketData.UnsubscribeAllAsync(
            TestContext.Current.CancellationToken)).Value;

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

        var result = (await _harness.Client.MarketData.UnsubscribeAsync(
            756733, TestContext.Current.CancellationToken)).Value;

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

        var result = (await _harness.Client.MarketData.UnsubscribeAsync(
            756733, TestContext.Current.CancellationToken)).Value;

        result.Success.ShouldBeTrue();
        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetSnapshot_ServerError_ReturnsFailureResult()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/marketdata/snapshot")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(500)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"error":"Internal Server Error"}"""));

        var result = await _harness.Client.MarketData.GetSnapshotAsync(
            [756733], ["31", "84", "86"], TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<IbkrApiError>();
        error.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetSnapshot_PreflightRequired_RetriesAndReturnsFullData()
    {
        var ct = TestContext.Current.CancellationToken;

        // First call returns metadata-only snapshot (only keys in _nonDataKeys)
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/marketdata/snapshot")
                .UsingGet())
            .InScenario("preflight")
            .WillSetStateTo("ready")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""[{"conid":756733,"server_id":"q1234","_updated":1234567890,"conidEx":"756733","6509":"q1234"}]"""));

        // Second call returns full field data
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/marketdata/snapshot")
                .UsingGet())
            .InScenario("preflight")
            .WhenStateIs("ready")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""[{"conid":756733,"server_id":"q1234","_updated":1234567891,"conidEx":"756733","6509":"S","31":"655.50","84":"655.00","86":"656.00"}]"""));

        var snapshots = (await _harness.Client.MarketData.GetSnapshotAsync(
            [756733], ["31", "84", "86"], ct)).Value;

        snapshots.ShouldNotBeEmpty();
        var snap = snapshots[0];
        snap.Conid.ShouldBe(756733);
        snap.LastPrice.ShouldBe("655.50");
        snap.BidPrice.ShouldBe("655.00");
        snap.AskPrice.ShouldBe("656.00");

        // Verify WireMock received 2 requests to the snapshot endpoint
        var snapshotRequests = _harness.Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/iserver/marketdata/snapshot").UsingGet());
        snapshotRequests.Count.ShouldBeGreaterThanOrEqualTo(2,
            "Preflight should have triggered a second snapshot request");
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
