using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using IbkrConduit.Tests.Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
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
        // The portfolio positions path isn't endpoint-rate-limited today, so this override is a
        // no-op in practice — applied anyway (mirroring OrderTests/ScannerTests) to keep this class
        // robust to future limiter additions on the positions path.
        _harness = await TestHarness.CreateAsync(configureServices: services =>
        {
            services.AddSingleton<IReadOnlyDictionary<string, RateLimiter>>(
                new Dictionary<string, RateLimiter>());
        });
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
    public async Task GetPositionsAsync_SparseFirstReadRetryFails_ReturnsFirstSuccessfulResult()
    {
        const string accountId = "U9990005";
        var path = $"/v1/api/portfolio/{accountId}/positions/0";

        // Call 1: sparse-but-successful (a legitimately-sparse thin account, e.g. a cash/forex
        // position with no ticker). Call 2 (the cold-read retry): a transient 500. ADR-0009 Decision
        // point 4 — a false-positive retry must never corrupt data or change the result the consumer
        // ultimately sees, so the good first read must win, not the failed retry.
        _harness.Server.Given(
            Request.Create().WithPath(path).UsingGet())
            .InScenario("coldread-positions-retry-fails")
            .WillSetStateTo("retry-failed")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Portfolio", "GET-coldread-positions-sparse")));

        _harness.Server.Given(
            Request.Create().WithPath(path).UsingGet())
            .InScenario("coldread-positions-retry-fails")
            .WhenStateIs("retry-failed")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(500)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"error":"Internal Server Error"}"""));

        var result = await _harness.Client.Portfolio.GetPositionsAsync(
            accountId, 0, cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue("a failed retry must not discard the good first read");
        result.Value.Count.ShouldBe(2);
        result.Value[0].Name.ShouldBeNullOrEmpty();
        result.Value[0].Ticker.ShouldBeNullOrEmpty();

        _harness.Server.FindLogEntries(Request.Create().WithPath(path).UsingGet())
            .Count.ShouldBe(2, "the sparse first read still triggers exactly one cold-read retry attempt");
    }

    [Fact]
    public async Task GetPositionsAsync_SparseFirstReadRetryThrowsTransportFault_ReturnsFirstSuccessfulResult()
    {
        // Unlike an HTTP-status retry failure (covered above), a genuine pre-response transport fault
        // (connection reset, DNS failure, a real client-side timeout) makes ResultFactory.FromResponse
        // itself throw (via ThrowOnSendFailure, ADR-0009 point 4a names "timeout" explicitly) rather
        // than returning a Result.Failure — this must be caught too, not just a captured non-2xx.
        // A DelegatingHandler fault-injects a thrown HttpRequestException on the 2nd call to this path
        // (the retry) — WireMock's own fault simulators (EMPTY_RESPONSE, MALFORMED_RESPONSE_CHUNK) were
        // confirmed not to reach this code path; they resolve to a non-throwing Result.Failure instead.
        const string accountId = "U9990006";
        var path = $"/v1/api/portfolio/{accountId}/positions/0";

        await using var faultHarness = await TestHarness.CreateAsync(configureServices: services =>
        {
            services.AddSingleton<IReadOnlyDictionary<string, RateLimiter>>(new Dictionary<string, RateLimiter>());
            services.ConfigureHttpClientDefaults(builder =>
                builder.AddHttpMessageHandler(() => new FaultOnNthCallHandler(path, faultOnCall: 2)));
        });

        faultHarness.StubAuthenticatedGet(path, FixtureLoader.LoadBody("Portfolio", "GET-coldread-positions-sparse"));

        var result = await faultHarness.Client.Portfolio.GetPositionsAsync(
            accountId, 0, cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue("a retry transport fault must not discard the good first read");
        result.Value.Count.ShouldBe(2);
        result.Value[0].Name.ShouldBeNullOrEmpty();
        result.Value[0].Ticker.ShouldBeNullOrEmpty();

        faultHarness.Server.FindLogEntries(Request.Create().WithPath(path).UsingGet())
            .Count.ShouldBe(1, "the retry's request never receives a WireMock response — it faults in the handler before reaching the server");
    }

    /// <summary>
    /// Throws a thrown (not HTTP-status) transport-level exception on the Nth request to a specific
    /// path — simulates a genuine pre-response fault (connection reset, timeout) that WireMock's own
    /// fault simulators don't reach, since this handler intercepts before the request ever leaves the
    /// process.
    /// </summary>
    private sealed class FaultOnNthCallHandler(string path, int faultOnCall) : System.Net.Http.DelegatingHandler
    {
        private int _count;

        protected override async Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath == path && System.Threading.Interlocked.Increment(ref _count) == faultOnCall)
            {
                throw new System.Net.Http.HttpRequestException("Simulated transport fault (RPD-06 retry-failure coverage)");
            }

            return await base.SendAsync(request, cancellationToken);
        }
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
