using System;
using System.Diagnostics;
using System.Threading.Tasks;
using IbkrConduit.Tests.Integration.Fixtures;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Portfolio;

/// <summary>
/// RPD-06 (ADR-0009, spec: docs/superpowers/specs/2026-07-14-rpd-06-cold-read-retry.md): WireMock
/// call-count-sequenced coverage for <c>GetPositionsAsync</c>'s internal retry-once-on-sparse-read.
/// Each test uses a distinct synthetic account id so the process-wide <see cref="ActivitySource"/>
/// (shared with every other concurrently-running test class) cannot cross-contaminate a captured
/// <see cref="Activity"/> — the capture filters on the <c>ibkr.account_id</c> tag, not just the
/// operation name.
/// </summary>
public class PortfolioColdReadRetryTests : IAsyncLifetime, IDisposable
{
    private TestHarness _harness = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = await TestHarness.CreateAsync();
    }

    [Fact]
    public async Task GetPositionsAsync_SparseFirstRead_RetriesOnceAndReturnsEnrichedData()
    {
        const string accountId = "U9990001";
        var path = $"/v1/api/portfolio/{accountId}/positions/0";

        _harness.Server.Given(
            Request.Create().WithPath(path).UsingGet())
            .InScenario("coldread-positions-retry")
            .WillSetStateTo("enriched")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Portfolio", "GET-coldread-positions-sparse")));

        _harness.Server.Given(
            Request.Create().WithPath(path).UsingGet())
            .InScenario("coldread-positions-retry")
            .WhenStateIs("enriched")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Portfolio", "GET-coldread-positions-enriched")));

        Activity? captured = null;
        using (var listener = MakeListener(accountId, a => captured = a))
        {
            var positions = (await _harness.Client.Portfolio.GetPositionsAsync(
                accountId, 0, cancellationToken: TestContext.Current.CancellationToken)).Value;

            positions.Count.ShouldBe(2);
            positions[0].Name.ShouldBe("INVESCO QQQ TRUST SERIES 1");
            positions[0].Ticker.ShouldBe("QQQ");
            positions[1].Name.ShouldBe("SS SPDR S&P 500 ETF TRUST-US");
            positions[1].Ticker.ShouldBe("SPY");
        }

        _harness.Server.FindLogEntries(Request.Create().WithPath(path).UsingGet())
            .Count.ShouldBe(2, "the sparse first read should trigger exactly one cold-read retry");

        captured.ShouldNotBeNull();
        captured!.GetTagItem("ibkr.cold_read_retry").ShouldBe(true);
    }

    [Fact]
    public async Task GetPositionsAsync_CleanFirstRead_DoesNotRetry()
    {
        const string accountId = "U9990002";
        var path = $"/v1/api/portfolio/{accountId}/positions/0";

        _harness.StubAuthenticatedGet(path,
            FixtureLoader.LoadBody("Portfolio", "GET-coldread-positions-enriched"));

        Activity? captured = null;
        using (var listener = MakeListener(accountId, a => captured = a))
        {
            var positions = (await _harness.Client.Portfolio.GetPositionsAsync(
                accountId, 0, cancellationToken: TestContext.Current.CancellationToken)).Value;

            positions.Count.ShouldBe(2);
        }

        _harness.Server.FindLogEntries(Request.Create().WithPath(path).UsingGet())
            .Count.ShouldBe(1, "a clean (enriched) first read must not trigger a retry");

        captured.ShouldNotBeNull();
        captured!.GetTagItem("ibkr.cold_read_retry").ShouldBeNull();
    }

    [Fact]
    public async Task GetPositionsAsync_SparseRetryStillSparse_CapsAtOneRetry()
    {
        const string accountId = "U9990003";
        var path = $"/v1/api/portfolio/{accountId}/positions/0";

        // Both calls return the sparse fixture — a genuinely thin account whose second read still
        // looks sparse. The retry must be capped at one attempt, never a third call.
        _harness.StubAuthenticatedGet(path,
            FixtureLoader.LoadBody("Portfolio", "GET-coldread-positions-sparse"));

        var positions = (await _harness.Client.Portfolio.GetPositionsAsync(
            accountId, 0, cancellationToken: TestContext.Current.CancellationToken)).Value;

        positions.Count.ShouldBe(2);
        positions[0].Name.ShouldBeNullOrEmpty();
        positions[0].Ticker.ShouldBeNullOrEmpty();

        _harness.Server.FindLogEntries(Request.Create().WithPath(path).UsingGet())
            .Count.ShouldBe(2, "a still-sparse retry must not trigger a second retry (capped at one)");
    }

    [Fact]
    public async Task GetPositionsAsync_401ThenSparseReplay_ComposesRetryWithReauth()
    {
        const string accountId = "U9990004";
        var path = $"/v1/api/portfolio/{accountId}/positions/0";

        // Call 1: 401 (triggers TokenRefreshHandler re-auth). Call 2: the replayed request, sparse.
        // Call 3: the cold-read retry issued on top of the replayed sparse result, enriched.
        _harness.Server.Given(
            Request.Create().WithPath(path).UsingGet())
            .InScenario("coldread-positions-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create().WithPath(path).UsingGet())
            .InScenario("coldread-positions-401")
            .WhenStateIs("token-expired")
            .WillSetStateTo("retried")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Portfolio", "GET-coldread-positions-sparse")));

        _harness.Server.Given(
            Request.Create().WithPath(path).UsingGet())
            .InScenario("coldread-positions-401")
            .WhenStateIs("retried")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Portfolio", "GET-coldread-positions-enriched")));

        Activity? captured = null;
        var captureCount = 0;
        using (var listener = MakeListener(accountId, a =>
        {
            captured = a;
            captureCount++;
        }))
        {
            var positions = (await _harness.Client.Portfolio.GetPositionsAsync(
                accountId, 0, cancellationToken: TestContext.Current.CancellationToken)).Value;

            positions.Count.ShouldBe(2);
            positions[0].Name.ShouldBe("INVESCO QQQ TRUST SERIES 1");
        }

        _harness.Server.FindLogEntries(Request.Create().WithPath(path).UsingGet())
            .Count.ShouldBe(3, "401 + replayed-sparse + cold-read-retry-enriched = 3 hits on the positions path");
        _harness.VerifyReauthenticationOccurred();

        captureCount.ShouldBe(1, "exactly one GetPositions span for the whole call, 401 replay stays inside it");
        captured.ShouldNotBeNull();
        captured!.GetTagItem("ibkr.cold_read_retry").ShouldBe(true);
    }

    /// <summary>
    /// A tightly-scoped <see cref="ActivityListener"/> that only reports spans whose
    /// <c>ibkr.account_id</c> tag equals <paramref name="accountId"/> — the
    /// <see cref="IbkrConduit.Diagnostics.IbkrConduitDiagnostics.ActivitySource"/> is process-global and
    /// tests run in parallel, so filtering by operation name alone would risk capturing another
    /// concurrently-running test's span with the same name.
    /// </summary>
    private static ActivityListener MakeListener(string accountId, Action<Activity> onCaptured)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "IbkrConduit",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == "IbkrConduit.Portfolio.GetPositions" &&
                    Equals(activity.GetTagItem("ibkr.account_id"), accountId))
                {
                    onCaptured(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await _harness.DisposeAsync();

    /// <inheritdoc />
    public void Dispose()
    {
        _harness.Dispose();
        GC.SuppressFinalize(this);
    }
}
