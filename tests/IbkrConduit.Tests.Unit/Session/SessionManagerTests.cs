using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Errors;
using IbkrConduit.Health;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
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
            deps.SessionHealthState,
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
            deps.SessionHealthState,
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
            deps.SessionHealthState,
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
            deps.SessionHealthState,
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
            deps.SessionHealthState,
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
            deps.SessionHealthState,
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
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance);

        // Initialize first
        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        // Trigger re-auth
        await manager.ReauthenticateAsync(TestContext.Current.CancellationToken);

        deps.TokenProvider.RefreshCallCount.ShouldBe(1);
        deps.SessionApi.InitCallCount.ShouldBe(2); // once for init, once for re-auth
    }

    [Fact]
    public async Task ReauthenticateAsync_DoesNotStopOrRecreateTickleTimer()
    {
        // Stopping the tickle timer from inside ReauthenticateAsync is a deadlock
        // hazard when reauth is itself triggered from the tickle's own failure
        // callback (TickleTimer.RunAsync awaits the callback; the callback calls
        // ReauthenticateAsync; ReauthenticateAsync.StopAsync awaits the same
        // background task). The fix is to keep the existing tickle timer running
        // through the reauth — the OAuth signing layer reads the new LST once
        // SessionTokenProvider updates it, so the next tickle cycle uses the
        // refreshed credentials automatically.
        //
        // This test pins the new contract: the original timer keeps running
        // through reauth, and no second timer is created.
        var deps = CreateDependencies();

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);
        var firstTimer = deps.TickleTimerFactory.CreatedTimer!;

        await manager.ReauthenticateAsync(TestContext.Current.CancellationToken);

        firstTimer.Stopped.ShouldBeFalse(
            "Reauth must not stop the tickle timer (would deadlock when reauth is triggered from the tickle's own failure callback).");
        deps.TickleTimerFactory.CreateCount.ShouldBe(
            1,
            "Reauth must not create a second tickle timer.");
        deps.TickleTimerFactory.CreatedTimer.ShouldBeSameAs(
            firstTimer,
            "The original timer instance must remain in use through reauth.");
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
            deps.SessionHealthState,
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
            deps.SessionHealthState,
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
            deps.SessionHealthState,
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
            deps.SessionHealthState,
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
            deps.SessionHealthState,
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
            deps.SessionHealthState,
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
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);
        await manager.ReauthenticateAsync(TestContext.Current.CancellationToken);

        deps.Notifier.NotifyCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task EnsureInitializedAsync_CryptographicException_Decrypt_WrapsInConfigurationException()
    {
        var deps = CreateDependencies();
        deps.TokenProvider.GetException = new CryptographicException("Unable to decrypt data");

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance);

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("decrypt");
        ex.CredentialHint.ShouldBe("EncryptionPrivateKey");
        ex.InnerException.ShouldBeOfType<CryptographicException>();
    }

    [Fact]
    public async Task EnsureInitializedAsync_CryptographicException_Sign_WrapsInConfigurationException()
    {
        var deps = CreateDependencies();
        deps.TokenProvider.GetException = new CryptographicException("Unable to sign data");

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance);

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("signature");
        ex.CredentialHint.ShouldBe("SignaturePrivateKey");
        ex.InnerException.ShouldBeOfType<CryptographicException>();
    }

    [Fact]
    public async Task EnsureInitializedAsync_CryptographicException_Generic_WrapsInConfigurationException()
    {
        var deps = CreateDependencies();
        deps.TokenProvider.GetException = new CryptographicException("The parameter is incorrect");

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance);

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Cryptographic operation failed");
        ex.CredentialHint.ShouldBe("SignaturePrivateKey, EncryptionPrivateKey");
        ex.InnerException.ShouldBeOfType<CryptographicException>();
    }

    [Fact]
    public async Task EnsureInitializedAsync_HttpRequestException_401_WrapsInConfigurationException()
    {
        var deps = CreateDependencies();
        deps.TokenProvider.GetException = new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized);

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance);

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("ConsumerKey");
        ex.CredentialHint.ShouldBe("ConsumerKey, AccessToken");
        ex.InnerException.ShouldBeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task EnsureInitializedAsync_HttpRequestException_403_WrapsInConfigurationException()
    {
        var deps = CreateDependencies();
        deps.TokenProvider.GetException = new HttpRequestException("Forbidden", null, HttpStatusCode.Forbidden);

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance);

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("rejected");
        ex.CredentialHint.ShouldBe("ConsumerKey, AccessToken");
        ex.InnerException.ShouldBeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task EnsureInitializedAsync_HttpRequestException_NetworkError_WrapsInTransientException()
    {
        var deps = CreateDependencies();
        deps.TokenProvider.GetException = new HttpRequestException("Connection refused");

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance);

        var ex = await Should.ThrowAsync<IbkrTransientException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));

        ex.InnerException.ShouldBeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task EnsureInitializedAsync_FormatException_WrapsInConfigurationException()
    {
        var deps = CreateDependencies();
        deps.TokenProvider.GetException = new FormatException("Input string was not in a correct format");

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance);

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Diffie-Hellman");
        ex.CredentialHint.ShouldBe("DhPrime");
        ex.InnerException.ShouldBeOfType<FormatException>();
    }

    [Fact]
    public async Task EnsureInitializedAsync_InvalidOperationException_WrapsInConfigurationException()
    {
        var deps = CreateDependencies();
        deps.TokenProvider.GetException = new InvalidOperationException("DH exchange failed");

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance);

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Diffie-Hellman");
        ex.CredentialHint.ShouldBe("DhPrime");
        ex.InnerException.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task EnsureInitializedAsync_JsonException_WrapsInConfigurationException()
    {
        var deps = CreateDependencies();
        deps.TokenProvider.GetException = new JsonException("Unexpected token");

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance);

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Unexpected response format");
        ex.CredentialHint.ShouldBe("BaseUrl");
        ex.InnerException.ShouldBeOfType<JsonException>();
    }

    [Fact]
    public async Task EnsureInitializedAsync_InitApiThrows_WrapsInConfigurationException()
    {
        var deps = CreateDependencies();
        deps.SessionApi.InitException = new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized);

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance);

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));

        ex.CredentialHint.ShouldBe("ConsumerKey, AccessToken");
        ex.InnerException.ShouldBeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task ReauthenticateAsync_TokenProviderThrows_WrapsInConfigurationException()
    {
        var deps = CreateDependencies();

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance);

        // Initialize successfully first
        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        // Now make refresh throw
        deps.TokenProvider.RefreshException = new CryptographicException("Unable to decrypt data");

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => manager.ReauthenticateAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("decrypt");
        ex.CredentialHint.ShouldBe("EncryptionPrivateKey");
        ex.InnerException.ShouldBeOfType<CryptographicException>();
    }

    [Fact]
    public async Task EnsureInitializedAsync_CallerCancelled_OperationCanceledException_NotWrapped()
    {
        var deps = CreateDependencies();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        deps.TokenProvider.GetException = new OperationCanceledException("Canceled", cts.Token);

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance);

        await Should.ThrowAsync<OperationCanceledException>(
            () => manager.EnsureInitializedAsync(cts.Token));
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_DoesNotThrow()
    {
        var deps = CreateDependencies();

        var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        await manager.DisposeAsync();
        await manager.DisposeAsync(); // Should not throw
    }

    [Fact]
    public async Task ProactiveRefresh_CompletesReauthWithoutCancellation()
    {
        var fakeTime = new FakeTimeProvider();
        var deps = CreateDependencies();
        deps.TokenProvider = new FakeSessionTokenProvider(fakeTime);
        deps.TokenProvider.TokenLifetime = TimeSpan.FromSeconds(10);
        deps.TokenProvider.SimulateAsyncRefresh = true;
        deps.Options.ProactiveRefreshMargin = TimeSpan.FromSeconds(8);

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance,
            fakeTime);

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);
        // Token lifetime = 10s, margin = 8s → timeUntilRefresh = 2s on the fake clock.
        // Advance 2s to trigger proactive refresh.
        fakeTime.Advance(TimeSpan.FromSeconds(2));

        // Wait for the background reauth to complete.
        // SecondInitTask completes as soon as InitCallCount reaches 2, which is the reliable
        // signal that the full reauth cycle (RefreshAsync + InitializeBrokerageSessionAsync)
        // has completed. WaitAsync ties the wait to the test CancellationToken for safety.
        await deps.SessionApi.SecondInitTask.WaitAsync(TestContext.Current.CancellationToken);

        deps.TokenProvider.RefreshCallCount.ShouldBeGreaterThanOrEqualTo(1,
            "Proactive refresh should have called RefreshAsync");
        deps.SessionApi.InitCallCount.ShouldBeGreaterThanOrEqualTo(2,
            "Proactive refresh should have re-initialized the session (1 init + 1 refresh)");
    }

    private static TestDependencies CreateDependencies() => new();

    private class TestDependencies
    {
        public FakeSessionTokenProvider TokenProvider { get; set; } = new();
        public FakeTickleTimerFactory TickleTimerFactory { get; } = new();
        public FakeSessionApi SessionApi { get; } = new();
        public FakeLifecycleNotifier Notifier { get; } = new();
        public SessionHealthState SessionHealthState { get; } = new();
        public IbkrClientOptions Options { get; set; } = new();
    }

    internal class FakeSessionTokenProvider : ISessionTokenProvider
    {
        private readonly TimeProvider _timeProvider;
        private DateTimeOffset? _lastExpiry;

        public FakeSessionTokenProvider(TimeProvider? timeProvider = null)
        {
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        public int GetCallCount { get; private set; }
        public int RefreshCallCount { get; private set; }

        public DateTimeOffset? CurrentTokenExpiry => _lastExpiry;

        /// <summary>Token expiry for newly created tokens. Default 24 hours.</summary>
        public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromHours(24);

        /// <summary>If set, GetLiveSessionTokenAsync throws this exception.</summary>
        public Exception? GetException { get; set; }

        /// <summary>If set, RefreshAsync throws this exception.</summary>
        public Exception? RefreshException { get; set; }

        public Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken)
        {
            GetCallCount++;
            if (GetException != null)
            {
                throw GetException;
            }

            _lastExpiry = _timeProvider.GetUtcNow().Add(TokenLifetime);
            return Task.FromResult(new LiveSessionToken(
                new byte[] { 0x01, 0x02, 0x03 },
                _lastExpiry.Value));
        }

        /// <summary>When true, RefreshAsync yields before returning to simulate real async work.</summary>
        public bool SimulateAsyncRefresh { get; set; }

        public async Task<LiveSessionToken> RefreshAsync(CancellationToken cancellationToken)
        {
            RefreshCallCount++;
            if (RefreshException != null)
            {
                throw RefreshException;
            }

            if (SimulateAsyncRefresh)
            {
                // Yield to allow cancellation to propagate — simulates real HTTP call
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
            }

            _lastExpiry = _timeProvider.GetUtcNow().Add(TokenLifetime);
            return new LiveSessionToken(
                new byte[] { 0x04, 0x05, 0x06 },
                _lastExpiry.Value);
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

        public IDisposable SubscribeTickleSucceeded(Func<CancellationToken, Task> onTickleSucceeded) =>
            throw new NotImplementedException();

        public Task NotifyTickleSucceededAsync(CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    internal class FakeSessionApi : IIbkrSessionApi
    {
        private readonly TaskCompletionSource _secondInitTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int InitCallCount { get; private set; }
        public int SuppressCallCount { get; private set; }
        public int LogoutCallCount { get; private set; }
        public SsodhInitRequest? LastInitRequest { get; private set; }
        public SuppressRequest? LastSuppressRequest { get; private set; }
        public bool LogoutShouldThrow { get; set; }

        /// <summary>If set, InitializeBrokerageSessionAsync throws this exception.</summary>
        public Exception? InitException { get; set; }

        /// <summary>Completes when <see cref="InitCallCount"/> reaches 2 (first re-auth).</summary>
        public Task SecondInitTask => _secondInitTcs.Task;

        public Task<SsodhInitResponse> InitializeBrokerageSessionAsync(SsodhInitRequest request, CancellationToken cancellationToken = default)
        {
            InitCallCount++;
            LastInitRequest = request;
            if (InitCallCount >= 2)
            {
                _secondInitTcs.TrySetResult();
            }

            if (InitException != null)
            {
                throw InitException;
            }

            return Task.FromResult(new SsodhInitResponse(Authenticated: true, Connected: true, Competing: false, Established: true, Message: null, Mac: null, ServerInfo: null, HardwareInfo: null));
        }

        public Task<TickleResponse> TickleAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new TickleResponse(
                Session: string.Empty,
                Hmds: null,
                Iserver: new TickleIserverStatus(
                    AuthStatus: new TickleAuthStatus(Authenticated: true, Competing: false, Connected: true, Established: true, Message: null, Mac: null, ServerInfo: null, HardwareInfo: null))));

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
            Task.FromResult(new AuthStatusResponse(true, false, true, true, null, null, null, null, null, null));

    }
}
