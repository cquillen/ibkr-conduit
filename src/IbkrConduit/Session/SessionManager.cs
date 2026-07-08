using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
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
using Microsoft.Extensions.Logging;
using Refit;

namespace IbkrConduit.Session;

/// <summary>
/// Orchestrates brokerage session lifecycle: lazy initialization, periodic tickle,
/// re-authentication on failure, and clean shutdown.
/// </summary>
internal sealed partial class SessionManager : ISessionManager
{
    private static readonly UpDownCounter<long> _activeSessionCount =
        IbkrConduitDiagnostics.Meter.CreateUpDownCounter<long>("ibkr.conduit.session.active");

    private static readonly Histogram<double> _initDuration =
        IbkrConduitDiagnostics.Meter.CreateHistogram<double>("ibkr.conduit.session.initialize.duration", "ms");

    private static readonly Counter<long> _refreshCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.session.refresh.count");

    private static readonly Histogram<double> _refreshDuration =
        IbkrConduitDiagnostics.Meter.CreateHistogram<double>("ibkr.conduit.session.refresh.duration", "ms");

    /// <summary>Initial delay between proactive-refresh retry attempts after a transient failure.</summary>
    private static readonly TimeSpan _proactiveRefreshRetryInitialBackoff = TimeSpan.FromSeconds(5);

    /// <summary>Ceiling for the exponential proactive-refresh retry backoff.</summary>
    private static readonly TimeSpan _proactiveRefreshRetryMaxBackoff = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Initial spacing between re-auth attempts once a competing session is observed with
    /// <see cref="IbkrClientOptions.Compete"/> false (ADR-0004). Without this, a lost compete has the
    /// tickle loop re-firing re-auth every failure interval (~5 s) — two <c>Compete=true</c> processes
    /// ping-pong the session. The spacing grows exponentially up to <see cref="_competeReauthMaxBackoff"/>.
    /// </summary>
    private static readonly TimeSpan _competeReauthInitialBackoff = TimeSpan.FromSeconds(5);

    /// <summary>Ceiling for the exponential compete-backoff spacing between re-auth attempts.</summary>
    private static readonly TimeSpan _competeReauthMaxBackoff = TimeSpan.FromMinutes(5);

    /// <summary>Path reported on the session error raised when ssodh/init returns <c>authenticated=false</c>.</summary>
    private const string _ssodhInitPath = "/v1/api/iserver/auth/ssodh/init";

    private readonly ISessionTokenProvider _sessionTokenProvider;
    private readonly ITickleTimerFactory _tickleTimerFactory;
    private readonly IIbkrSessionApi _sessionApi;
    private readonly IbkrClientOptions _options;
    private readonly ISessionLifecycleNotifier _notifier;
    private readonly SessionHealthState _sessionHealthState;
    private readonly ILogger<SessionManager> _logger;
    private readonly TenantContext _tenant;
    private readonly Dictionary<string, object> _logScope;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();

    /// <summary>
    /// Monotonically incremented after every successful reauth. Used by
    /// <see cref="ReauthenticateAsync"/> to dedupe concurrent calls from a
    /// burst of 401s: a caller snapshots the epoch before waiting on the
    /// semaphore and short-circuits if the snapshot no longer matches —
    /// meaning another caller already completed a reauth. See issue #168.
    /// </summary>
    private long _reauthEpoch;

    private volatile SessionState _state = SessionState.Uninitialized;
    private volatile bool _disposed;

    /// <summary>
    /// Whether the session was ever brought fully up (reached <see cref="SessionState.Ready"/>),
    /// so the active-session gauge was incremented and a logout is warranted on dispose. Tracked
    /// separately from <see cref="_state"/> because a failed reauth resets the state machine to
    /// <see cref="SessionState.Uninitialized"/> (SES-3) without un-establishing the session, and a
    /// clean re-init must not double-count the gauge (SES-6).
    /// </summary>
    private volatile bool _sessionEstablished;
    private readonly object _proactiveRefreshLock = new();
    private ITickleTimer? _tickleTimer;
    private CancellationTokenSource? _proactiveRefreshCts;

    /// <summary>
    /// The currently-scheduled proactive-refresh loop, captured (not fire-and-forget) so
    /// <see cref="DisposeAsync"/> can await it to completion — a leaked <see cref="RunProactiveRefreshAsync"/>
    /// loop (or the re-auth it is mid-flight on) outliving dispose is CON-1's leaked-loop window.
    /// Mutated only under <see cref="_proactiveRefreshLock"/>.
    /// </summary>
    private Task? _proactiveRefreshTask;
    private LiveSessionToken? _currentLst;

