using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Diagnostics;
using IbkrConduit.Errors;
using IbkrConduit.Health;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Refit;
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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        deps.SessionApi.SuppressCallCount.ShouldBe(1);
        deps.SessionApi.LastSuppressRequest.ShouldNotBeNull();
        deps.SessionApi.LastSuppressRequest!.MessageIds.ShouldBe(
            new List<string> { "o163", "o451" });
    }

    [Fact]
    public async Task EnsureInitializedAsync_SuppressWrappedTransportFault_ThrowsOriginalHttpRequestException()
    {
        // Refit 11 wraps the raw Task<T> /suppress call's transport fault in
        // ApiRequestException. EnsureInitializedAsync must surface the original
        // HttpRequestException — not the wrapper, and not a credential exception.
        var deps = CreateDependencies();
        deps.Options = new IbkrClientOptions
        {
            Compete = true,
            SuppressMessageIds = new List<string> { "o163" },
        };

        var request = new HttpRequestMessage(
            HttpMethod.Post, "https://api.ibkr.com/v1/api/iserver/questions/suppress");
        var inner = new HttpRequestException("connection refused");
        deps.SessionApi.SuppressException = new ApiRequestException(
            request, HttpMethod.Post, new RefitSettings(), inner);

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

        var ex = await Should.ThrowAsync<HttpRequestException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));
        ex.ShouldBeSameAs(inner);
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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

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
    public async Task ReauthenticateAsync_ConcurrentCalls_AcquiresLstOnlyOnce()
    {
        // Issue #168: when many requests 401 in a tight burst, each one calls
        // ReauthenticateAsync. The semaphore serializes them, but without a
        // Ready-state short-circuit each queued caller still re-runs the full
        // LST handshake even though the first call has already restored the
        // session. The fix: after acquiring the semaphore, check if state is
        // already Ready and return — same double-check pattern that
        // EnsureInitializedAsync uses.
        var deps = CreateDependencies();

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        var refreshBefore = deps.TokenProvider.RefreshCallCount;
        var initBefore = deps.SessionApi.InitCallCount;

        // Fire two concurrent reauths from a Ready state. The semaphore
        // serializes them; the second one must observe Ready and return
        // without doing any work.
        var reauthA = manager.ReauthenticateAsync(TestContext.Current.CancellationToken);
        var reauthB = manager.ReauthenticateAsync(TestContext.Current.CancellationToken);
        await Task.WhenAll(reauthA, reauthB);

        (deps.TokenProvider.RefreshCallCount - refreshBefore).ShouldBe(1,
            "Two concurrent ReauthenticateAsync calls from Ready state must result in exactly one LST refresh.");
        (deps.SessionApi.InitCallCount - initBefore).ShouldBe(1,
            "Two concurrent ReauthenticateAsync calls from Ready state must result in exactly one /ssodh/init call.");
    }

    [Fact]
    public async Task ReauthenticateAsync_FirstCallThrows_QueuedCallerStillReauths()
    {
        // Pins the invariant that Interlocked.Increment(ref _reauthEpoch) lives
        // on the success path, NOT in the finally. If reauth A throws, the
        // epoch must NOT advance — caller B (queued behind A on the semaphore)
        // must observe epoch == snapshot and proceed with its own reauth
        // attempt rather than incorrectly short-circuiting.
        // See issue #168.
        var deps = CreateDependencies();

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        var refreshBefore = deps.TokenProvider.RefreshCallCount;

        // Configure the token provider to throw on the next RefreshAsync,
        // succeed on subsequent calls. The first reauth to acquire the
        // semaphore will hit the failing call; the second (queued) caller
        // must still execute its own RefreshAsync rather than short-circuit.
        deps.TokenProvider.ThrowOnNextRefresh = true;

        var reauthA = manager.ReauthenticateAsync(TestContext.Current.CancellationToken);
        var reauthB = manager.ReauthenticateAsync(TestContext.Current.CancellationToken);

        var results = await Task.WhenAll(
            CaptureOutcome(reauthA),
            CaptureOutcome(reauthB));

        // Exactly one threw (the failed first reauth, wrapped to
        // IbkrTransientException by WrapCredentialException), one succeeded
        // (the queued caller's own retry).
        results.Count(r => r.Threw).ShouldBe(1,
            "Exactly one of the concurrent reauths should have hit the throwing first call.");
        results.Count(r => !r.Threw).ShouldBe(1,
            "The other reauth should have proceeded successfully on its own LST refresh.");
        results.Single(r => r.Threw).Exception.ShouldBeOfType<IbkrTransientException>();

        // RefreshAsync was called twice during the burst: once for the failed
        // attempt, once for the queued caller's retry. If a future refactor
        // moved the epoch increment into `finally`, the queued caller would
        // observe epoch != snapshot and short-circuit, leaving this delta at 1.
        (deps.TokenProvider.RefreshCallCount - refreshBefore).ShouldBe(2,
            "Failed reauth must NOT advance the epoch — queued caller must still execute its own LST refresh attempt.");

        static async Task<(bool Threw, Exception? Exception)> CaptureOutcome(Task t)
        {
            try
            {
                await t;
                return (false, null);
            }
            catch (Exception ex)
            {
                return (true, ex);
            }
        }
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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

        // Should not throw even if never initialized
        await manager.DisposeAsync();

        deps.SessionApi.LogoutCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task DisposeAsync_SkipLogoutOnDispose_DoesNotLogout()
    {
        // The manager path (ManagedTenant) issues its own single bounded logout, so the
        // session manager suppresses its dispose-time logout to avoid a duplicate (MGR-1).
        var deps = CreateDependencies();
        deps.Options.SkipLogoutOnDispose = true;

        var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);
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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

        await Should.ThrowAsync<OperationCanceledException>(
            () => manager.EnsureInitializedAsync(cts.Token));
    }

    [Fact]
    public async Task EnsureInitializedAsync_WrappedCancellation_PropagatesOperationCanceled()
    {
        // Refit 11 wraps caller cancellation thrown from a raw Task<T> session call
        // (e.g. /ssodh/init) in an ApiRequestException. The SessionManager must unwrap
        // it so cancellation propagates as OperationCanceledException rather than being
        // misreported as a credential error by WrapCredentialException.
        var deps = CreateDependencies();
        using var cts = new CancellationTokenSource();

        var request = new HttpRequestMessage(
            HttpMethod.Post, "https://api.ibkr.com/v1/api/iserver/auth/ssodh/init");
        // Cancel the token mid-init (so the semaphore wait succeeds first), then throw
        // the Refit 11 ApiRequestException wrapping the resulting OperationCanceledException.
        deps.SessionApi.OnInit = () => cts.Cancel();
        deps.SessionApi.InitException = new ApiRequestException(
            request, HttpMethod.Post, new RefitSettings(),
            new OperationCanceledException(cts.Token));

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

        await Should.ThrowAsync<OperationCanceledException>(
            () => manager.EnsureInitializedAsync(cts.Token));
    }

    [Fact]
    public async Task ReauthenticateAsync_WrappedCancellation_PropagatesOperationCanceled()
    {
        // Same Refit 11 wrapping applies to the reauth path's /ssodh/init call.
        var deps = CreateDependencies();

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

        // Initialize successfully first.
        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        using var cts = new CancellationTokenSource();

        var request = new HttpRequestMessage(
            HttpMethod.Post, "https://api.ibkr.com/v1/api/iserver/auth/ssodh/init");
        deps.SessionApi.OnInit = () => cts.Cancel();
        deps.SessionApi.InitException = new ApiRequestException(
            request, HttpMethod.Post, new RefitSettings(),
            new OperationCanceledException(cts.Token));

        await Should.ThrowAsync<OperationCanceledException>(
            () => manager.ReauthenticateAsync(cts.Token));
    }

    [Fact]
    public async Task EnsureInitializedAsync_WrappedTransportFailure_ThrowsTransient()
    {
        // Refit 11 wraps a transport failure (e.g. connection refused) thrown from the
        // raw Task<T> /ssodh/init call in an ApiRequestException whose InnerException is
        // the original HttpRequestException. The SessionManager must unwrap it so the
        // failure is classified as transient (retryable) rather than misreported as a
        // non-retryable configuration error.
        var deps = CreateDependencies();
        var inner = new HttpRequestException("connection refused");
        var request = new HttpRequestMessage(
            HttpMethod.Post, "https://api.ibkr.com/v1/api/iserver/auth/ssodh/init");
        deps.SessionApi.InitException = new ApiRequestException(
            request, HttpMethod.Post, new RefitSettings(), inner);

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

        var ex = await Should.ThrowAsync<IbkrTransientException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));

        ex.InnerException.ShouldBeSameAs(inner);
    }

    [Fact]
    public async Task EnsureInitializedAsync_WrappedTimeout_ThrowsTransient()
    {
        // Refit 11 wraps a request timeout (TaskCanceledException, with the caller's
        // token NOT cancelled) from the raw Task<T> /ssodh/init call in an
        // ApiRequestException. With an uncancelled token, RethrowIfWrappedCancellation
        // is a no-op, so the flow reaches WrapCredentialException, which must classify
        // the unwrapped TaskCanceledException as a transient timeout.
        var deps = CreateDependencies();
        var inner = new TaskCanceledException("timed out");
        var request = new HttpRequestMessage(
            HttpMethod.Post, "https://api.ibkr.com/v1/api/iserver/auth/ssodh/init");
        deps.SessionApi.InitException = new ApiRequestException(
            request, HttpMethod.Post, new RefitSettings(), inner);

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

        var ex = await Should.ThrowAsync<IbkrTransientException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));

        ex.InnerException.ShouldBeSameAs(inner);
    }

    [Fact]
    public async Task ReauthenticateAsync_WrappedTransportFailure_ThrowsTransient()
    {
        // Same Refit 11 wrapping applies to the reauth path's /ssodh/init call: a
        // wrapped transport failure must surface as a transient (retryable) error.
        var deps = CreateDependencies();

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

        // Initialize successfully first.
        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        var inner = new HttpRequestException("connection refused");
        var request = new HttpRequestMessage(
            HttpMethod.Post, "https://api.ibkr.com/v1/api/iserver/auth/ssodh/init");
        deps.SessionApi.InitException = new ApiRequestException(
            request, HttpMethod.Post, new RefitSettings(), inner);

        var ex = await Should.ThrowAsync<IbkrTransientException>(
            () => manager.ReauthenticateAsync(TestContext.Current.CancellationToken));

        ex.InnerException.ShouldBeSameAs(inner);
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
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

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
            new TenantContext("test"),
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

    [Fact]
    public async Task EnsureInitializedAsync_AfterFailedReauth_StopsLeakedTickleTimerAndReinits()
    {
        // SES-6 + SES-3: a failed reauth leaves the tickle timer running and (before the fix)
        // the state stranded off-Ready. The next consumer call re-enters EnsureInitializedAsync
        // and — without the fix — creates a SECOND tickle timer, leaking the first (a concurrent
        // duplicate tickle loop). The fix stops the old timer before creating the new one.
        var deps = CreateDependencies();

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);
        var firstTimer = deps.TickleTimerFactory.CreatedTimers[0];

        // Fail the reauth: the token refresh throws a transient error.
        deps.TokenProvider.RefreshException =
            new HttpRequestException("boom", null, HttpStatusCode.ServiceUnavailable);
        await Should.ThrowAsync<IbkrTransientException>(
            () => manager.ReauthenticateAsync(TestContext.Current.CancellationToken));

        // A later consumer call re-enters init cleanly (no permanent wedge).
        deps.TokenProvider.RefreshException = null;
        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        deps.TickleTimerFactory.CreateCount.ShouldBe(2, "Re-init after a failed reauth must create a new tickle timer.");
        var secondTimer = deps.TickleTimerFactory.CreatedTimers[1];
        secondTimer.ShouldNotBeSameAs(firstTimer);
        firstTimer.Stopped.ShouldBeTrue(
            "Re-init must stop the previous tickle timer so a failed-reauth cycle cannot leak a second live loop.");
        secondTimer.Started.ShouldBeTrue("The new tickle timer must be started.");
    }

    [Fact]
    public async Task InitFailedReauthReinit_ActiveSessionCount_NetsToZeroAfterDispose()
    {
        // SES-3/SES-6: a failed reauth must reset session state so the next call re-inits
        // cleanly, and the active-session gauge must count the tenant exactly once — never
        // double-incrementing on re-init, and always decrementing on dispose. The gauge is an
        // UpDownCounter, so its net over init → failed-reauth → re-init → dispose must be zero.
        var tenantId = "vcr06-active-" + Guid.NewGuid().ToString("N");
        long net = 0;
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == IbkrConduitDiagnostics.MeterName
                    && instrument.Name == "ibkr.conduit.session.active")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
        {
            foreach (var t in tags)
            {
                if (t.Key == LogFields.TenantId && (string?)t.Value == tenantId)
                {
                    Interlocked.Add(ref net, measurement);
                }
            }
        });
        listener.Start();

        var deps = CreateDependencies();
        var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance,
            new TenantContext(tenantId));

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken); // +1

        deps.TokenProvider.RefreshException =
            new HttpRequestException("boom", null, HttpStatusCode.ServiceUnavailable);
        await Should.ThrowAsync<IbkrTransientException>(
            () => manager.ReauthenticateAsync(TestContext.Current.CancellationToken)); // no gauge change

        deps.TokenProvider.RefreshException = null;
        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken); // must NOT double-count

        await manager.DisposeAsync(); // -1
        listener.Dispose();

        Interlocked.Read(ref net).ShouldBe(0,
            "The active-session gauge must net to zero: re-init after a failed reauth must not double-count, "
            + "and dispose must decrement exactly once.");
    }

    [Fact]
    public async Task ProactiveRefresh_TransientFailureThenSuccess_RetriesUntilSuccess()
    {
        // SES-5: proactive refresh must NOT be one-shot. A transient failure at the refresh
        // margin must be retried with backoff rather than abandoning the token to expiry.
        var fakeTime = new FakeTimeProvider();
        var deps = CreateDependencies();
        deps.TokenProvider = new FakeSessionTokenProvider(fakeTime)
        {
            TokenLifetime = TimeSpan.FromSeconds(10),
            SimulateAsyncRefresh = true,
        };
        deps.Options.ProactiveRefreshMargin = TimeSpan.FromSeconds(8); // timeUntilRefresh = 2s

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"),
            fakeTime);

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        // The first proactive refresh attempt throws a transient error; subsequent ones succeed.
        deps.TokenProvider.ThrowOnNextRefresh = true;

        // Pump the fake clock: 2s to reach the refresh point, then repeatedly past the retry
        // backoff so the retried attempt fires. The retry must eventually re-init the session.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (deps.SessionApi.InitCallCount < 2 && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(10));
            await Task.Yield();
        }

        await deps.SessionApi.SecondInitTask.WaitAsync(TestContext.Current.CancellationToken);

        deps.TokenProvider.RefreshCallCount.ShouldBeGreaterThanOrEqualTo(2,
            "A failed proactive refresh must be retried (at least the failed attempt + one retry).");
        deps.SessionApi.InitCallCount.ShouldBeGreaterThanOrEqualTo(2,
            "The retried proactive refresh must re-initialize the session.");
    }

    [Fact]
    public async Task ScheduleProactiveRefresh_TokenAlreadyDue_RefreshesImmediately()
    {
        // SES-5: when the freshly-acquired token is already inside the refresh margin, the
        // refresh must fire immediately instead of being silently skipped and left to expire.
        var fakeTime = new FakeTimeProvider();
        var deps = CreateDependencies();
        deps.TokenProvider = new FakeSessionTokenProvider(fakeTime)
        {
            TokenLifetime = TimeSpan.FromSeconds(5),      // initial token already inside the margin
            RefreshTokenLifetime = TimeSpan.FromHours(1), // refreshed token is long-lived (no tight loop)
        };
        deps.Options.ProactiveRefreshMargin = TimeSpan.FromSeconds(30); // timeUntilRefresh = 5 - 30 < 0

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"),
            fakeTime);

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        // The already-due refresh is scheduled with a zero delay; nudge the fake clock so the
        // zero-delay timer fires (the refreshed token is long-lived, so it fires exactly once).
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (deps.SessionApi.InitCallCount < 2 && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromMilliseconds(1));
            await Task.Yield();
        }

        await deps.SessionApi.SecondInitTask.WaitAsync(TestContext.Current.CancellationToken);

        deps.TokenProvider.RefreshCallCount.ShouldBeGreaterThanOrEqualTo(1,
            "An already-due token must refresh immediately, not be skipped.");
        deps.SessionApi.InitCallCount.ShouldBeGreaterThanOrEqualTo(2,
            "The immediate proactive refresh must re-initialize the session.");
    }

    [Fact]
    public async Task DisposeAsync_DuringInFlightProactiveReauth_CancelsAndDrainsReauth_NoLeakedLoop()
    {
        // CON-1: dispose racing an in-flight re-auth must cancel _disposeCts FIRST — so the re-auth
        // (running on the dispose token via the proactive-refresh loop) unwinds — and must AWAIT the
        // background loops to completion so nothing leaks past dispose. Under the pre-fix ordering,
        // dispose never cancelled the dispose token and never awaited RunProactiveRefreshAsync, so the
        // in-flight re-auth stayed blocked, later touched the disposed semaphore (ObjectDisposedException),
        // and the proactive-refresh loop leaked — the root cause of the intermittent tickle-watchdog hang.
        var fakeTime = new FakeTimeProvider();
        var deps = CreateDependencies();
        deps.TokenProvider = new FakeSessionTokenProvider(fakeTime)
        {
            TokenLifetime = TimeSpan.FromSeconds(10),      // initial token → refresh due in 2s
            RefreshTokenLifetime = TimeSpan.FromHours(1),  // refreshed token long-lived
        };
        deps.Options.ProactiveRefreshMargin = TimeSpan.FromSeconds(8); // timeUntilRefresh = 2s

        var reachedGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inFlightReauthWasCancelled = false;

        // Gate the re-auth's ssodh/init (the 2nd init) in-flight until dispose cancels its token.
        deps.SessionApi.InitGate = async (callNumber, ct) =>
        {
            if (callNumber < 2)
            {
                return;
            }

            reachedGate.TrySetResult();
            try
            {
                await releaseGate.Task.WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                inFlightReauthWasCancelled = true;
                throw;
            }
        };

        var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            deps.SessionHealthState,
            NullLogger<SessionManager>.Instance,
            new TenantContext("test"),
            fakeTime);

        try
        {
            await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

            // Fire the proactive refresh — it runs ReauthenticateAsync on the dispose token and
            // blocks inside the gated ssodh/init.
            fakeTime.Advance(TimeSpan.FromSeconds(2));
            await reachedGate.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            // Dispose while the re-auth is in-flight. Must complete promptly (bounded) and drain it.
            await manager.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            inFlightReauthWasCancelled.ShouldBeTrue(
                "dispose must cancel _disposeCts first (unwinding the in-flight re-auth) and await the "
                + "proactive-refresh loop, so no background reauth/tickle loop leaks past DisposeAsync.");
        }
        finally
        {
            // Release the gate so any residual wait unblocks for clean test teardown.
            releaseGate.TrySetResult();
        }
    }

    // ---- ADR-0004: competing-session truth & health evidence (VCR-07) ----

    [Fact]
    public async Task EnsureInitializedAsync_SsodhReturnsUnauthenticatedCompeting_ThrowsSessionErrorWithIsCompeting()
    {
        var deps = CreateDependencies();
        deps.SessionApi.InitResponse = new SsodhInitResponse(
            Authenticated: false, Connected: true, Competing: true, Established: false,
            Message: "competing session", Mac: null, ServerInfo: null, HardwareInfo: null);

        await using var manager = new SessionManager(
            deps.TokenProvider, deps.TickleTimerFactory, deps.SessionApi, deps.Options,
            deps.Notifier, deps.SessionHealthState, NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

        var ex = await Should.ThrowAsync<IbkrApiException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));

        var sessionError = ex.Error.ShouldBeOfType<IbkrSessionError>();
        sessionError.IsCompeting.ShouldBeTrue();

        // GAP3-2: health reflects the server verdict, not a laundered authenticated:true / competing:false.
        var snapshot = deps.SessionHealthState.GetSnapshot();
        snapshot.Authenticated.ShouldBeFalse();
        snapshot.Competing.ShouldBeTrue();
    }

    [Fact]
    public async Task ReauthenticateAsync_SsodhReturnsUnauthenticatedCompeting_ThrowsAndDoesNotReachReady()
    {
        var deps = CreateDependencies();

        await using var manager = new SessionManager(
            deps.TokenProvider, deps.TickleTimerFactory, deps.SessionApi, deps.Options,
            deps.Notifier, deps.SessionHealthState, NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

        // Bring the session up cleanly first.
        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        // Now a competing takeover: the reauth's ssodh reports authenticated=false, competing=true.
        deps.SessionApi.InitResponse = new SsodhInitResponse(
            Authenticated: false, Connected: true, Competing: true, Established: false,
            Message: null, Mac: null, ServerInfo: null, HardwareInfo: null);

        var ex = await Should.ThrowAsync<IbkrApiException>(
            () => manager.ReauthenticateAsync(TestContext.Current.CancellationToken));
        ex.Error.ShouldBeOfType<IbkrSessionError>().IsCompeting.ShouldBeTrue();

        var snapshot = deps.SessionHealthState.GetSnapshot();
        snapshot.Authenticated.ShouldBeFalse();
        snapshot.Competing.ShouldBeTrue();

        // Not Ready: the notifier (fired only on a successful reauth) never ran.
        deps.Notifier.NotifyCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ReauthenticateAsync_SsodhReportsCompeting_HealthSnapshotRetainsCompeting()
    {
        var deps = CreateDependencies();

        await using var manager = new SessionManager(
            deps.TokenProvider, deps.TickleTimerFactory, deps.SessionApi, deps.Options,
            deps.Notifier, deps.SessionHealthState, NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        // A tickle recorded competing:true just before reauth.
        deps.SessionHealthState.Update(authenticated: false, connected: true, competing: true, established: true);

        // The reauth "succeeds" (authenticated:true) but the server still reports competing:true.
        deps.SessionApi.InitResponse = new SsodhInitResponse(
            Authenticated: true, Connected: true, Competing: true, Established: true,
            Message: null, Mac: null, ServerInfo: null, HardwareInfo: null);

        await manager.ReauthenticateAsync(TestContext.Current.CancellationToken);

        // GAP3-2: reauth did not launder competing:true into competing:false.
        deps.SessionHealthState.GetSnapshot().Competing.ShouldBeTrue();
    }

    [Fact]
    public async Task ReauthenticateAsync_ServerReportsCompetingCleared_ClearsHealthCompeting()
    {
        var deps = CreateDependencies();

        await using var manager = new SessionManager(
            deps.TokenProvider, deps.TickleTimerFactory, deps.SessionApi, deps.Options,
            deps.Notifier, deps.SessionHealthState, NullLogger<SessionManager>.Instance,
            new TenantContext("test"));

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);
        deps.SessionHealthState.Update(authenticated: false, connected: true, competing: true, established: true);

        // The reauth wins the session back — server positively reports competing:false.
        deps.SessionApi.InitResponse = new SsodhInitResponse(
            Authenticated: true, Connected: true, Competing: false, Established: true,
            Message: null, Mac: null, ServerInfo: null, HardwareInfo: null);

        await manager.ReauthenticateAsync(TestContext.Current.CancellationToken);

        // A positive competing:false from the server clears the sticky verdict.
        deps.SessionHealthState.GetSnapshot().Competing.ShouldBeFalse();
    }

    [Fact]
    public async Task ReauthenticateAsync_CompeteFalseCompetingObserved_SpacesOutReauthAttempts()
    {
        var clock = new FakeTimeProvider();
        var deps = CreateDependencies();
        deps.TokenProvider = new FakeSessionTokenProvider(clock);
        deps.Options = new IbkrClientOptions { Compete = false };
        deps.SessionApi.InitResponse = new SsodhInitResponse(
            Authenticated: false, Connected: true, Competing: true, Established: false,
            Message: null, Mac: null, ServerInfo: null, HardwareInfo: null);

        await using var manager = new SessionManager(
            deps.TokenProvider, deps.TickleTimerFactory, deps.SessionApi, deps.Options,
            deps.Notifier, deps.SessionHealthState, NullLogger<SessionManager>.Instance,
            new TenantContext("test"), clock);

        // First reauth actually contacts ssodh and observes competing → arms the backoff, throws.
        await Should.ThrowAsync<IbkrApiException>(
            () => manager.ReauthenticateAsync(TestContext.Current.CancellationToken));
        deps.SessionApi.InitCallCount.ShouldBe(1);

        // A reauth arriving inside the backoff window (as the tickle loop would drive it) short-circuits
        // without a second ssodh call — no 5-second steal-back ping-pong.
        await manager.ReauthenticateAsync(TestContext.Current.CancellationToken);
        deps.SessionApi.InitCallCount.ShouldBe(1);

        // Once the backoff elapses, a reauth attempt runs again.
        clock.Advance(TimeSpan.FromSeconds(6));
        await Should.ThrowAsync<IbkrApiException>(
            () => manager.ReauthenticateAsync(TestContext.Current.CancellationToken));
        deps.SessionApi.InitCallCount.ShouldBe(2);
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

        /// <summary>
        /// Token lifetime for tokens produced by <see cref="RefreshAsync"/>. When null,
        /// <see cref="TokenLifetime"/> is used. Lets a test seed a short-lived initial token
        /// (already inside the refresh margin) while the refreshed token is long-lived, so an
        /// immediate proactive refresh fires exactly once instead of tight-looping.
        /// </summary>
        public TimeSpan? RefreshTokenLifetime { get; set; }

        /// <summary>If set, GetLiveSessionTokenAsync throws this exception.</summary>
        public Exception? GetException { get; set; }

        /// <summary>If set, RefreshAsync throws this exception.</summary>
        public Exception? RefreshException { get; set; }

        /// <summary>
        /// When true, the next call to <see cref="RefreshAsync"/> throws an
        /// <see cref="HttpRequestException"/> with status 503 (which
        /// <c>WrapCredentialException</c> turns into <see cref="IbkrTransientException"/>),
        /// and the flag auto-resets so subsequent refresh calls succeed.
        /// </summary>
        public bool ThrowOnNextRefresh { get; set; }

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
            if (ThrowOnNextRefresh)
            {
                ThrowOnNextRefresh = false;
                throw new HttpRequestException(
                    "Simulated transient failure",
                    inner: null,
                    statusCode: HttpStatusCode.ServiceUnavailable);
            }

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

            _lastExpiry = _timeProvider.GetUtcNow().Add(RefreshTokenLifetime ?? TokenLifetime);
            return new LiveSessionToken(
                new byte[] { 0x04, 0x05, 0x06 },
                _lastExpiry.Value);
        }
    }

    internal class FakeTickleTimerFactory : ITickleTimerFactory
    {
        public int CreateCount { get; private set; }
        public FakeTickleTimer? CreatedTimer { get; private set; }

        /// <summary>Every timer this factory has created, in creation order.</summary>
        public List<FakeTickleTimer> CreatedTimers { get; } = new();

        public ITickleTimer Create(
            IIbkrSessionApi sessionApi,
            Func<CancellationToken, Task> onFailure)
        {
            CreateCount++;
            CreatedTimer = new FakeTickleTimer();
            CreatedTimers.Add(CreatedTimer);
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

        /// <summary>
        /// If set, InitializeBrokerageSessionAsync returns this response instead of the default
        /// authenticated:true body. Lets a test drive the ADR-0004 authenticated=false / competing paths.
        /// </summary>
        public SsodhInitResponse? InitResponse { get; set; }

        /// <summary>If set, SuppressQuestionsAsync throws this exception.</summary>
        public Exception? SuppressException { get; set; }

        /// <summary>
        /// If set, invoked at the start of <see cref="InitializeBrokerageSessionAsync"/>
        /// before <see cref="InitException"/> is thrown. Lets a test simulate caller
        /// cancellation occurring during the in-flight init call.
        /// </summary>
        public Action? OnInit { get; set; }

        /// <summary>Completes when <see cref="InitCallCount"/> reaches 2 (first re-auth).</summary>
        public Task SecondInitTask => _secondInitTcs.Task;

        /// <summary>
        /// If set, awaited inside <see cref="InitializeBrokerageSessionAsync"/> before the response is
        /// returned, receiving the current call number and the call's <see cref="CancellationToken"/>.
        /// Lets a test hold a re-auth's ssodh/init call in-flight (blocked) so dispose can be observed
        /// racing against it (CON-1). Default null → no gating.
        /// </summary>
        public Func<int, CancellationToken, Task>? InitGate { get; set; }

        public async Task<SsodhInitResponse> InitializeBrokerageSessionAsync(SsodhInitRequest request, CancellationToken cancellationToken = default)
        {
            InitCallCount++;
            LastInitRequest = request;
            if (InitCallCount >= 2)
            {
                _secondInitTcs.TrySetResult();
            }

            OnInit?.Invoke();

            if (InitException != null)
            {
                throw InitException;
            }

            if (InitGate != null)
            {
                await InitGate(InitCallCount, cancellationToken);
            }

            if (InitResponse != null)
            {
                return InitResponse;
            }

            return new SsodhInitResponse(Authenticated: true, Connected: true, Competing: false, Established: true, Message: null, Mac: null, ServerInfo: null, HardwareInfo: null);
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
            if (SuppressException != null)
            {
                throw SuppressException;
            }

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
