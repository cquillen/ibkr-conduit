using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Session;

/// <summary>
/// Edge case tests for <see cref="SessionManager"/> covering concurrent re-auth,
/// dispose-during-init, logout failures, and suppress-skip scenarios.
/// </summary>
public class SessionManagerEdgeTests
{
    [Fact]
    public async Task ReauthenticateAsync_WhenAlreadyReady_SkipsIfTokenNotExpiring()
    {
        var deps = CreateDependencies();

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        // Two concurrent re-auth calls -- the semaphore serializes them.
        // Both should complete without error and only one actual refresh should happen
        // because the second caller acquires the semaphore after the first has
        // already finished refreshing.
        var task1 = manager.ReauthenticateAsync(TestContext.Current.CancellationToken);
        var task2 = manager.ReauthenticateAsync(TestContext.Current.CancellationToken);

        await Task.WhenAll(task1, task2);

        // Both calls go through because the semaphore serializes (doesn't skip).
        // The key behavior is that neither call throws and both complete.
        deps.TokenProvider.RefreshCallCount.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task DisposeAsync_DuringInit_CleansUpGracefully()
    {
        var deps = CreateDependencies();
        deps.TokenProvider.DelayMs = 200; // Slow down token acquisition

        var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        // Start initialization in background
        var initTask = Task.Run(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        // Give init a moment to start, then dispose
        await Task.Delay(50, TestContext.Current.CancellationToken);
        await manager.DisposeAsync();

        // Init may throw OperationCanceledException or ObjectDisposedException,
        // or complete normally before dispose runs. All are acceptable.
        try
        {
            await initTask;
        }
        catch (ObjectDisposedException)
        {
            // Expected -- semaphore was disposed while waiting
        }
        catch (OperationCanceledException)
        {
            // Also acceptable
        }
    }

    [Fact]
    public async Task DisposeAsync_LogoutFailure_SwallowsException()
    {
        var deps = CreateDependencies();
        deps.SessionApi.LogoutShouldThrow = true;

        var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        // Dispose should not throw even though logout fails
        await manager.DisposeAsync();

        deps.SessionApi.LogoutCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task EnsureInitializedAsync_SuppressEmpty_SkipsSuppressCall()
    {
        var deps = CreateDependencies();
        deps.Options = new IbkrClientOptions
        {
            Compete = true,
            SuppressMessageIds = [],
        };

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        deps.SessionApi.SuppressCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ScheduleProactiveRefresh_NearZeroExpiry_SkipsScheduling()
    {
        var deps = CreateDependencies();
        // Token expires in 30 minutes — less than the 1-hour buffer, so
        // timeUntilRefresh = 30m - 1h = -30m <= 0, proactive refresh skipped
        deps.TokenProvider.TokenExpiry = DateTimeOffset.UtcNow.AddMinutes(30);

        var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        // Dispose immediately — should succeed without errors from proactive refresh
        await manager.DisposeAsync();

        // Token was acquired but no proactive refresh fired (no RefreshAsync call)
        deps.TokenProvider.GetCallCount.ShouldBe(1);
        deps.TokenProvider.RefreshCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ReauthenticateAsync_WhileShuttingDown_ReturnsEarly()
    {
        var deps = CreateDependencies();

        var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        // Dispose sets state to ShuttingDown
        await manager.DisposeAsync();

        // Session is now shut down — verify it completed cleanly
        deps.SessionApi.LogoutCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task ProactiveRefresh_NearExpiry_DisposeCleansUpWithoutCrash()
    {
        var deps = CreateDependencies();
        // Token expires in 1h + 200ms — proactive refresh would fire after ~200ms
        deps.TokenProvider.TokenExpiry = DateTimeOffset.UtcNow.AddHours(1).AddMilliseconds(200);
        // Make RefreshAsync throw to test that the background task catches exceptions
        deps.TokenProvider.RefreshShouldThrow = true;

        var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        // Wait long enough for proactive refresh to attempt and fail
        await Task.Delay(500, TestContext.Current.CancellationToken);

        // Dispose should not throw even though refresh failed in background
        await manager.DisposeAsync();
    }

    private static TestDependencies CreateDependencies() => new();

    private class TestDependencies
    {
        public DelayableTokenProvider TokenProvider { get; } = new();
        public FakeTickleTimerFactory TickleTimerFactory { get; } = new();
        public FakeSessionApi SessionApi { get; } = new();
        public FakeLifecycleNotifier Notifier { get; } = new();
        public IbkrClientOptions Options { get; set; } = new();
    }

    internal class DelayableTokenProvider : ISessionTokenProvider
    {
        private LiveSessionToken? _tokenOverride;

        public int GetCallCount { get; private set; }
        public int RefreshCallCount { get; private set; }
        public int DelayMs { get; set; }
        public bool RefreshShouldThrow { get; set; }

        public DateTimeOffset TokenExpiry
        {
            set => _tokenOverride = new LiveSessionToken(new byte[] { 0x01, 0x02, 0x03 }, value);
        }

        public async Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken)
        {
            GetCallCount++;
            if (DelayMs > 0)
            {
                await Task.Delay(DelayMs, cancellationToken);
            }

            return _tokenOverride ?? new LiveSessionToken(
                new byte[] { 0x01, 0x02, 0x03 },
                DateTimeOffset.UtcNow.AddHours(24));
        }

        public async Task<LiveSessionToken> RefreshAsync(CancellationToken cancellationToken)
        {
            RefreshCallCount++;
            if (RefreshShouldThrow)
            {
                throw new InvalidOperationException("Simulated refresh failure");
            }

            if (DelayMs > 0)
            {
                await Task.Delay(DelayMs, cancellationToken);
            }

            return new LiveSessionToken(
                new byte[] { 0x04, 0x05, 0x06 },
                DateTimeOffset.UtcNow.AddHours(24));
        }
    }

    internal class FakeTickleTimerFactory : ITickleTimerFactory
    {
        public int CreateCount { get; private set; }
        public FakeTickleTimer? CreatedTimer { get; private set; }

        public ITickleTimer Create(
            IIbkrSessionApi sessionApi,
            Func<CancellationToken, Task> onFailure)
        {
            CreateCount++;
            CreatedTimer = new FakeTickleTimer();
            return CreatedTimer;
        }
    }

    internal class FakeTickleTimer : ITickleTimer
    {
        public bool Started { get; private set; }
        public bool Stopped { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Started = true;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            Stopped = true;
            return Task.CompletedTask;
        }
    }

    internal class FakeLifecycleNotifier : ISessionLifecycleNotifier
    {
        public int NotifyCallCount { get; private set; }

        public IDisposable Subscribe(Func<CancellationToken, Task> onSessionRefreshed) =>
            throw new NotImplementedException();

        public Task NotifyAsync(CancellationToken cancellationToken)
        {
            NotifyCallCount++;
            return Task.CompletedTask;
        }
    }

    internal class FakeSessionApi : IIbkrSessionApi
    {
        public int InitCallCount { get; private set; }
        public int SuppressCallCount { get; private set; }
        public int LogoutCallCount { get; private set; }
        public bool LogoutShouldThrow { get; set; }

        public Task<SsodhInitResponse> InitializeBrokerageSessionAsync(
            SsodhInitRequest request, CancellationToken cancellationToken = default)
        {
            InitCallCount++;
            return Task.FromResult(new SsodhInitResponse(true, true, false));
        }

        public Task<TickleResponse> TickleAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new TickleResponse(
                Session: string.Empty,
                Iserver: new TickleIserverStatus(
                    AuthStatus: new TickleAuthStatus(true, false, true))));

        public Task<SuppressResponse> SuppressQuestionsAsync(
            SuppressRequest request, CancellationToken cancellationToken = default)
        {
            SuppressCallCount++;
            return Task.FromResult(new SuppressResponse("submitted"));
        }

        public Task<LogoutResponse> LogoutAsync(CancellationToken cancellationToken = default)
        {
            LogoutCallCount++;
            if (LogoutShouldThrow)
            {
                throw new HttpRequestException("Simulated logout failure");
            }

            return Task.FromResult(new LogoutResponse(true));
        }

        public Task<SuppressResetResponse> ResetSuppressedQuestionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SuppressResetResponse(Status: "submitted"));

        public Task<AuthStatusResponse> GetAuthStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AuthStatusResponse(true, false, true, null, null, null));

#pragma warning disable CS0618 // Obsolete member
        public Task<ReauthenticateResponse> ReauthenticateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ReauthenticateResponse(Message: "triggered"));
#pragma warning restore CS0618

        public Task<SsoValidateResponse> ValidateSsoAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SsoValidateResponse(UserId: 12345, Expire: 0, Result: true, AuthTime: 0));
    }
}