    /// <summary>
    /// Current compete-backoff spacing. Grows exponentially each time a re-auth observes a competing
    /// session under <c>Compete=false</c>; reset to <see cref="_competeReauthInitialBackoff"/> on a
    /// successful re-auth. Mutated only under <see cref="_semaphore"/>.
    /// </summary>
    private TimeSpan _competeReauthBackoff = _competeReauthInitialBackoff;

    /// <summary>
    /// Earliest time the next compete-backed-off re-auth may actually contact ssodh. Re-auth calls
    /// arriving before this (from the tickle failure loop) short-circuit without hammering the server.
    /// Mutated only under <see cref="_semaphore"/>.
    /// </summary>
    private DateTimeOffset _competeReauthNextAllowed = DateTimeOffset.MinValue;

    /// <summary>
    /// Creates a new session manager.
    /// </summary>
    public SessionManager(
        ISessionTokenProvider sessionTokenProvider,
        ITickleTimerFactory tickleTimerFactory,
        IIbkrSessionApi sessionApi,
        IbkrClientOptions options,
        ISessionLifecycleNotifier notifier,
        SessionHealthState sessionHealthState,
        ILogger<SessionManager> logger,
        TenantContext tenant,
        TimeProvider? timeProvider = null)
    {
        _sessionTokenProvider = sessionTokenProvider;
        _tickleTimerFactory = tickleTimerFactory;
        _sessionApi = sessionApi;
        _options = options;
        _notifier = notifier;
        _sessionHealthState = sessionHealthState;
        _logger = logger;
        _tenant = tenant;
        _logScope = new Dictionary<string, object> { [LogFields.TenantId] = _tenant.TenantId };
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_state == SessionState.Ready)
        {
            return;
        }

        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Session.Initialize");
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
        using var _ = _logger.BeginScope(_logScope);

