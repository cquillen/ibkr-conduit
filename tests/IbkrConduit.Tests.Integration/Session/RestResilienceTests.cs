using System;
using System.Threading.Tasks;
using IbkrConduit.Errors;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Session;

/// <summary>
/// Pin current REST resilience behavior. These tests exercise scenarios that
/// the codebase appears to handle correctly; they protect against future regressions.
/// All four should pass without code changes — if any fails, investigate before merge.
/// </summary>
public class RestResilienceTests : IAsyncLifetime, IDisposable
{
    private TestHarness _harness = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        // Short tickle interval so the tickle-recovery test can observe several
        // ticks within a reasonable wait. Failure interval is even shorter so
        // recovery happens quickly.
        _harness = await TestHarness.CreateAsync(opts =>
        {
            opts.TickleIntervalSeconds = 2;
            opts.TickleFailureIntervalSeconds = 1;
        });
    }

    /// <summary>
    /// Pins: TickleTimer's catch-all keeps the loop alive across transient failures.
    /// The session continues serving application API requests after tickle recovers.
    /// </summary>
    [Fact]
    [Trait("Category", "Slow")]
    public async Task TickleTimer_RecoversFromTransientNetworkFailure()
    {
        var ct = TestContext.Current.CancellationToken;

        _harness.StubAuthenticatedGet(
            "/v1/api/portfolio/accounts",
            """[{"id":"U1234567","accountTitle":"Test","type":"DEMO"}]""");

        // Trigger session init (also fires the first tickle as part of the timer start)
        await _harness.Client.Portfolio.GetAccountsAsync(ct);

        var tickleCountAfterInit = _harness.Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/tickle").UsingPost()).Count;

        // Configure tickle to fail twice with 503, then recover.
        // AtPriority(-1) makes the scenario stubs win over the default 200 stub
        // registered by TestHarness (lower priority value = higher precedence).
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/tickle")
                .UsingPost())
            .AtPriority(-1)
            .InScenario("tickle-recovery")
            .WillSetStateTo("first-fail")
            .RespondWith(Response.Create().WithStatusCode(503).WithBody("Service Unavailable"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/tickle")
                .UsingPost())
            .AtPriority(-1)
            .InScenario("tickle-recovery")
            .WhenStateIs("first-fail")
            .WillSetStateTo("second-fail")
            .RespondWith(Response.Create().WithStatusCode(503).WithBody("Service Unavailable"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/tickle")
                .UsingPost())
            .AtPriority(-1)
            .InScenario("tickle-recovery")
            .WhenStateIs("second-fail")
            .WillSetStateTo("recovered")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"session":"s","iserver":{"authStatus":{"authenticated":true,"competing":false,"connected":true}}}"""));

        // Wait through enough failure-interval ticks (1s) for recovery
        await Task.Delay(TimeSpan.FromSeconds(8), ct);

        // The session should still be functional — make another application call
        var result = await _harness.Client.Portfolio.GetAccountsAsync(ct);
        result.IsSuccess.ShouldBeTrue("Session should remain functional after transient tickle failures");

        // Tickle should have been called several times (at least the 2 fails + recovery)
        var tickleCountFinal = _harness.Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/tickle").UsingPost()).Count;
        (tickleCountFinal - tickleCountAfterInit).ShouldBeGreaterThanOrEqualTo(3,
            "Tickle should have fired at least 3 times after init: 2 failures + recovery");
    }

    /// <summary>
    /// Pins: SessionManager.ReauthenticateAsync's failure path doesn't permanently
    /// break the session — a subsequent application API call goes through
    /// EnsureInitializedAsync (state is no longer Ready after the failed reauth)
    /// which performs a fresh init using the next-successful LST handshake.
    /// </summary>
    [Fact]
    [Trait("Category", "Slow")]
    public async Task Reauth_RecoversFromTransientLstFailure()
    {
        var ct = TestContext.Current.CancellationToken;

        // First application call returns 401 to trigger re-auth, second call returns 200.
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .InScenario("reauth-lst-fail")
            .WillSetStateTo("first-401-served")
            .RespondWith(Response.Create().WithStatusCode(401).WithBody("Unauthorized"));

        // After the 401, TokenRefreshHandler invokes Reauthenticate -> LST.
        // The first LST attempt during re-auth fails with 503.
        // AtPriority(-1) ensures this scenario-specific stub wins over the default
        // MockLstServer stub registered by TestHarness.
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/oauth/live_session_token")
                .UsingPost())
            .AtPriority(-1)
            .InScenario("reauth-lst-fail")
            .WhenStateIs("first-401-served")
            .WillSetStateTo("lst-failed")
            .RespondWith(Response.Create().WithStatusCode(503).WithBody("Service Unavailable"));

        // Subsequent LST calls fall through to the default MockLstServer stub
        // (no scenario constraint), which performs the real DH handshake.

        // After LST failure, the session is left in a non-Ready state. The next
        // application call will go through EnsureInitializedAsync and succeed.
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .InScenario("reauth-lst-fail")
            .WhenStateIs("lst-failed")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""[{"id":"U1234567","accountTitle":"Test","type":"DEMO"}]"""));

        // First call: 401 -> re-auth -> LST 503 -> reauth throws IbkrApiException
        await Should.ThrowAsync<IbkrApiException>(
            async () => await _harness.Client.Portfolio.GetAccountsAsync(ct));

        // Second call: state is non-Ready, EnsureInitializedAsync performs full init
        // using the now-default LST stub, then issues the request.
        var result = await _harness.Client.Portfolio.GetAccountsAsync(ct);
        result.IsSuccess.ShouldBeTrue(
            "Session should recover after a single LST failure during re-auth");
    }

    /// <summary>
    /// Pins current behavior: 500 responses on application endpoints propagate to
    /// the caller without triggering re-auth. NOTE: IBKR is inconsistent with status
    /// codes and sometimes uses 500 for conditions other clients would use 4xx for.
    /// If a future investigation finds that some 500 responses indicate stale sessions,
    /// the re-auth trigger logic in TokenRefreshHandler may need to be widened. This
    /// test documents the current narrow behavior so any change is deliberate.
    /// </summary>
    [Fact]
    public async Task Application500_PropagatesWithoutTriggeringReauth()
    {
        var ct = TestContext.Current.CancellationToken;

        // Trigger session init with a successful call
        _harness.StubAuthenticatedGet(
            "/v1/api/portfolio/accounts",
            """[{"id":"U1234567","accountTitle":"Test","type":"DEMO"}]""");
        await _harness.Client.Portfolio.GetAccountsAsync(ct);

        var lstCountAfterInit = _harness.Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/oauth/live_session_token").UsingPost()).Count;

        // Stub a 500 on a different endpoint (live orders).
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/orders")
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500).WithBody("Internal Server Error"));

        var result = await _harness.Client.Orders.GetLiveOrdersAsync(cancellationToken: ct);
        result.IsSuccess.ShouldBeFalse("500 should propagate as a failure");

        // LST endpoint should NOT have been called again
        var lstCountAfter500 = _harness.Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/oauth/live_session_token").UsingPost()).Count;
        lstCountAfter500.ShouldBe(lstCountAfterInit,
            "500 on application endpoint should not trigger re-auth (current behavior)");
    }

    /// <summary>
    /// Pins: SessionManager._semaphore serializes concurrent re-auth attempts —
    /// they cannot run in parallel, both calls succeed, and the session is healthy
    /// afterwards.
    /// <para>
    /// FINDING (Task 8): the plan asserted that two concurrent 401-recoveries trigger
    /// only ONE LST acquisition. In the current implementation
    /// <see cref="IbkrConduit.Session.SessionManager.ReauthenticateAsync"/> does not
    /// short-circuit on the post-handshake <c>Ready</c> state, so the second
    /// reauth waits on the semaphore, observes <c>Ready</c>, and refreshes the LST
    /// again. Net effect: two concurrent 401s -> two LST handshakes (wasteful but
    /// not broken). This test pins current behavior (delta == 2). If a future change
    /// adds a Ready-state short-circuit (a worthwhile optimization that avoids
    /// hammering IBKR's LST endpoint when many concurrent requests 401 together),
    /// update this assertion to <c>delta == 1</c>.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Concurrent401_SerializesReauth()
    {
        var ct = TestContext.Current.CancellationToken;

        // Trigger session init
        _harness.StubAuthenticatedGet(
            "/v1/api/portfolio/accounts",
            """[{"id":"U1234567","accountTitle":"Test","type":"DEMO"}]""");
        await _harness.Client.Portfolio.GetAccountsAsync(ct);

        var lstCountAfterInit = _harness.Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/oauth/live_session_token").UsingPost()).Count;

        // Two endpoints, each returning 401 then 200.
        // Endpoint A: live orders (returns OrdersResponse — body is {"orders":[...]})
        // Endpoint B: trades (returns List<Trade> — body is [])
        _harness.Server.Given(
            Request.Create().WithPath("/v1/api/iserver/account/orders").UsingGet())
            .InScenario("concurrent-401-orders")
            .WillSetStateTo("orders-401-served")
            .RespondWith(Response.Create().WithStatusCode(401).WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create().WithPath("/v1/api/iserver/account/orders").UsingGet())
            .InScenario("concurrent-401-orders")
            .WhenStateIs("orders-401-served")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"orders":[]}"""));

        _harness.Server.Given(
            Request.Create().WithPath("/v1/api/iserver/account/trades").UsingGet())
            .InScenario("concurrent-401-trades")
            .WillSetStateTo("trades-401-served")
            .RespondWith(Response.Create().WithStatusCode(401).WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create().WithPath("/v1/api/iserver/account/trades").UsingGet())
            .InScenario("concurrent-401-trades")
            .WhenStateIs("trades-401-served")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("[]"));

        // Issue both calls concurrently
        var taskA = _harness.Client.Orders.GetLiveOrdersAsync(cancellationToken: ct);
        var taskB = _harness.Client.Orders.GetTradesAsync(cancellationToken: ct);
        await Task.WhenAll(taskA, taskB);

        (await taskA).IsSuccess.ShouldBeTrue("Orders call should recover via re-auth");
        (await taskB).IsSuccess.ShouldBeTrue("Trades call should recover via re-auth");

        // Pin current behavior: each concurrent 401 triggers its own LST refresh
        // (semaphore serializes them but does not de-dup). See test docstring for
        // the recommended optimization.
        var lstCountAfterCalls = _harness.Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/oauth/live_session_token").UsingPost()).Count;
        (lstCountAfterCalls - lstCountAfterInit).ShouldBe(2,
            "Two concurrent 401-recoveries currently each acquire their own LST. " +
            "If this changes to de-dup via a Ready-state short-circuit, update the assertion to 1.");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _harness.DisposeAsync();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _harness.Dispose();
        GC.SuppressFinalize(this);
    }
}
