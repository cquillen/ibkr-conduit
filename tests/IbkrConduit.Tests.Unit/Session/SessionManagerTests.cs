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
    public async Task ReauthenticateAsync_StopsAndRestartsTickleTimer()
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
    public async Task EnsureInitializedAsync_HttpRequestException_NetworkError_WrapsInConfigurationException()
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

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("network");
        ex.CredentialHint.ShouldBe("BaseUrl");
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
    public async Task EnsureInitializedAsync_OperationCanceledException_NotWrapped()
    {
        var deps = CreateDependencies();
        deps.TokenProvider.GetException = new OperationCanceledException("Canceled");

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance);

        await Should.ThrowAsync<OperationCanceledException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));
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
        var deps = CreateDependencies();
        deps.TokenProvider.TokenLifetime = TimeSpan.FromSeconds(2);
        deps.TokenProvider.SimulateAsyncRefresh = true;
        deps.Options.ProactiveRefreshMargin = TimeSpan.FromMilliseconds(1500);

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        // Token expires in 2s, margin is 1.5s → refresh fires in ~0.5s
        // Wait for the proactive refresh to complete
        await Task.Delay(2000, TestContext.Current.CancellationToken);

        // The refresh should have called RefreshAsync (re-acquire LST)
        // and InitializeBrokerageSessionAsync (re-init session)
        deps.TokenProvider.RefreshCallCount.ShouldBeGreaterThanOrEqualTo(1,
            "Proactive refresh should have called RefreshAsync");
        deps.SessionApi.InitCallCount.ShouldBeGreaterThanOrEqualTo(2,
            "Proactive refresh should have re-initialized the session (1 init + 1 refresh)");
    }

    private static TestDependencies CreateDependencies() => new();

    private class TestDependencies
    {
        public FakeSessionTokenProvider TokenProvider { get; } = new();
        public FakeTickleTimerFactory TickleTimerFactory { get; } = new();
        public FakeSessionApi SessionApi { get; } = new();
        public FakeLifecycleNotifier Notifier { get; } = new();
        public SessionHealthState SessionHealthState { get; } = new();
        public IbkrClientOptions Options { get; set; } = new();
    }

    internal class FakeSessionTokenProvider : ISessionTokenProvider
    {
        public int GetCallCount { get; private set; }
        public int RefreshCallCount { get; private set; }

        public DateTimeOffset? CurrentTokenExpiry => DateTimeOffset.UtcNow.Add(TokenLifetime);

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

            return Task.FromResult(new LiveSessionToken(
                new byte[] { 0x01, 0x02, 0x03 },
                DateTimeOffset.UtcNow.Add(TokenLifetime)));
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

            return new LiveSessionToken(
                new byte[] { 0x04, 0x05, 0x06 },
                DateTimeOffset.UtcNow.Add(TokenLifetime));
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

        /// <summary>If set, InitializeBrokerageSessionAsync throws this exception.</summary>
        public Exception? InitException { get; set; }

        public Task<SsodhInitResponse> InitializeBrokerageSessionAsync(SsodhInitRequest request, CancellationToken cancellationToken = default)
        {
            InitCallCount++;
            LastInitRequest = request;
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