        await _semaphore.WaitAsync(cancellationToken);
        var sw = Stopwatch.StartNew();
        try
        {
            if (_state == SessionState.Ready)
            {
                return;
            }

            _state = SessionState.Initializing;
            LogInitializing();

            try
            {
                SsodhInitResponse initResponse;
                try
                {
                    _currentLst = await _sessionTokenProvider.GetLiveSessionTokenAsync(cancellationToken);

                    initResponse = await _sessionApi.InitializeBrokerageSessionAsync(
                        new SsodhInitRequest(Publish: true, Compete: _options.Compete), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (ApiRequestException ex)
                {
                    // Refit 11 wraps caller cancellation from raw Task<T> session calls in
                    // ApiRequestException; unwrap so cancellation is not misreported as a credential error.
                    ex.RethrowIfWrappedCancellation(cancellationToken);
                    // Refit 11 also wraps transport failures (network error / timeout) in
                    // ApiRequestException. Unwrap to the original SendAsync exception so the
                    // type-switch in WrapCredentialException classifies it correctly (e.g.
                    // transient) rather than falling through to the configuration-error default.
                    throw WrapCredentialException(ex.InnerException ?? ex);
                }
                catch (Exception ex)
                {
                    throw WrapCredentialException(ex);
                }

                // SES-1/ADR-0004: a 200 ssodh/init with authenticated=false is a FAILED init, not
                // success — the session never reached the brokerage bridge. Surface it as a session
                // error carrying the server's competing verdict (no Ready transition, no tickle start).
                if (!initResponse.Authenticated)
                {
                    throw BuildUnauthenticatedInitError(initResponse);
                }

                if (_options.SuppressMessageIds.Count > 0)
                {
                    try
                    {
                        await _sessionApi.SuppressQuestionsAsync(
                            new SuppressRequest(_options.SuppressMessageIds), cancellationToken);
                    }
                    catch (ApiRequestException ex)
                    {
                        // Refit 11 wraps the original SendAsync exception; re-throw it so callers observe the same
                        // exception type (transport/timeout/cancellation) they did under Refit 10.
                        ex.RethrowOriginal();
                        throw; // unreachable unless InnerException is null
                    }
                }

                // GAP3-2/ADR-0004: health is fed from the server response, never a literal — a
                // competing:true reported here is not laundered into competing:false.
                _sessionHealthState.Update(
                    authenticated: true,
                    connected: initResponse.Connected,
                    competing: initResponse.Competing,
                    established: initResponse.Established);

                // CON-1 / SES-2: if dispose began while this init was awaiting ssodh/init, do NOT start
                // a tickle timer. DisposeAsync sets _state = ShuttingDown (and _disposed) and cancels
                // _disposeCts before parking on the semaphore barrier. The timer created below is tied
                // to _disposeCts (SES-2 — its lifetime is the session's, not the initializing caller's),
                // so a timer started after that cancel would begin already-cancelled and stop on its
                // own; the ShuttingDown short-circuit still avoids the wasted create/start and the
                // active-session gauge races. Mirrors the ShuttingDown short-circuit in
                // ReauthenticateAsync. DisposeAsync's post-barrier StopAsync remains the airtight
                // backstop for the residual race where this check passes just before dispose flips
                // the state.
                if (_disposed || _state == SessionState.ShuttingDown)
                {
                    return;
                }

                // Stop any prior tickle timer before creating a new one. A failed reauth leaves the
                // old timer running (reauth intentionally does not stop it — deadlock hazard); re-init
                // must tear it down here so a failed-reauth→re-init cycle cannot leak a second live
                // tickle loop (SES-6). Safe here because EnsureInitializedAsync is never invoked from
                // the tickle's own failure callback (that path is ReauthenticateAsync).
                if (_tickleTimer != null)
                {
                    await _tickleTimer.StopAsync();
                }

                _tickleTimer = _tickleTimerFactory.Create(_sessionApi, OnTickleFailureAsync);

                // SES-2 (PVR-12): the keepalive loop's lifetime is the session's, NOT the initializing
                // caller's. Tie it to _disposeCts — not the consumer's cancellationToken — so a caller
                // that cancels/disposes its token after init (e.g. a bounded startup CTS) does not
                // silently stop keepalive and rot the session. Only DisposeAsync (which cancels
                // _disposeCts and awaits StopAsync) ends the loop.
                await _tickleTimer.StartAsync(_disposeCts.Token);

                ScheduleProactiveRefresh();

                _state = SessionState.Ready;

                // Count the tenant on the active-session gauge exactly once, even across a
                // failed-reauth→re-init cycle (SES-6: no duplicate increment).
                if (!_sessionEstablished)
                {
                    _sessionEstablished = true;
                    _activeSessionCount.Add(1,
                        new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId));
                }

                _initDuration.Record(sw.Elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId));
                LogInitialized();
            }
            catch
            {
                // SES-3: a failed init must not strand the state machine off-Ready. Reset so the
                // next consumer call re-enters init cleanly instead of taking the slow path forever.
                _state = SessionState.Uninitialized;
                throw;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task ReauthenticateAsync(CancellationToken cancellationToken)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Session.Reauthenticate");
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
        using var _ = _logger.BeginScope(_logScope);

        // Snapshot the reauth epoch before waiting on the semaphore. If
        // another caller completes a reauth while we wait, they will
        // increment the epoch — we'll detect that after acquiring the
        // semaphore and short-circuit. See issue #168.
        var epochBeforeWait = Interlocked.Read(ref _reauthEpoch);

        // Yield once before contending for the semaphore so that concurrent
        // callers in the same burst — including ones with all-synchronous
        // dependencies (e.g., unit tests) — get to snapshot the epoch before
        // any of them progresses past the semaphore. Without this yield, a
        // burst dispatched from a single thread runs strictly sequentially:
        // caller A would complete (incrementing the epoch) before caller B
        // even reads it, and the dedupe would never trigger.
        await Task.Yield();

        await _semaphore.WaitAsync(cancellationToken);
        var sw = Stopwatch.StartNew();
        try
        {
            if (_state == SessionState.ShuttingDown)
            {
                return;
            }

            // Dedupe: if the epoch advanced while we were waiting, another
            // caller already completed a full reauth — the session is back
            // to Ready and there is nothing to do.
            if (Interlocked.Read(ref _reauthEpoch) != epochBeforeWait)
            {
                return;
            }

            // ADR-0004 compete backoff: after a competing session was observed with Compete=false,
            // space out the actual re-auth attempts instead of hammering ssodh every tickle-failure
            // interval. The tickle loop still fires; these excess calls short-circuit here so only one
            // real re-auth runs per (growing) backoff window — no 5-second steal-back ping-pong.
            if (!_options.Compete && _timeProvider.GetUtcNow() < _competeReauthNextAllowed)
            {
                LogCompeteBackoffSkip();
                return;
            }

            _state = SessionState.Reauthenticating;
            LogReauthenticating();

            // Intentionally NOT stopping the existing tickle timer here.
            // ReauthenticateAsync can be invoked from the tickle's own failure
            // callback; awaiting StopAsync from within that callback chain
            // would deadlock (StopAsync awaits the background task, which is
            // awaiting the callback, which is awaiting StopAsync). The OAuth
            // signing layer reads the new LST as soon as SessionTokenProvider
            // updates it below, so the next tickle cycle uses the refreshed
            // credentials automatically — no restart needed.
            //
            // We also intentionally do NOT cancel a pending proactive refresh here.
            // A successful reauth reschedules it on the success path below; a failed
            // one must keep its retry slot alive so proactive refresh survives a
            // transient failure rather than being cancelled mid-flight (SES-5).

            try
            {
                SsodhInitResponse initResponse;
                try
                {
                    _currentLst = await _sessionTokenProvider.RefreshAsync(cancellationToken);

                    initResponse = await _sessionApi.InitializeBrokerageSessionAsync(
                        new SsodhInitRequest(Publish: true, Compete: _options.Compete), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (ApiRequestException ex)
                {
                    // Refit 11 wraps caller cancellation from raw Task<T> session calls in
                    // ApiRequestException; unwrap so cancellation is not misreported as a credential error.
                    ex.RethrowIfWrappedCancellation(cancellationToken);
                    // Refit 11 also wraps transport failures (network error / timeout) in
                    // ApiRequestException. Unwrap to the original SendAsync exception so the
                    // type-switch in WrapCredentialException classifies it correctly (e.g.
                    // transient) rather than falling through to the configuration-error default.
                    throw WrapCredentialException(ex.InnerException ?? ex);
                }
                catch (Exception ex)
                {
                    throw WrapCredentialException(ex);
                }

                // SES-1/ADR-0004: authenticated=false is a FAILED reauth (no Ready, no epoch bump).
                // If the server reports competing under Compete=false, arm the backoff so subsequent
                // tickle-driven reauth attempts space out instead of looping at the failure interval.
                if (!initResponse.Authenticated)
                {
                    if (!_options.Compete && initResponse.Competing)
                    {
                        _competeReauthNextAllowed = _timeProvider.GetUtcNow() + _competeReauthBackoff;
                        _competeReauthBackoff = TimeSpan.FromTicks(
                            Math.Min(_competeReauthBackoff.Ticks * 2, _competeReauthMaxBackoff.Ticks));
                    }

                    throw BuildUnauthenticatedInitError(initResponse);
                }

                if (_options.SuppressMessageIds.Count > 0)
                {
                    try
                    {
                        await _sessionApi.SuppressQuestionsAsync(
                            new SuppressRequest(_options.SuppressMessageIds), cancellationToken);
                    }
                    catch (ApiRequestException ex)
                    {
                        // Refit 11 wraps the original SendAsync exception; re-throw it so callers observe the same
                        // exception type (transport/timeout/cancellation) they did under Refit 10.
                        ex.RethrowOriginal();
                        throw; // unreachable unless InnerException is null
                    }
                }

                // GAP3-2/ADR-0004: fed from the server response, never a literal competing:false —
                // a competing verdict a tickle just recorded is only cleared by a server response
                // that positively reports competing:false. Reset the backoff on a clean reauth.
                _sessionHealthState.Update(
                    authenticated: true,
                    connected: initResponse.Connected,
                    competing: initResponse.Competing,
                    established: initResponse.Established);
                _competeReauthBackoff = _competeReauthInitialBackoff;
                _competeReauthNextAllowed = DateTimeOffset.MinValue;

                // No tickle timer recreate either — see the comment above. The existing timer keeps
                // running. ScheduleProactiveRefresh replaces any pending proactive-refresh slot.
                ScheduleProactiveRefresh();

                await _notifier.NotifyAsync(cancellationToken);

                _state = SessionState.Ready;
                _refreshCount.Add(1,
                    new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId),
                    new KeyValuePair<string, object?>(LogFields.Trigger, "reauth"));
                _refreshDuration.Record(sw.Elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId));

                // Mark the reauth complete BEFORE releasing the semaphore so any
                // caller queued behind us short-circuits via the epoch check above.
                Interlocked.Increment(ref _reauthEpoch);

                LogReauthenticated();
            }
            catch
            {
                // SES-3: a failed reauth must not strand the state machine at Reauthenticating.
                // Reset so the next consumer call (or the surviving tickle loop / proactive-refresh
                // retry) re-enters init cleanly instead of wedging on the stale credentials.
                _state = SessionState.Uninitialized;
                throw;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Session.Shutdown");
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);

        _state = SessionState.ShuttingDown;

        // CON-1: cancel the dispose token FIRST, before touching the tickle timer, the
        // proactive-refresh loop, or the semaphore. Every internal background operation that runs
        // on this token — the proactive-refresh loop and the re-auth it drives — begins unwinding
        // immediately. Without this first-cancel, an in-flight re-auth kept running past dispose,
        // later touched the disposed semaphore (ObjectDisposedException) and left a live refresh
        // loop behind: the leaked-loop window behind the intermittent tickle-watchdog hang.
        _disposeCts.Cancel();

        // Stop and AWAIT the tickle loop (which drains any tickle-triggered re-auth, cancelled via
        // the tickle timer's own token).
        if (_tickleTimer != null)
        {
            await _tickleTimer.StopAsync();
        }

        // Cancel the proactive-refresh slot, then serialize with any still-in-flight re-auth/init by
        // acquiring the semaphore before AWAITing the proactive loop and tearing anything down. Once
        // held, no re-auth/init can be inside its critical section (so disposing the semaphore cannot
        // throw ObjectDisposedException into one) and no success path can schedule a fresh refresh
        // loop. The dispose token is already cancelled, so an internal re-auth holding or waiting on
        // the semaphore unwinds promptly and releases it — this acquisition cannot deadlock against it.
        CancelProactiveRefresh();
        await _semaphore.WaitAsync(CancellationToken.None);

        // CON-1: a FIRST init that raced this dispose could have created and started a NEW tickle
        // timer AFTER the pre-barrier stop above ran (which was a no-op because _tickleTimer was
        // still null while that init was mid-flight). Now that the semaphore is held, no init/reauth
        // is inside its critical section, so _tickleTimer references whatever a racing init started;
        // stop it. Idempotent and null-safe — a no-op when nothing new was started or the timer is
        // already stopped. The just-started timer has no callback in-flight, so this stops promptly
        // (no deadlock), and it runs before _semaphore/_disposeCts are disposed, so it cannot throw
        // ObjectDisposedException. This must NOT replace the pre-barrier stop: a tickle-callback
        // reauth can hold the semaphore, so the pre-barrier stop is still needed to AWAIT the tickle
        // background task to completion (draining that reauth — now cancelled via the SES-2 tickle
        // token's link to _disposeCts) and let the WaitAsync above proceed.
        if (_tickleTimer != null)
        {
            await _tickleTimer.StopAsync();
        }

        Task? proactiveRefreshTask;
        lock (_proactiveRefreshLock)
        {
            proactiveRefreshTask = _proactiveRefreshTask;
            _proactiveRefreshTask = null;
        }

        if (proactiveRefreshTask != null)
        {
            // The loop runs on the (now-cancelled) dispose token and never re-enters the semaphore
            // once cancelled, so awaiting it while holding the semaphore is safe — it cannot leak
            // past dispose.
            await proactiveRefreshTask;
        }

        // Read whether the session was ever established AFTER draining every in-flight operation and
        // acquiring the semaphore, so a dispose racing an in-flight init sees the settled value (SES-6):
        // the gauge decrement and the logout track exactly whether the session actually came up. Keyed
        // off _sessionEstablished, not _state, because a failed reauth resets _state to Uninitialized
        // without un-establishing the session.
        var wasInitialized = _sessionEstablished;

        if (wasInitialized)
        {
            _activeSessionCount.Add(-1,
                new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId));
        }

        // The manager path issues its own single bounded logout in ManagedTenant.DisposeAsync,
        // so it suppresses this one to avoid a duplicate (VCR-08 / MGR-1). The single-account
        // path leaves the flag false and still logs out here on dispose.
        if (wasInitialized && !_options.SkipLogoutOnDispose)
        {
            try
            {
                await _sessionApi.LogoutAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                LogLogoutFailed(ex);
            }
        }

        _semaphore.Dispose();
        _disposeCts.Dispose();

        GC.SuppressFinalize(this);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Initializing brokerage session")]
    private partial void LogInitializing();

    [LoggerMessage(Level = LogLevel.Information, Message = "Brokerage session initialized successfully")]
    private partial void LogInitialized();

    [LoggerMessage(Level = LogLevel.Information, Message = "Re-authenticating brokerage session")]
    private partial void LogReauthenticating();

    [LoggerMessage(Level = LogLevel.Information, Message = "Brokerage session re-authenticated successfully")]
    private partial void LogReauthenticated();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Logout failed during shutdown — ignoring")]
    private partial void LogLogoutFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tickle failure detected — triggering re-authentication")]
    private partial void LogTickleFailure();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Brokerage session initialization reported authenticated=false (competing={Competing}) — treating as failed")]
    private partial void LogInitNotAuthenticated(bool competing);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Re-authentication skipped — competing session backoff in effect (Compete=false)")]
    private partial void LogCompeteBackoffSkip();

    [LoggerMessage(Level = LogLevel.Error, Message = "Proactive refresh failed")]
    private partial void LogProactiveRefreshFailed(Exception exception);

    private Task OnTickleFailureAsync(CancellationToken cancellationToken)
    {
        LogTickleFailure();
        return ReauthenticateAsync(cancellationToken);
    }

    /// <summary>
    /// Feeds the failed-auth server verdict into health state and builds the session error that
    /// carries the competing evidence (ADR-0004: SES-1 honesty + GAP3-1 truthful <c>IsCompeting</c>).
    /// The caller throws the returned exception so the failure path is explicit.
    /// </summary>
    private IbkrApiException BuildUnauthenticatedInitError(SsodhInitResponse response)
    {
        LogInitNotAuthenticated(response.Competing);

        _sessionHealthState.Update(
            authenticated: false,
            connected: response.Connected,
            competing: response.Competing,
            established: response.Established,
            failReason: response.Message ?? "Brokerage session initialization returned authenticated=false");

        return new IbkrApiException(new IbkrSessionError(
            HttpStatusCode.Unauthorized,
            response.Message ?? "Brokerage session initialization failed — server reported authenticated=false",
            RawBody: null,
            RequestPath: _ssodhInitPath,
            IsCompeting: response.Competing));
    }

    private void ScheduleProactiveRefresh()
    {
        if (_currentLst == null)
        {
            return;
        }

        CancelProactiveRefresh();

        var timeUntilRefresh = _currentLst.Expiry - _timeProvider.GetUtcNow() - _options.ProactiveRefreshMargin;
        if (timeUntilRefresh < TimeSpan.Zero)
        {
            // SES-5: an already-due token must refresh immediately, not be skipped —
            // skipping lets the LST run to expiry and wedges the session.
            timeUntilRefresh = TimeSpan.Zero;
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);
        lock (_proactiveRefreshLock)
        {
            _proactiveRefreshCts = cts;

            // Call the async method directly (not via Task.Run) so the Task.Delay timer
            // is registered synchronously before this method returns. With Task.Run, the
            // body is queued to the thread pool and may not register its timer before a
            // test that advances a FakeTimeProvider, causing a missed-Advance hang.
            //
            // Capture the running loop under the lock so DisposeAsync can await it to
            // completion (CON-1) — a fire-and-forget task would let the loop, and any
            // re-auth it is mid-flight on, outlive dispose.
            _proactiveRefreshTask = RunProactiveRefreshAsync(timeUntilRefresh, cts.Token);
        }
    }

    private async Task RunProactiveRefreshAsync(TimeSpan timeUntilRefresh, CancellationToken slotToken)
    {
        try
        {
            await Task.Delay(timeUntilRefresh, _timeProvider, slotToken);

            // SES-5: retry with exponential backoff until success, dispose, or supersession.
            // A single transient failure must not abandon the token to expiry (which would then
            // wedge the session via SES-2/SES-3). A successful ReauthenticateAsync reschedules a
            // fresh proactive-refresh slot and cancels this one, so we return on success.
            var backoff = _proactiveRefreshRetryInitialBackoff;
            while (!slotToken.IsCancellationRequested && !_disposeCts.IsCancellationRequested)
            {
                var succeeded = false;
                try
                {
                    // Re-auth on the dispose token, not the slot token: a successful reauth
                    // reschedules proactive refresh, which cancels this slot — passing the slot
                    // token here would cancel the very operation we are performing.
                    await ReauthenticateAsync(_disposeCts.Token);
                    succeeded = true;
                }
                catch (OperationCanceledException)
                {
                    // Shutdown or supersession — handled by the outer catch.
                    throw;
                }
                catch (Exception ex)
                {
                    LogProactiveRefreshFailed(ex);
                }

                if (succeeded)
                {
                    return;
                }

                await Task.Delay(backoff, _timeProvider, slotToken);
                backoff = backoff < _proactiveRefreshRetryMaxBackoff
                    ? TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, _proactiveRefreshRetryMaxBackoff.Ticks))
                    : _proactiveRefreshRetryMaxBackoff;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown or when a newer schedule supersedes this slot.
        }
        catch (Exception ex)
        {
            LogProactiveRefreshFailed(ex);
        }
    }

    private void CancelProactiveRefresh()
    {
        CancellationTokenSource? cts;
        lock (_proactiveRefreshLock)
        {
            cts = _proactiveRefreshCts;
            _proactiveRefreshCts = null;
        }

        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    internal static Exception WrapCredentialException(Exception ex) =>
        ex switch
        {
            // AUT-4: an LST-validation failure implicates the fields that derive and validate the token —
            // ConsumerKey, EncryptionPrivateKey (via the decrypted access-token secret), and DhPrime — NOT
            // SignaturePrivateKey (which only signs the LST request; a bad signing key is rejected upstream
            // as an HTTP 401 and never reaches the local signature comparison). Matched by type and placed
            // first so the "signature" wording in its message cannot fall through to the "sign" branch below.
            LiveSessionTokenValidationException lve =>
                new IbkrConfigurationException(
                    "Live Session Token validation failed — the token derived from the OAuth handshake does not "
                    + "match IBKR's signature. Verify ConsumerKey, EncryptionPrivateKey, EncryptedAccessTokenSecret, "
                    + "and DhPrime (SignaturePrivateKey is not implicated — a bad signing key is rejected upstream as HTTP 401)",
                    "ConsumerKey, EncryptionPrivateKey, EncryptedAccessTokenSecret, DhPrime", lve),

            CryptographicException ce when ce.Message.Contains("decrypt", StringComparison.OrdinalIgnoreCase) =>
                new IbkrConfigurationException(
                    "Failed to decrypt access token secret — verify EncryptionPrivateKey matches the key registered in the IBKR portal",
                    "EncryptionPrivateKey", ce),

            CryptographicException ce when ce.Message.Contains("sign", StringComparison.OrdinalIgnoreCase) =>
                new IbkrConfigurationException(
                    "RSA signature failed — verify SignaturePrivateKey matches the key registered in the IBKR portal",
                    "SignaturePrivateKey", ce),

            CryptographicException ce =>
                new IbkrConfigurationException(
                    "Cryptographic operation failed during session initialization — verify SignaturePrivateKey and EncryptionPrivateKey",
                    "SignaturePrivateKey, EncryptionPrivateKey", ce),

            HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } he =>
                new IbkrConfigurationException(
                    "LST acquisition rejected by IBKR — verify ConsumerKey and AccessToken are correct and not expired",
                    "ConsumerKey, AccessToken", he),

            HttpRequestException he =>
                new IbkrTransientException(
                    $"IBKR API returned a transient error ({(he.StatusCode is { } sc ? ((int)sc).ToString(CultureInfo.InvariantCulture) : "no response")}) — retry expected",
                    he),

            TaskCanceledException tce =>
                new IbkrTransientException(
                    "IBKR API request timed out before a response was received — retry expected",
                    tce),

            FormatException fe =>
                new IbkrConfigurationException(
                    "Diffie-Hellman key exchange produced invalid data — verify DhPrime is the correct RFC 3526 Group 14 prime",
                    "DhPrime", fe),

            InvalidOperationException ioe =>
                new IbkrConfigurationException(
                    "Diffie-Hellman key exchange failed — verify DhPrime is the correct RFC 3526 Group 14 prime",
                    "DhPrime", ioe),

            JsonException je =>
                new IbkrConfigurationException(
                    "Unexpected response format from IBKR LST endpoint — the API may be experiencing issues or the endpoint URL may be incorrect",
                    "BaseUrl", je),

            _ => new IbkrConfigurationException(
                $"Session initialization failed: {ex.Message}",
                null, ex),
        };

    private enum SessionState
    {
        Uninitialized,
        Initializing,
        Ready,
        Reauthenticating,
        ShuttingDown,
    }
}
