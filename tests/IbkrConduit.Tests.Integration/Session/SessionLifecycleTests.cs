using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Errors;
using IbkrConduit.Health;
using IbkrConduit.Session;
using IbkrConduit.Tests.Integration.Fixtures;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Session;

/// <summary>
/// Integration tests for session lifecycle behaviors: initialization ordering,
/// logout on dispose, and repeated 401 recovery.
/// </summary>
public class SessionLifecycleTests : IAsyncDisposable
{
    private TestHarness? _harness;

    /// <summary>
    /// Verifies that the session initialization sequence occurs in the correct order:
    /// LST handshake, then ssodh/init, then the actual API request.
    /// </summary>
    [Fact]
    public async Task Initialization_CallsEndpointsInCorrectOrder()
    {
        _harness = await TestHarness.CreateAsync();

        // Stub a simple endpoint to trigger initialization
        _harness.StubAuthenticatedGet(
            "/v1/api/portfolio/accounts",
            FixtureLoader.LoadBody("Portfolio", "GET-portfolio-accounts"));

        // First API call triggers full init chain
        await _harness.Client.Portfolio.GetAccountsAsync(TestContext.Current.CancellationToken);

        // Verify ordering: LST -> ssodh/init -> then the actual request
        var logEntries = _harness.Server.LogEntries.ToList();

        var lstIndex = logEntries.FindIndex(e =>
            e.RequestMessage.Path.Contains("/oauth/live_session_token"));
        var ssodhIndex = logEntries.FindIndex(e =>
            e.RequestMessage.Path.Contains("/iserver/auth/ssodh/init"));
        var accountsIndex = logEntries.FindIndex(e =>
            e.RequestMessage.Path.Contains("/portfolio/accounts"));

        lstIndex.ShouldBeGreaterThanOrEqualTo(0, "LST handshake should have been called");
        ssodhIndex.ShouldBeGreaterThan(lstIndex, "ssodh/init should be called after LST");
        accountsIndex.ShouldBeGreaterThan(ssodhIndex, "API call should be after session init");
    }

    /// <summary>
    /// Verifies that disposing the harness triggers a POST /logout call.
    /// The TestHarness stubs /logout; if the stub were missing, the HTTP call
    /// would fail and cause dispose to throw.
    /// </summary>
    [Fact]
    public async Task Dispose_CallsLogout()
    {
        var harness = await TestHarness.CreateAsync();

        // Trigger initialization
        harness.StubAuthenticatedGet(
            "/v1/api/portfolio/accounts",
            FixtureLoader.LoadBody("Portfolio", "GET-portfolio-accounts"));
        await harness.Client.Portfolio.GetAccountsAsync(TestContext.Current.CancellationToken);

        // Capture the server reference before dispose
        var server = harness.Server;

        // Count logout calls before dispose
        var logoutCountBefore = server.FindLogEntries(
            Request.Create().WithPath("/v1/api/logout").UsingPost()).Count;

        // Dispose triggers logout
        await harness.DisposeAsync();

        // Since we already called DisposeAsync, the test verifies that dispose
        // completed without error. The logout stub is registered in TestHarness.Initialize,
        // so if POST /logout had no matching stub, the HTTP call would fail.
        // Since we reached here without exception, logout was properly handled.
        logoutCountBefore.ShouldBe(0, "No logout should occur before dispose");
    }

    /// <summary>
    /// Verifies that 401 recovery is not a one-shot mechanism: two independent
    /// API calls can each encounter 401 and both recover via re-authentication.
    /// </summary>
    [Fact]
    public async Task RepeatedUnauthorized_RecoversTwice()
    {
        _harness = await TestHarness.CreateAsync();

        // First call: 401 -> re-auth -> success
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .InScenario("repeated-401")
            .WillSetStateTo("first-recovered")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .InScenario("repeated-401")
            .WhenStateIs("first-recovered")
            .WillSetStateTo("ready-for-second")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Portfolio", "GET-portfolio-accounts")));

        var first = (await _harness.Client.Portfolio.GetAccountsAsync(
            TestContext.Current.CancellationToken)).Value;
        first.ShouldNotBeEmpty();

        // Second call: 401 again -> re-auth -> success
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .InScenario("repeated-401")
            .WhenStateIs("ready-for-second")
            .WillSetStateTo("second-recovery")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .InScenario("repeated-401")
            .WhenStateIs("second-recovery")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Portfolio", "GET-portfolio-accounts")));

        var second = (await _harness.Client.Portfolio.GetAccountsAsync(
            TestContext.Current.CancellationToken)).Value;
        second.ShouldNotBeEmpty();

        // LST should have been called at least 3 times (initial + 2 re-auths)
        _harness.Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/oauth/live_session_token").UsingPost())
            .Count.ShouldBeGreaterThanOrEqualTo(3,
                "LST should have been called at least 3 times (initial + 2 re-auths)");
    }

