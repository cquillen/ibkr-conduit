using System;
using System.Diagnostics;
using System.Threading.Tasks;
using IbkrConduit.Tests.Integration.Fixtures;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Orders;

/// <summary>
/// RPD-06 (ADR-0009, spec: docs/superpowers/specs/2026-07-14-rpd-06-cold-read-retry.md): WireMock
/// call-count-sequenced coverage for <c>GetTradesAsync</c>'s internal retry-once-on-empty-first-read.
/// Each test passes a distinct <c>days</c> value so the process-wide <see cref="ActivitySource"/>
/// (shared with every other concurrently-running test class) cannot cross-contaminate a captured
/// <see cref="Activity"/> — the capture filters on the <c>days</c> tag, not just the operation name.
/// </summary>
public class OrderColdReadRetryTests : IAsyncLifetime, IDisposable
{
    private const string _tradesPath = "/v1/api/iserver/account/trades";
    private TestHarness _harness = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = await TestHarness.CreateAsync();
    }

    [Fact]
    public async Task GetTradesAsync_SparseFirstRead_RetriesOnceAndReturnsPopulatedData()
    {
        const int days = 90001;

        _harness.Server.Given(
            Request.Create().WithPath(_tradesPath).UsingGet())
            .InScenario("coldread-trades-retry")
            .WillSetStateTo("populated")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Orders", "GET-trades-empty")));

        _harness.Server.Given(
            Request.Create().WithPath(_tradesPath).UsingGet())
            .InScenario("coldread-trades-retry")
            .WhenStateIs("populated")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Orders", "GET-coldread-trades-populated")));

        Activity? captured = null;
        using (var listener = MakeListener(days, a => captured = a))
        {
            var trades = (await _harness.Client.Orders.GetTradesAsync(
                days, cancellationToken: TestContext.Current.CancellationToken)).Value;

            trades.Count.ShouldBe(2);
        }

        _harness.Server.FindLogEntries(Request.Create().WithPath(_tradesPath).UsingGet())
            .Count.ShouldBe(2, "the empty first read should trigger exactly one cold-read retry");

        captured.ShouldNotBeNull();
        captured!.GetTagItem("ibkr.cold_read_retry").ShouldBe(true);
    }

    [Fact]
    public async Task GetTradesAsync_NonEmptyFirstRead_DoesNotRetry()
    {
        const int days = 90002;

        _harness.StubAuthenticatedGet(_tradesPath,
            FixtureLoader.LoadBody("Orders", "GET-trades"));

        Activity? captured = null;
        using (var listener = MakeListener(days, a => captured = a))
        {
            var trades = (await _harness.Client.Orders.GetTradesAsync(
                days, cancellationToken: TestContext.Current.CancellationToken)).Value;

            trades.Count.ShouldBe(1);
        }

        _harness.Server.FindLogEntries(Request.Create().WithPath(_tradesPath).UsingGet())
            .Count.ShouldBe(1, "a non-empty first read must not trigger a retry");

        captured.ShouldNotBeNull();
        captured!.GetTagItem("ibkr.cold_read_retry").ShouldBeNull();
    }

    [Fact]
    public async Task GetTradesAsync_EmptyRetryStillEmpty_CapsAtOneRetry()
    {
        const int days = 90003;

        // Both calls return empty — a genuinely quiet trading day whose retry is still empty. The
        // retry must be capped at one attempt, never a third call (ADR-0009's bounded false-positive
        // cost).
        _harness.StubAuthenticatedGet(_tradesPath,
            FixtureLoader.LoadBody("Orders", "GET-trades-empty"));

        var trades = (await _harness.Client.Orders.GetTradesAsync(
            days, cancellationToken: TestContext.Current.CancellationToken)).Value;

        trades.ShouldBeEmpty();

        _harness.Server.FindLogEntries(Request.Create().WithPath(_tradesPath).UsingGet())
            .Count.ShouldBe(2, "a still-empty retry must not trigger a second retry (capped at one)");
    }

    [Fact]
    public async Task GetTradesAsync_EmptyFirstReadRetryFails_ReturnsFirstSuccessfulResult()
    {
        const int days = 90005;

        // Call 1: empty-but-successful (a genuinely trade-free day). Call 2 (the cold-read retry): a
        // transient 500. ADR-0009 Decision point 4 — a false-positive retry must never corrupt data or
        // change the result the consumer ultimately sees, so the good first read must win, not the
        // failed retry.
        _harness.Server.Given(
            Request.Create().WithPath(_tradesPath).UsingGet())
            .InScenario("coldread-trades-retry-fails")
            .WillSetStateTo("retry-failed")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Orders", "GET-trades-empty")));

        _harness.Server.Given(
            Request.Create().WithPath(_tradesPath).UsingGet())
            .InScenario("coldread-trades-retry-fails")
            .WhenStateIs("retry-failed")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(500)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"error":"Internal Server Error"}"""));

        var result = await _harness.Client.Orders.GetTradesAsync(
            days, cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue("a failed retry must not discard the good first read");
        result.Value.ShouldBeEmpty();

        _harness.Server.FindLogEntries(Request.Create().WithPath(_tradesPath).UsingGet())
            .Count.ShouldBe(2, "the empty first read still triggers exactly one cold-read retry attempt");
    }

    [Fact]
    public async Task GetTradesAsync_401ThenEmptyReplay_ComposesRetryWithReauth()
    {
        const int days = 90004;

        // Call 1: 401 (triggers TokenRefreshHandler re-auth). Call 2: the replayed request, empty.
        // Call 3: the cold-read retry issued on top of the replayed empty result, populated.
        _harness.Server.Given(
            Request.Create().WithPath(_tradesPath).UsingGet())
            .InScenario("coldread-trades-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create().WithPath(_tradesPath).UsingGet())
            .InScenario("coldread-trades-401")
            .WhenStateIs("token-expired")
            .WillSetStateTo("retried")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Orders", "GET-trades-empty")));

        _harness.Server.Given(
            Request.Create().WithPath(_tradesPath).UsingGet())
            .InScenario("coldread-trades-401")
            .WhenStateIs("retried")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Orders", "GET-coldread-trades-populated")));

        Activity? captured = null;
        var captureCount = 0;
        using (var listener = MakeListener(days, a =>
        {
            captured = a;
            captureCount++;
        }))
        {
            var trades = (await _harness.Client.Orders.GetTradesAsync(
                days, cancellationToken: TestContext.Current.CancellationToken)).Value;

            trades.Count.ShouldBe(2);
        }

        _harness.Server.FindLogEntries(Request.Create().WithPath(_tradesPath).UsingGet())
            .Count.ShouldBe(3, "401 + replayed-empty + cold-read-retry-populated = 3 hits on the trades path");
        _harness.VerifyReauthenticationOccurred();

        captureCount.ShouldBe(1, "exactly one GetTrades span for the whole call, 401 replay stays inside it");
        captured.ShouldNotBeNull();
        captured!.GetTagItem("ibkr.cold_read_retry").ShouldBe(true);
    }

    /// <summary>
    /// A tightly-scoped <see cref="ActivityListener"/> that only reports spans whose <c>days</c> tag
    /// equals <paramref name="days"/> — the
    /// <see cref="IbkrConduit.Diagnostics.IbkrConduitDiagnostics.ActivitySource"/> is process-global
    /// and tests run in parallel, so filtering by operation name alone would risk capturing another
    /// concurrently-running test's span with the same name.
    /// </summary>
    private static ActivityListener MakeListener(int days, Action<Activity> onCaptured)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "IbkrConduit",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == "IbkrConduit.Order.GetTrades" &&
                    Equals(activity.GetTagItem("days"), days))
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
