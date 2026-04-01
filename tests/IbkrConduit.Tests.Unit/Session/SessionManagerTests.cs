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

public class SessionManagerTests
{
    [Fact]
    public async Task EnsureInitializedAsync_FirstCall_InitializesSession()
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

        deps.SessionApi.InitCallCount.ShouldBe(1);
        deps.SessionApi.LastInitRequest.ShouldNotBeNull();
        deps.SessionApi.LastInitRequest!.Publish.ShouldBeTrue();
        deps.SessionApi.LastInitRequest.Compete.ShouldBeTrue();
    }

    [Fact]
    public async Task EnsureInitializedAsync_SecondCall_DoesNotReinitialize()
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
        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        deps.SessionApi.InitCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task EnsureInitializedAsync_AcquiresLstBeforeInit()
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

        deps.TokenProvider.GetCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task EnsureInitializedAsync_WithSuppressIds_SuppressesQuestions()
    {
        var deps = CreateDependencies();
        deps.Options = new IbkrClientOptions
        {
            Compete = true,
            SuppressMessageIds = new List<string> { "o163", "o451" },
        };

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        deps.SessionApi.SuppressCallCount.ShouldBe(1);
        deps.SessionApi.LastSuppressRequest.ShouldNotBeNull();
        deps.SessionApi.LastSuppressRequest!.MessageIds.ShouldBe(
            new List<string> { "o163", "o451" });
    }

    [Fact]
    public async Task EnsureInitializedAsync_WithoutSuppressIds_SkipsSuppression()
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

        deps.SessionApi.SuppressCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task EnsureInitializedAsync_StartsTickleTimer()
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

        deps.TickleTimerFactory.CreatedTimer.ShouldNotBeNull();
        deps.TickleTimerFactory.CreatedTimer!.Started.ShouldBeTrue();
    }

    [Fact]
    public async Task ReauthenticateAsync_RefreshesTokenAndReinitsSession()
    {
        var deps = CreateDependencies();

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        // Initialize first
        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        // Trigger re-auth
        await manager.ReauthenticateAsync(TestContext.Current.CancellationToken);

        deps.TokenProvider.RefreshCallCount.ShouldBe(1);
        deps.SessionApi.InitCallCount.ShouldBe(2); // once for init, once for re-auth
    }

    [Fact]
    public async Task ReauthenticateAsync_StopsAndRestartsTickleTimer()
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
        var firstTimer = deps.TickleTimerFactory.CreatedTimer!;

        await manager.ReauthenticateAsync(TestContext.Current.CancellationToken);

        firstTimer.Stopped.ShouldBeTrue();
        // A new timer was created and started
        deps.TickleTimerFactory.CreateCount.ShouldBe(2);
        deps.TickleTimerFactory.CreatedTimer!.Started.ShouldBeTrue();
    }

    [Fact]
    public async Task ReauthenticateAsync_WithSuppressIds_ResuppressesQuestions()
    {
        var deps = CreateDependencies();
        deps.Options = new IbkrClientOptions
        {
            Compete = true,
            SuppressMessageIds = new List<string> { "o163" },
        };

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);
        await manager.ReauthenticateAsync(TestContext.Current.CancellationToken);

        deps.SessionApi.SuppressCallCount.ShouldBe(2);
    }

    [Fact]
    public async Task DisposeAsync_CallsLogout()
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
        await manager.DisposeAsync();

        deps.SessionApi.LogoutCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task DisposeAsync_StopsTickleTimer()
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
        var timer = deps.TickleTimerFactory.CreatedTimer!;

        await manager.DisposeAsync();

        timer.Stopped.ShouldBeTrue();
    }

    [Fact]
    public async Task DisposeAsync_WithoutInit_DoesNotThrow()
    {
        var deps = CreateDependencies();

        var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        // Should not throw even if never initialized
        await manager.DisposeAsync();

        deps.SessionApi.LogoutCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task DisposeAsync_LogoutThrows_DoesNotPropagate()
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

        // Should not throw
        await manager.DisposeAsync();
    }

    [Fact]
    public async Task EnsureInitializedAsync_PreCancelledToken_ThrowsOperationCanceled()
    {
        var deps = CreateDependencies();

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(
            () => manager.EnsureInitializedAsync(cts.Token));
    }

    [Fact]
    public async Task ReauthenticateAsync_NotifiesSessionLifecycleSubscribers()
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
        await manager.ReauthenticateAsync(TestContext.Current.CancellationToken);

        deps.Notifier.NotifyCallCount.ShouldBe(1);
    }

    private static TestDependencies CreateDependencies() => new();

    private class TestDependencies
    {
        public FakeSessionTokenProvider TokenProvider { get; } = new();
        public FakeTickleTimerFactory TickleTimerFactory { get; } = new();
        public FakeSessionApi SessionApi { get; } = new();
        public FakeLifecycleNotifier Notifier { get; } = new();
        public IbkrClientOptions Options { get; set; } = new();
    }

    internal class FakeSessionTokenProvider : ISessionTokenProvider
    {
        private readonly LiveSessionToken _token = new(
            new byte[] { 0x01, 0x02, 0x03 },
            DateTimeOffset.UtcNow.AddHours(24));

        public int GetCallCount { get; private set; }
        public int RefreshCallCount { get; private set; }

        public Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken)
        {
            GetCallCount++;
            return Task.FromResult(_token);
        }

        public Task<LiveSessionToken> RefreshAsync(CancellationToken cancellationToken)
        {
            RefreshCallCount++;
            return Task.FromResult(new LiveSessionToken(
                new byte[] { 0x04, 0x05, 0x06 },
                DateTimeOffset.UtcNow.AddHours(24)));
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
        public SsodhInitRequest? LastInitRequest { get; private set; }
        public SuppressRequest? LastSuppressRequest { get; private set; }
        public bool LogoutShouldThrow { get; set; }

        public Task<SsodhInitResponse> InitializeBrokerageSessionAsync(SsodhInitRequest request, CancellationToken cancellationToken = default)
        {
            InitCallCount++;
            LastInitRequest = request;
            return Task.FromResult(new SsodhInitResponse(Authenticated: true, Connected: true, Competing: false));
        }

        public Task<TickleResponse> TickleAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new TickleResponse(
                Session: string.Empty,
                Iserver: new TickleIserverStatus(
                    AuthStatus: new TickleAuthStatus(Authenticated: true, Competing: false, Connected: true))));

        public Task<SuppressResponse> SuppressQuestionsAsync(SuppressRequest request, CancellationToken cancellationToken = default)
        {
            SuppressCallCount++;
            LastSuppressRequest = request;
            return Task.FromResult(new SuppressResponse(Status: "submitted"));
        }

        public Task<LogoutResponse> LogoutAsync(CancellationToken cancellationToken = default)
        {
            LogoutCallCount++;
            if (LogoutShouldThrow)
            {
                throw new HttpRequestException("Simulated logout failure");
            }

            return Task.FromResult(new LogoutResponse(Confirmed: true));
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