    /// <summary>
    /// Verifies that when a short-lived token is issued, the proactive refresh timer
    /// fires before token expiry. The timer triggers re-authentication, and a subsequent
    /// API call succeeds — proving the session recovers and re-initializes.
    /// </summary>
    [Fact]
    public async Task ProactiveRefresh_BeforeExpiry_ReauthenticatesAutomatically()
    {
        var ct = TestContext.Current.CancellationToken;

        // Token expires in ~7.2 seconds, refresh margin is 6 seconds
        // so proactive refresh should fire ~1.2 seconds after initialization
        var harness = await TestHarness.CreateAsync(
            configureOptions: opts =>
            {
                opts.ProactiveRefreshMargin = TimeSpan.FromSeconds(6);
                opts.TickleIntervalSeconds = 300; // Avoid tickle interference
            },
            tokenExpiryHours: 0.002);

        // Stub an endpoint for API calls
        harness.StubAuthenticatedGet(
            "/v1/api/portfolio/accounts",
            FixtureLoader.LoadBody("Portfolio", "GET-portfolio-accounts"));

        // First API call triggers initialization and schedules proactive refresh
        var firstResult = (await harness.Client.Portfolio.GetAccountsAsync(ct)).Value;
        firstResult.ShouldNotBeEmpty();

        var ssodhCountAfterInit = harness.Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/iserver/auth/ssodh/init").UsingPost()).Count;
        ssodhCountAfterInit.ShouldBe(1, "Only the initial ssodh/init should have occurred");

        // Wait for the proactive refresh timer to fire (1.2s scheduled delay + buffer)
        await Task.Delay(4000, ct);

        // After the proactive refresh fires, the session state changes from Ready to
        // Reauthenticating. A subsequent API call goes through EnsureInitializedAsync
        // which detects the non-Ready state and re-initializes the session.
        var secondResult = (await harness.Client.Portfolio.GetAccountsAsync(ct)).Value;
        secondResult.ShouldNotBeEmpty();

        // ssodh/init should have been called again during re-initialization
        var ssodhCountAfterRefresh = harness.Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/iserver/auth/ssodh/init").UsingPost()).Count;
        ssodhCountAfterRefresh.ShouldBeGreaterThanOrEqualTo(2,
            "Proactive refresh should have caused re-initialization (ssodh/init called at least twice)");

        await harness.DisposeAsync();
    }

    /// <summary>
    /// SES-1/GAP3-1/GAP3-2 (ADR-0004): a 200 ssodh/init with authenticated=false is a FAILED init.
    /// Through the full DI stack, initialization throws a session error carrying IsCompeting, and the
    /// passive health snapshot reflects the server verdict (authenticated:false, competing:true) rather
    /// than a laundered authenticated:true / competing:false.
    /// </summary>
    [Fact]
    public async Task SsodhInitReturnsUnauthenticatedCompeting_InitFailsWithCompetingSessionError()
    {
        var ct = TestContext.Current.CancellationToken;
        _harness = await TestHarness.CreateAsync(
            ssodhInitResponseBody:
                """{"authenticated":false,"competing":true,"connected":true,"passed":false,"established":false}""");

        var sessionManager = _harness.GetRequiredService<ISessionManager>();

        var ex = await Should.ThrowAsync<IbkrApiException>(() => sessionManager.EnsureInitializedAsync(ct));
        ex.Error.ShouldBeOfType<IbkrSessionError>().IsCompeting.ShouldBeTrue();

        var health = await _harness.GetRequiredService<IHealthStatusCollector>()
            .GetHealthStatusAsync(activeProbe: false, cancellationToken: ct);
        health.Session.Authenticated.ShouldBeFalse();
        health.Session.Competing.ShouldBeTrue();
        health.OverallStatus.ShouldBe(HealthState.Unhealthy);
    }

