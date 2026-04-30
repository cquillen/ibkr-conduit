using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Health;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Session;

public class TickleTimerTests
{
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
    public async Task StartAsync_WhenTickleThrows_InvokesFailureCallback()
    {
        var sessionApi = new FakeSessionApi { ShouldThrow = true };
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
            healthyIntervalSeconds: 1,
            failureIntervalSeconds: 1,
            fakeTime);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await timer.StartAsync(cts.Token);

        fakeTime.Advance(TimeSpan.FromSeconds(1));
        await failureTcs.Task.WaitAsync(TestContext.Current.CancellationToken);

        failureTcs.Task.IsCompletedSuccessfully.ShouldBeTrue("Failure callback should have been invoked on exception");
        await timer.StopAsync();
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

    private class FakeSessionApi : IIbkrSessionApi
    {
        private int _tickleCallCount;
        public int TickleCallCount => Volatile.Read(ref _tickleCallCount);
        public bool Authenticated { get; set; } = true;
        public bool ShouldThrow { get; set; }

        public Task<TickleResponse> TickleAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _tickleCallCount);

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
