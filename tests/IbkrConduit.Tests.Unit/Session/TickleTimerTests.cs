using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Diagnostics;
using IbkrConduit.Errors;
using IbkrConduit.Health;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Refit;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Session;

public class TickleTimerTests
{
    /// <summary>
    /// Builds a Refit <see cref="ApiException"/> for the given status code, exactly as Refit
    /// surfaces a non-success HTTP response from a raw <c>Task&lt;T&gt;</c> interface method.
    /// </summary>
    private static async Task<ApiException> CreateTickleApiException(HttpStatusCode statusCode)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.ibkr.com/v1/api/tickle");
        using var response = new HttpResponseMessage(statusCode);
        return await ApiException.Create(request, HttpMethod.Post, response, new RefitSettings());
    }

    [Fact]
    public async Task StartAsync_CallsTickleOnInterval()
    {
        var sessionApi = new FakeSessionApi();
        var fakeTime = new FakeTimeProvider();
        var failureCount = 0;
        Func<CancellationToken, Task> onFailure = _ =>
        {
            Interlocked.Increment(ref failureCount);
            return Task.CompletedTask;
        };

        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);
        var timer = new TickleTimer(
            sessionApi,
            onFailure,
            new SessionHealthState(),
            NullLogger<TickleTimer>.Instance,
            notifier,
            new TenantContext("test"),
            healthyIntervalSeconds: 1,
            failureIntervalSeconds: 1,
            fakeTime);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await timer.StartAsync(cts.Token);

        // Pump: advance 1s at a time until 2 ticks are observed.
        // If RunAsync hasn't re-registered its next timer yet when we advance,
        // the loop advances again on the next iteration — reliable without real delays.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (sessionApi.TickleCallCount < 2 && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        await timer.StopAsync();

        sessionApi.TickleCallCount.ShouldBeGreaterThanOrEqualTo(2);
        failureCount.ShouldBe(0);
    }

    private static async Task WaitForTickleCount(FakeSessionApi sessionApi, int expectedCount, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (sessionApi.TickleCallCount < expectedCount)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Expected {expectedCount} ticks but only saw {sessionApi.TickleCallCount}");
            }
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }

    [Fact]
    public async Task StartAsync_WhenTickleReturnsUnauthenticated_InvokesFailureCallback()
    {
        var sessionApi = new FakeSessionApi { Authenticated = false };
        var fakeTime = new FakeTimeProvider();
        var failureTcs = new TaskCompletionSource();
        Func<CancellationToken, Task> onFailure = _ =>
        {
            failureTcs.TrySetResult();
            return Task.CompletedTask;
        };

        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);
        var timer = new TickleTimer(
            sessionApi,
            onFailure,
            new SessionHealthState(),
            NullLogger<TickleTimer>.Instance,
            notifier,
            new TenantContext("test"),
            healthyIntervalSeconds: 1,
            failureIntervalSeconds: 1,
            fakeTime);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await timer.StartAsync(cts.Token);

        fakeTime.Advance(TimeSpan.FromSeconds(1));
        await failureTcs.Task.WaitAsync(TestContext.Current.CancellationToken);

        failureTcs.Task.IsCompletedSuccessfully.ShouldBeTrue("Failure callback should have been invoked");
        await timer.StopAsync();
    }

    [Fact]
    public async Task StartAsync_WhenTickleThrows_DoesNotInvokeFailureCallback()
    {
        // Per IBKR's behavior, session-dead is signalled by 401 or
        // response-body authenticated=false — NEVER by transport-level
        // failures (5xx, network errors, timeouts). Reauthing on those
        // is pure waste because reauth needs the same network. Only the
        // !isAuthenticated branch (covered by the test above) should
        // trigger _onFailure.
        var sessionApi = new FakeSessionApi { ShouldThrow = true };
        var fakeTime = new FakeTimeProvider();
        var failureCount = 0;
        Func<CancellationToken, Task> onFailure = _ =>
        {
            Interlocked.Increment(ref failureCount);
            return Task.CompletedTask;
        };

        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);
        var timer = new TickleTimer(
            sessionApi,
            onFailure,
            new SessionHealthState(),
            NullLogger<TickleTimer>.Instance,
            notifier,
            new TenantContext("test"),
            healthyIntervalSeconds: 1,
            failureIntervalSeconds: 1,
            fakeTime);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await timer.StartAsync(cts.Token);

        // Advance until the throwing tickle has been observed at least once.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (sessionApi.TickleCallCount < 1 && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }
        sessionApi.TickleCallCount.ShouldBeGreaterThanOrEqualTo(1,
            "Tickle should have been attempted at least once");

        await timer.StopAsync();

        failureCount.ShouldBe(0,
            "Transport-level tickle failures must NOT invoke the failure callback — they are not session-dead signals.");
    }

    [Fact]
    public async Task RunAsync_FailureBurst_DoesNotInvokeFailureCallback()
    {
        // Issue #168 scenario: 12 consecutive transport-level tickle failures
        // produced 12 reauth attempts under the old code (a thundering herd of
        // pointless LST handshakes during a network outage). Under the new
        // contract: 0 callback invocations regardless of burst length.
        var sessionApi = new FakeSessionApi { ShouldThrow = true };
        var fakeTime = new FakeTimeProvider();
        var failureCount = 0;
        Func<CancellationToken, Task> onFailure = _ =>
        {
            Interlocked.Increment(ref failureCount);
            return Task.CompletedTask;
        };

        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);
        var timer = new TickleTimer(
            sessionApi,
            onFailure,
            new SessionHealthState(),
            NullLogger<TickleTimer>.Instance,
            notifier,
            new TenantContext("test"),
            healthyIntervalSeconds: 1,
            failureIntervalSeconds: 1,
            fakeTime);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await timer.StartAsync(cts.Token);

        // Pump the clock until at least 12 throwing tickles have been observed.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (sessionApi.TickleCallCount < 12 && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        await timer.StopAsync();

        sessionApi.TickleCallCount.ShouldBeGreaterThanOrEqualTo(12,
            "Test setup: pump should have driven at least 12 throwing tickles.");
        failureCount.ShouldBe(0,
            "A burst of transport-level failures must not fire reauth callbacks (issue #168).");
    }

    [Fact]
    public async Task RunAsync_TickleReturns401_TriggersReauthAndMarksSessionUnauthenticated()
    {
        // SES-2: an HTTP 401 tickle is a session-dead signal (the server rejected the
        // LST signature), NOT a transport failure. Refit surfaces it as an ApiException
        // with StatusCode==401 (distinct from the ApiRequestException that wraps transport
        // faults). The tickle loop must (a) invoke the failure callback to re-authenticate
        // and (b) reflect the server's verdict in health (authenticated=false).
        var apiException = await CreateTickleApiException(HttpStatusCode.Unauthorized);
        var sessionApi = new FakeSessionApi { TickleException = apiException };
        var fakeTime = new FakeTimeProvider();
        var failureTcs = new TaskCompletionSource();
        Func<CancellationToken, Task> onFailure = _ =>
        {
            failureTcs.TrySetResult();
            return Task.CompletedTask;
        };

        // Seed health as a live session so the 401's flip to unauthenticated is observable.
        var healthState = new SessionHealthState();
        healthState.Update(authenticated: true, connected: true, competing: false, established: true);

        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);
        var timer = new TickleTimer(
            sessionApi,
            onFailure,
            healthState,
            NullLogger<TickleTimer>.Instance,
            notifier,
            new TenantContext("test"),
            healthyIntervalSeconds: 1,
            failureIntervalSeconds: 1,
            fakeTime);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await timer.StartAsync(cts.Token);

        // Advance until the 401 tickle has fired and the failure callback ran.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!failureTcs.Task.IsCompleted && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        await failureTcs.Task.WaitAsync(TestContext.Current.CancellationToken);
        await timer.StopAsync();

        failureTcs.Task.IsCompletedSuccessfully.ShouldBeTrue(
            "A 401 tickle must invoke the reauth failure callback.");
        healthState.Authenticated.ShouldBeFalse(
            "A 401 tickle must mark the session unauthenticated in health state.");
    }

    [Fact]
    public async Task RunAsync_ReauthCallbackThrowsIn401Branch_LoopKeepsTickingAtFailureCadence()
    {
        // SES-1 (PVR-12): the 401 branch awaits the reauth callback (_onFailure). A reauth failure
        // thrown from there — a transient blip during recovery — must NOT escape and permanently kill
        // the keepalive loop; one failed reauth would then rot the session forever. The loop must
        // catch the throw, log it, and keep ticking at the failure cadence so the next cycle retries
        // reauth. Under the pre-fix code the throw escaped the enclosing catch and faulted RunAsync,
        // so the callback fired exactly once and the loop died.
        var apiException = await CreateTickleApiException(HttpStatusCode.Unauthorized);
        var sessionApi = new FakeSessionApi { TickleException = apiException };
        var fakeTime = new FakeTimeProvider();

        var failureCallbackCount = 0;
        Func<CancellationToken, Task> onFailure = _ =>
        {
            Interlocked.Increment(ref failureCallbackCount);
            // Simulate a reauth attempt failing (as WrapCredentialException surfaces a transient blip).
            throw new IbkrTransientException("simulated reauth failure during recovery");
        };

        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);
        var timer = new TickleTimer(
            sessionApi,
            onFailure,
            new SessionHealthState(),
            NullLogger<TickleTimer>.Instance,
            notifier,
            new TenantContext("test"),
            healthyIntervalSeconds: 1,
            failureIntervalSeconds: 1,
            fakeTime);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await timer.StartAsync(cts.Token);

        // Pump the clock. Each 401 tickle re-invokes the throwing reauth callback. A living loop keeps
        // re-invoking it every failure interval; a dead loop (pre-fix) stops after the first throw.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (failureCallbackCount < 3 && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        await timer.StopAsync();

        failureCallbackCount.ShouldBeGreaterThanOrEqualTo(3,
            "A reauth failure thrown from the 401 branch must not kill the keepalive loop — the loop must "
            + "keep ticking at the failure cadence and retry reauth on each cycle.");
        sessionApi.TickleCallCount.ShouldBeGreaterThanOrEqualTo(3,
            "The tickle loop must keep contacting the server after a thrown reauth failure.");
    }

    [Fact]
    public async Task RunAsync_ConsecutiveTransportFailures_MarksHealthUnauthenticatedWithoutReauth()
    {
        // SES-2 (transport arm): transport-level failures are not, on their own,
        // session-dead signals — they must NOT fire reauth. But health cannot report a
        // session live on stale evidence forever: after a run of consecutive transport
        // failures the session is marked unauthenticated so passive health tells the truth.
        var sessionApi = new FakeSessionApi { ShouldThrow = true };
        var fakeTime = new FakeTimeProvider();
        var failureCount = 0;
        Func<CancellationToken, Task> onFailure = _ =>
        {
            Interlocked.Increment(ref failureCount);
            return Task.CompletedTask;
        };

        // Seed health as a live session so the flip to unauthenticated is observable.
        var healthState = new SessionHealthState();
        healthState.Update(authenticated: true, connected: true, competing: false, established: true);

        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);
        var timer = new TickleTimer(
            sessionApi,
            onFailure,
            healthState,
            NullLogger<TickleTimer>.Instance,
            notifier,
            new TenantContext("test"),
            healthyIntervalSeconds: 1,
            failureIntervalSeconds: 1,
            fakeTime);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await timer.StartAsync(cts.Token);

        // Drive a sustained run of transport failures.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (sessionApi.TickleCallCount < 5 && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        // Poll for the health flip (the SetFailed write happens on the tickle-loop thread).
        deadline = DateTime.UtcNow.AddSeconds(2);
        while (healthState.Authenticated && DateTime.UtcNow < deadline)
        {
            await Task.Yield();
        }

        await timer.StopAsync();

        healthState.Authenticated.ShouldBeFalse(
            "Sustained consecutive transport failures must eventually mark the session unauthenticated.");
        failureCount.ShouldBe(0,
            "Transport-level tickle failures must never fire the reauth callback.");
    }

    [Fact]
    public async Task StopAsync_StopsTickling()
    {
        var sessionApi = new FakeSessionApi();
        var fakeTime = new FakeTimeProvider();
        Func<CancellationToken, Task> onFailure = _ => Task.CompletedTask;

        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);
        var timer = new TickleTimer(
            sessionApi,
            onFailure,
            new SessionHealthState(),
            NullLogger<TickleTimer>.Instance,
            notifier,
            new TenantContext("test"),
            healthyIntervalSeconds: 1,
            failureIntervalSeconds: 1,
            fakeTime);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await timer.StartAsync(cts.Token);

        // Let one tick happen before stopping
        fakeTime.Advance(TimeSpan.FromSeconds(1));
        await WaitForTickleCount(sessionApi, 1, TestContext.Current.CancellationToken);

        var countAfterStop = sessionApi.TickleCallCount;
        await timer.StopAsync();

        // Advance clock further — no more ticks should happen since the timer is stopped
        fakeTime.Advance(TimeSpan.FromSeconds(2));
        await Task.Yield();

        sessionApi.TickleCallCount.ShouldBe(countAfterStop);
    }

    [Fact]
    public async Task StopAsync_CalledTwice_DoesNotThrow()
    {
        var sessionApi = new FakeSessionApi();
        var fakeTime = new FakeTimeProvider();
        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);
        var timer = new TickleTimer(
            sessionApi,
            _ => Task.CompletedTask,
            new SessionHealthState(),
            NullLogger<TickleTimer>.Instance,
            notifier,
            new TenantContext("test"),
            healthyIntervalSeconds: 1,
            failureIntervalSeconds: 1,
            fakeTime);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await timer.StartAsync(cts.Token);

        await timer.StopAsync();
        await timer.StopAsync(); // Should not throw
    }

    [Fact]
    public async Task StopAsync_WithoutStart_DoesNotThrow()
    {
        var sessionApi = new FakeSessionApi();
        var fakeTime = new FakeTimeProvider();
        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);
        var timer = new TickleTimer(
            sessionApi,
            _ => Task.CompletedTask,
            new SessionHealthState(),
            NullLogger<TickleTimer>.Instance,
            notifier,
            new TenantContext("test"),
            healthyIntervalSeconds: 1,
            failureIntervalSeconds: 1,
            fakeTime);

        await timer.StopAsync(); // Should not throw
    }

    [Fact]
    public async Task RunAsync_TickleSuccess_FiresTickleSucceededNotification()
    {
        var sessionApi = new FakeSessionApi();
        var fakeTime = new FakeTimeProvider();
        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);
        var notificationCount = 0;
        notifier.SubscribeTickleSucceeded(_ => { Interlocked.Increment(ref notificationCount); return Task.CompletedTask; });

        var timer = new TickleTimer(
            sessionApi,
            _ => Task.CompletedTask,
            new SessionHealthState(),
            NullLogger<TickleTimer>.Instance,
            notifier,
            new TenantContext("test"),
            healthyIntervalSeconds: 1,
            failureIntervalSeconds: 1,
            fakeTime);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await timer.StartAsync(cts.Token);

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (notificationCount < 2 && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        await timer.StopAsync();

        notificationCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task RunAsync_TickleFailure_DoesNotFireTickleSucceededNotification()
    {
        var sessionApi = new FakeSessionApi { ShouldThrow = true };
        var fakeTime = new FakeTimeProvider();
        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);
        var notificationCount = 0;
        notifier.SubscribeTickleSucceeded(_ => { Interlocked.Increment(ref notificationCount); return Task.CompletedTask; });

        var timer = new TickleTimer(
            sessionApi,
            _ => Task.CompletedTask,
            new SessionHealthState(),
            NullLogger<TickleTimer>.Instance,
            notifier,
            new TenantContext("test"),
            healthyIntervalSeconds: 1,
            failureIntervalSeconds: 1,
            fakeTime);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await timer.StartAsync(cts.Token);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (sessionApi.TickleCallCount < 3 && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        await timer.StopAsync();

        notificationCount.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_NotifierThrows_TickleLoopContinues()
    {
        var sessionApi = new FakeSessionApi();
        var fakeTime = new FakeTimeProvider();
        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);
        notifier.SubscribeTickleSucceeded(_ => throw new InvalidOperationException("boom"));

        var timer = new TickleTimer(
            sessionApi,
            _ => Task.CompletedTask,
            new SessionHealthState(),
            NullLogger<TickleTimer>.Instance,
            notifier,
            new TenantContext("test"),
            healthyIntervalSeconds: 1,
            failureIntervalSeconds: 1,
            fakeTime);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await timer.StartAsync(cts.Token);

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (sessionApi.TickleCallCount < 3 && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        await timer.StopAsync();

        sessionApi.TickleCallCount.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task RunAsync_AfterSuccess_UsesHealthyInterval()
    {
        var sessionApi = new FakeSessionApi();
        var fakeTime = new FakeTimeProvider();
        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);

        var timer = new TickleTimer(
            sessionApi,
            _ => Task.CompletedTask,
            new SessionHealthState(),
            NullLogger<TickleTimer>.Instance,
            notifier,
            new TenantContext("test"),
            healthyIntervalSeconds: 60,
            failureIntervalSeconds: 5,
            fakeTime);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await timer.StartAsync(cts.Token);

        // Advance to first tickle
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (sessionApi.TickleCallCount < 1 && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(60));
            await Task.Yield();
        }
        sessionApi.TickleCallCount.ShouldBe(1);

        // Advance 5 seconds — at the failure interval we'd get a second tickle, but we shouldn't
        for (var i = 0; i < 5; i++)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }
        sessionApi.TickleCallCount.ShouldBe(1);

        // Advance another 60 seconds — now we should see the second tickle
        deadline = DateTime.UtcNow.AddSeconds(5);
        while (sessionApi.TickleCallCount < 2 && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(60));
            await Task.Yield();
        }
        sessionApi.TickleCallCount.ShouldBeGreaterThanOrEqualTo(2);

        await timer.StopAsync();
    }

    [Fact]
    public async Task RunAsync_AfterFailure_UsesFailureInterval()
    {
        var sessionApi = new FakeSessionApi { ShouldThrow = true };
        var fakeTime = new FakeTimeProvider();
        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);

        var timer = new TickleTimer(
            sessionApi,
            _ => Task.CompletedTask,
            new SessionHealthState(),
            NullLogger<TickleTimer>.Instance,
            notifier,
            new TenantContext("test"),
            healthyIntervalSeconds: 60,
            failureIntervalSeconds: 5,
            fakeTime);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await timer.StartAsync(cts.Token);

        // Advance 60s to trigger first tickle (healthy interval initially because _lastTickleSucceeded defaults to true)
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (sessionApi.TickleCallCount < 1 && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(60));
            await Task.Yield();
        }
        sessionApi.TickleCallCount.ShouldBe(1);

        // Advance only 5 seconds — at the failure interval we should now get a second tickle
        deadline = DateTime.UtcNow.AddSeconds(5);
        while (sessionApi.TickleCallCount < 2 && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(5));
            await Task.Yield();
        }
        sessionApi.TickleCallCount.ShouldBeGreaterThanOrEqualTo(2);

        await timer.StopAsync();
    }

    [Fact]
    public async Task RunAsync_RecoversAfterFailure_ReturnsToHealthyInterval()
    {
        var sessionApi = new FakeSessionApi { ShouldThrow = true };
        var fakeTime = new FakeTimeProvider();
        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);

        var timer = new TickleTimer(
            sessionApi,
            _ => Task.CompletedTask,
            new SessionHealthState(),
            NullLogger<TickleTimer>.Instance,
            notifier,
            new TenantContext("test"),
            healthyIntervalSeconds: 60,
            failureIntervalSeconds: 5,
            fakeTime);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await timer.StartAsync(cts.Token);

        // First tickle (after 60s healthy interval) fails
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (sessionApi.TickleCallCount < 1 && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(60));
            await Task.Yield();
        }

        // Second tickle (after 5s failure interval) — flip to success right before
        sessionApi.ShouldThrow = false;
        deadline = DateTime.UtcNow.AddSeconds(5);
        while (sessionApi.TickleCallCount < 2 && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(5));
            await Task.Yield();
        }
        sessionApi.TickleCallCount.ShouldBe(2);
        var afterRecovery = sessionApi.TickleCallCount;

        // Advance 5s — at failure interval we'd get another tickle, but cadence should be back to 60s
        for (var i = 0; i < 5; i++)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }
        sessionApi.TickleCallCount.ShouldBe(afterRecovery);

        // Advance 60s — should see the next tickle
        deadline = DateTime.UtcNow.AddSeconds(5);
        while (sessionApi.TickleCallCount < afterRecovery + 1 && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(60));
            await Task.Yield();
        }
        sessionApi.TickleCallCount.ShouldBeGreaterThan(afterRecovery);

        await timer.StopAsync();
    }

    [Fact]
    public async Task RunAsync_WrappedCancellationDuringTickle_DoesNotCountAsFailureAndStopsCleanly()
    {
        // Refit 11 wraps a cancelled in-flight tickle SendAsync in ApiRequestException. On a
        // shutdown-during-tickle race this must be treated as shutdown — NOT logged/counted as a
        // tickle failure — and must not escape StopAsync (which only catches OperationCanceledException).
        using var startCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var sessionApi = new FakeSessionApi
        {
            // On tickle: cancel the timer's token (mirroring shutdown), then throw the Refit 11
            // wrapper exactly as a cancelled in-flight SendAsync would surface it.
            TickleHook = () =>
            {
                startCts.Cancel();
                var inner = new OperationCanceledException("cancelled in flight");
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.ibkr.com/tickle");
                throw new ApiRequestException(request, HttpMethod.Get, new RefitSettings(), inner);
            },
        };

        var fakeTime = new FakeTimeProvider();
        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);
        var healthState = new SessionHealthState();
        var logger = new CapturingLogger<TickleTimer>();
        var timer = new TickleTimer(
            sessionApi,
            _ => Task.CompletedTask,
            healthState,
            logger,
            notifier,
            new TenantContext("test"),
            healthyIntervalSeconds: 1,
            failureIntervalSeconds: 1,
            fakeTime);

        await timer.StartAsync(startCts.Token);

        // Pump until the throwing tickle has been observed.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (sessionApi.TickleCallCount < 1 && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }
        sessionApi.TickleCallCount.ShouldBeGreaterThanOrEqualTo(1, "The tickle should have been attempted.");

        // StopAsync awaits the background task and only catches OperationCanceledException.
        // The wrapped cancellation must unwrap so this completes cleanly.
        await Should.NotThrowAsync(async () => await timer.StopAsync());

        // The wrapped cancellation must NOT be logged as a tickle failure.
        logger.Messages.ShouldNotContain(
            m => m.Level == LogLevel.Warning,
            "Shutdown cancellation wrapped by Refit 11 must not be logged as a tickle failure.");
    }

    /// <summary>
    /// Minimal <see cref="ILogger{T}"/> that captures emitted entries with their level so a test
    /// can assert which log paths did (or did not) run. Mirrors the capturing-logger pattern used
    /// by other test classes in this suite.
    /// </summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add((logLevel, formatter(state, exception)));
        }
    }

    private class FakeSessionApi : IIbkrSessionApi
    {
        private int _tickleCallCount;
        public int TickleCallCount => Volatile.Read(ref _tickleCallCount);
        public bool Authenticated { get; set; } = true;
        public bool ShouldThrow { get; set; }

        /// <summary>
        /// Optional hook invoked inside <see cref="TickleAsync"/> before returning. Lets a test
        /// simulate a cancelled in-flight SendAsync by cancelling the timer's token and throwing.
        /// </summary>
        public Action? TickleHook { get; set; }

        /// <summary>
        /// If set, <see cref="TickleAsync"/> throws this exception. Lets a test simulate a
        /// specific Refit failure (e.g. a 401 <see cref="ApiException"/>) instead of the generic
        /// transport <see cref="HttpRequestException"/> produced by <see cref="ShouldThrow"/>.
        /// </summary>
        public Exception? TickleException { get; set; }

        public Task<TickleResponse> TickleAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _tickleCallCount);

            TickleHook?.Invoke();

            if (TickleException != null)
            {
                throw TickleException;
            }

            if (ShouldThrow)
            {
                throw new HttpRequestException("Simulated tickle failure");
            }

            return Task.FromResult(new TickleResponse(
                Session: string.Empty,
                Hmds: null,
                Iserver: new TickleIserverStatus(
                    AuthStatus: new TickleAuthStatus(Authenticated: Authenticated, Competing: false, Connected: true, Established: true, Message: null, Mac: null, ServerInfo: null, HardwareInfo: null))));
        }

        public Task<SsodhInitResponse> InitializeBrokerageSessionAsync(SsodhInitRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SsodhInitResponse(Authenticated: true, Connected: true, Competing: false, Established: true, Message: null, Mac: null, ServerInfo: null, HardwareInfo: null));

        public Task<SuppressResponse> SuppressQuestionsAsync(SuppressRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SuppressResponse(Status: "submitted"));

        public Task<LogoutResponse> LogoutAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new LogoutResponse(Confirmed: true));

        public Task<SuppressResetResponse> ResetSuppressedQuestionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SuppressResetResponse(Status: "submitted"));

        public Task<AuthStatusResponse> GetAuthStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AuthStatusResponse(true, false, true, true, null, null, null, null, null, null));
    }
}