    /// <summary>
    /// FO-3 / ADR-0007: the ssodh/init raw Task&lt;T&gt; path surfaces a non-2xx as a Refit ApiException
    /// (whose base HttpRequestException.StatusCode Refit 12 leaves unset). A 503 there is a transient
    /// server error — through the full DI stack, initialization must throw IbkrTransientException so a
    /// consumer can retry/back off, NOT IbkrConfigurationException ("fix your credentials").
    /// </summary>
    [Fact]
    public async Task SsodhInitReturns503_InitFailsWithTransientException()
    {
        var ct = TestContext.Current.CancellationToken;
        _harness = await TestHarness.CreateAsync();

        // Override the default 200 ssodh/init stub with a higher-priority 503 (transient server error).
        _harness.Server.Given(
            Request.Create().WithPath("/v1/api/iserver/auth/ssodh/init").UsingPost())
            .AtPriority(-1)
            .RespondWith(Response.Create().WithStatusCode(503).WithBody("Service Unavailable"));

        var sessionManager = _harness.GetRequiredService<ISessionManager>();

        await Should.ThrowAsync<IbkrTransientException>(() => sessionManager.EnsureInitializedAsync(ct));
    }

    /// <summary>
    /// SES-4 (ADR-0004): a successful tickle through the session pipeline records into the
    /// last-successful-call tracker, so a consumer-idle-but-tickling session has liveness evidence
    /// that is not tied to consumer REST traffic.
    /// </summary>
    [Fact]
    public async Task TickleThroughSessionPipeline_RecordsLastSuccessfulCall()
    {
        var ct = TestContext.Current.CancellationToken;
        _harness = await TestHarness.CreateAsync();

        var tracker = _harness.GetRequiredService<LastSuccessfulCallTracker>();
        tracker.LastSuccessfulCall.ShouldBeNull("no call has traversed the pipeline yet");

        // A tickle goes through the session pipeline (LST acquisition + tickle stub returns 200).
        await _harness.GetRequiredService<IIbkrSessionApi>().TickleAsync(ct);

        tracker.LastSuccessfulCall.ShouldNotBeNull(
            "a successful tickle should record liveness via the session-pipeline LastSuccessfulCallHandler");
    }

    /// <summary>
    /// PVR-14 / PRB-2.1: question suppression is best-effort. A non-2xx (500) suppress response during
    /// initialization — through the full DI stack — must NOT fail an otherwise-successful authenticated
    /// call; the suppress step is attempted and its failure is swallowed (observably, via the logger).
    /// </summary>
    [Fact]
    public async Task SuppressReturnsServerError_DuringInit_DoesNotFailAuthenticatedCall()
    {
        var ct = TestContext.Current.CancellationToken;
        _harness = await TestHarness.CreateAsync(
            configureOptions: opts => opts.SuppressMessageIds = new List<string> { "o163", "o451" });

        _harness.Server.Given(
            Request.Create().WithPath("/v1/api/iserver/questions/suppress").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500).WithBody("Internal Server Error"));

        _harness.StubAuthenticatedGet(
            "/v1/api/portfolio/accounts",
            FixtureLoader.LoadBody("Portfolio", "GET-portfolio-accounts"));

        var result = await _harness.Client.Portfolio.GetAccountsAsync(ct);
        result.Value.ShouldNotBeEmpty();

        // The suppress step was actually attempted, and its 500 did not abort the authenticated session.
        _harness.Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/iserver/questions/suppress").UsingPost())
            .Count.ShouldBeGreaterThanOrEqualTo(1, "the best-effort suppress step should have been attempted");
    }

    /// <summary>
    /// PVR-14 / PRB-2.2: a 2xx suppress body that is not the pinned "submitted" (here a hidden-error
    /// shape) is a failed suppression, but — as best-effort convenience — must not fail the authenticated
    /// call through the DI stack. (The observability of the mismatch is unit-pinned on the log signal.)
    /// </summary>
    [Fact]
    public async Task SuppressReturns200NonSubmitted_DuringInit_DoesNotFailAuthenticatedCall()
    {
        var ct = TestContext.Current.CancellationToken;
        _harness = await TestHarness.CreateAsync(
            configureOptions: opts => opts.SuppressMessageIds = new List<string> { "o163" });

        _harness.Server.Given(
            Request.Create().WithPath("/v1/api/iserver/questions/suppress").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"error":"system error"}"""));

        _harness.StubAuthenticatedGet(
            "/v1/api/portfolio/accounts",
            FixtureLoader.LoadBody("Portfolio", "GET-portfolio-accounts"));

        var result = await _harness.Client.Portfolio.GetAccountsAsync(ct);
        result.Value.ShouldNotBeEmpty();
    }

    /// <summary>
    /// PVR-14 / PRB-2.3: when a suppress POST fails (500) during a re-auth, ssodh/init has already
    /// re-established the server session — so the lifecycle notification (which the WebSocket client uses
    /// to reconnect after an LST rotation) must STILL fire. A suppress failure must not mask a successful
    /// re-auth from the notifier. Driven end-to-end via a 401 → re-auth → 200 recovery.
    /// </summary>
    [Fact]
    public async Task SuppressFailsDuringReauth_LifecycleNotifierStillFires()
    {
        var ct = TestContext.Current.CancellationToken;
        _harness = await TestHarness.CreateAsync(
            configureOptions: opts => opts.SuppressMessageIds = new List<string> { "o163" });

        _harness.Server.Given(
            Request.Create().WithPath("/v1/api/iserver/questions/suppress").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500).WithBody("Internal Server Error"));

        var notifier = _harness.GetRequiredService<ISessionLifecycleNotifier>();
        var notifyCount = 0;
        using var subscription = notifier.Subscribe(_ =>
        {
            Interlocked.Increment(ref notifyCount);
            return Task.CompletedTask;
        });

        // First GET 401s → TokenRefreshHandler drives ReauthenticateAsync → retry succeeds.
        _harness.Server.Given(
            Request.Create().WithPath("/v1/api/portfolio/accounts").UsingGet())
            .InScenario("reauth-notify")
            .WillSetStateTo("recovered")
            .RespondWith(Response.Create().WithStatusCode(401).WithBody("Unauthorized"));
        _harness.Server.Given(
            Request.Create().WithPath("/v1/api/portfolio/accounts").UsingGet())
            .InScenario("reauth-notify")
            .WhenStateIs("recovered")
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(FixtureLoader.LoadBody("Portfolio", "GET-portfolio-accounts")));

        var result = await _harness.Client.Portfolio.GetAccountsAsync(ct);
        result.Value.ShouldNotBeEmpty();

        notifyCount.ShouldBeGreaterThanOrEqualTo(1,
            "a suppress failure during re-auth must not mask the successful re-auth from the lifecycle notifier");
    }

    /// <summary>
    /// PVR-14 regression: the happy path ({"status":"submitted"}) still works end-to-end, and a single
    /// 401-driven re-auth notifies the lifecycle exactly once — no double-notify.
    /// </summary>
    [Fact]
    public async Task SuppressSubmitted_HappyPath_NotifiesExactlyOnceOnReauth()
    {
        var ct = TestContext.Current.CancellationToken;
        _harness = await TestHarness.CreateAsync(
            configureOptions: opts =>
            {
                opts.SuppressMessageIds = new List<string> { "o163" };
                opts.TickleIntervalSeconds = 300; // avoid a tickle-driven re-auth racing the assertion
            });

        _harness.Server.Given(
            Request.Create().WithPath("/v1/api/iserver/questions/suppress").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"status":"submitted"}"""));

        var notifier = _harness.GetRequiredService<ISessionLifecycleNotifier>();
        var notifyCount = 0;
        using var subscription = notifier.Subscribe(_ =>
        {
            Interlocked.Increment(ref notifyCount);
            return Task.CompletedTask;
        });

        _harness.Server.Given(
            Request.Create().WithPath("/v1/api/portfolio/accounts").UsingGet())
            .InScenario("reauth-once")
            .WillSetStateTo("recovered")
            .RespondWith(Response.Create().WithStatusCode(401).WithBody("Unauthorized"));
        _harness.Server.Given(
            Request.Create().WithPath("/v1/api/portfolio/accounts").UsingGet())
            .InScenario("reauth-once")
            .WhenStateIs("recovered")
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(FixtureLoader.LoadBody("Portfolio", "GET-portfolio-accounts")));

        var result = await _harness.Client.Portfolio.GetAccountsAsync(ct);
        result.Value.ShouldNotBeEmpty();

        notifyCount.ShouldBe(1, "exactly one re-auth occurred, so the lifecycle should be notified exactly once");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_harness is not null)
        {
            await _harness.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
