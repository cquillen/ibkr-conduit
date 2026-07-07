using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Diagnostics;
using IbkrConduit.Errors;
using IbkrConduit.Health;
using Microsoft.Extensions.Logging;
using Refit;

namespace IbkrConduit.Session;

/// <summary>
/// Sends tickle requests at a fixed interval using <see cref="TimeProvider"/> for testability.
/// When the session is detected as unauthenticated or the tickle call fails,
/// the failure callback is invoked to trigger re-authentication.
/// </summary>
internal sealed partial class TickleTimer : ITickleTimer
{
    private static readonly Counter<long> _tickleCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.session.tickle.count");

    private static readonly Counter<long> _tickleFailureCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.session.tickle.failure.count");

    /// <summary>
    /// Number of consecutive transport-level tickle failures after which the session is
    /// marked unauthenticated in health state. Transport failures are not, on their own,
    /// session-dead signals (reauth needs the same network), but health must not report a
    /// session live on stale evidence forever (SES-2).
    /// </summary>
    private const int _maxConsecutiveTransportFailuresBeforeUnhealthy = 3;

    private readonly IIbkrSessionApi _sessionApi;
    private readonly Func<CancellationToken, Task> _onFailure;
    private readonly SessionHealthState _sessionHealthState;
    private readonly ILogger<TickleTimer> _logger;
    private readonly ISessionLifecycleNotifier _notifier;
    private readonly TenantContext _tenant;
    private readonly Dictionary<string, object> _logScope;
    private readonly int _healthyIntervalSeconds;
    private readonly int _failureIntervalSeconds;
    private readonly TimeProvider _timeProvider;
    private readonly object _startStopLock = new();
    private CancellationTokenSource? _cts;
    private Task? _backgroundTask;

    /// <summary>
    /// Tracks the previous tickle's outcome to drive cadence selection. Initialized
    /// to <c>true</c> so the first iteration uses the healthy interval — we have no
    /// reason to fast-poll before observing a failure.
    /// </summary>
    private bool _lastTickleSucceeded = true;

    /// <summary>
    /// Count of consecutive transport-level tickle failures. Reset to zero whenever a
    /// tickle receives any HTTP response (transport is working). Drives the
    /// <see cref="_maxConsecutiveTransportFailuresBeforeUnhealthy"/> health guard.
    /// </summary>
    private int _consecutiveTransportFailures;

    /// <summary>
    /// Creates a new tickle timer.
    /// </summary>
    /// <param name="sessionApi">Refit client for session endpoints.</param>
    /// <param name="onFailure">Callback invoked when the session is detected as dead.</param>
    /// <param name="sessionHealthState">Shared session health state to update after each tickle.</param>
    /// <param name="logger">Logger for tickle events.</param>
    /// <param name="notifier">Session lifecycle notifier — used to fire "tickle succeeded" events that the WebSocket client subscribes to as a reconnect watchdog.</param>
    /// <param name="tenant">Per-provider tenant identity used to tag telemetry.</param>
    /// <param name="healthyIntervalSeconds">Interval between tickle requests after a successful tickle, in seconds. Default is 60.</param>
    /// <param name="failureIntervalSeconds">Interval between tickle requests after a failed tickle, in seconds. Default is 5. Used to recover quickly from network outages and transient backend errors.</param>
    /// <param name="timeProvider">Time provider for delay abstraction. Default is <see cref="TimeProvider.System"/>.</param>
    public TickleTimer(
        IIbkrSessionApi sessionApi,
        Func<CancellationToken, Task> onFailure,
        SessionHealthState sessionHealthState,
        ILogger<TickleTimer> logger,
        ISessionLifecycleNotifier notifier,
        TenantContext tenant,
        int healthyIntervalSeconds = 60,
        int failureIntervalSeconds = 5,
        TimeProvider? timeProvider = null)
    {
        _sessionApi = sessionApi;
        _onFailure = onFailure;
        _sessionHealthState = sessionHealthState;
        _logger = logger;
        _notifier = notifier;
        _tenant = tenant;
        _logScope = new Dictionary<string, object> { [LogFields.TenantId] = _tenant.TenantId };
        _healthyIntervalSeconds = healthyIntervalSeconds;
        _failureIntervalSeconds = failureIntervalSeconds;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_startStopLock)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _backgroundTask = RunAsync(_cts.Token);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        Task? taskToAwait;
        CancellationTokenSource? ctsToDispose;

        lock (_startStopLock)
        {
            ctsToDispose = _cts;
            taskToAwait = _backgroundTask;
            _cts = null;
            _backgroundTask = null;
        }

        if (ctsToDispose != null)
        {
            await ctsToDispose.CancelAsync();
        }

        if (taskToAwait != null)
        {
            try
            {
                await taskToAwait;
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
            }
        }

        ctsToDispose?.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tickle response indicates session is not authenticated")]
    private partial void LogSessionNotAuthenticated();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Tickle successful — session is authenticated")]
    private partial void LogTickleSuccessful();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tickle failed with exception")]
    private partial void LogTickleFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tickle returned HTTP 401 — session no longer authenticated; triggering re-authentication")]
    private partial void LogTickleUnauthorized();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tickle-succeeded notification threw")]
    private partial void LogTickleNotificationFailed(Exception exception);

    private void UpdateSessionHealthState(TickleAuthStatus? authStatus)
    {
        if (authStatus is null)
        {
            return;
        }

        _sessionHealthState.Update(
            authenticated: authStatus.Authenticated,
            connected: authStatus.Connected,
            competing: authStatus.Competing,
            established: authStatus.Established);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var delaySeconds = _lastTickleSucceeded ? _healthyIntervalSeconds : _failureIntervalSeconds;
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), _timeProvider, cancellationToken);

            using var _ = _logger.BeginScope(_logScope);

            try
            {
                using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Session.Tickle");
                activity?.SetTag(LogFields.TenantId, _tenant.TenantId);

                var response = await _sessionApi.TickleAsync(cancellationToken);

                // A response was received — transport is working; clear the transport-failure run.
                _consecutiveTransportFailures = 0;

                var authStatus = response.Iserver?.AuthStatus;
                var isAuthenticated = authStatus?.Authenticated ?? false;
                activity?.SetTag("authenticated", isAuthenticated);

                UpdateSessionHealthState(authStatus);

                if (!isAuthenticated)
                {
                    _tickleCount.Add(1,
                        new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId),
                        new KeyValuePair<string, object?>("success", false));
                    _tickleFailureCount.Add(1,
                        new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId));
                    _lastTickleSucceeded = false;
                    LogSessionNotAuthenticated();
                    await _onFailure(cancellationToken);
                }
                else
                {
                    _tickleCount.Add(1,
                        new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId),
                        new KeyValuePair<string, object?>("success", true));
                    _lastTickleSucceeded = true;
                    LogTickleSuccessful();
                    try
                    {
                        await _notifier.NotifyTickleSucceededAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        LogTickleNotificationFailed(ex);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Refit wraps a cancelled in-flight tickle SendAsync in ApiRequestException; unwrap so
                // shutdown cancellation propagates as OperationCanceledException (handled by StopAsync) rather
                // than being logged as a tickle failure. Genuine failures fall through to the branches below.
                if (ex is ApiRequestException are)
                {
                    are.RethrowIfWrappedCancellation(cancellationToken);
                }

                _tickleCount.Add(1,
                    new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId),
                    new KeyValuePair<string, object?>("success", false));
                _tickleFailureCount.Add(1,
                    new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId));
                _lastTickleSucceeded = false;

                if (ex is ApiException { StatusCode: HttpStatusCode.Unauthorized })
                {
                    // SES-2: an HTTP 401 tickle is a session-dead signal — the server rejected
                    // the LST signature (distinct from the ApiRequestException that wraps transport
                    // faults). Reflect the server's verdict in health (authenticated=false, leaving
                    // any competing evidence intact per ADR-0004's "fed from server responses, never
                    // literals") and trigger re-authentication. ReauthenticateAsync acquires a fresh
                    // LST via RefreshAsync, so the dead LST is not needed.
                    _consecutiveTransportFailures = 0;
                    _sessionHealthState.SetFailed("Tickle returned HTTP 401 — session no longer authenticated");
                    LogTickleUnauthorized();
                    await _onFailure(cancellationToken);
                }
                else
                {
                    // Transport-level failures (5xx, network errors, timeouts) are NOT, on their
                    // own, session-dead signals — reauth requires the same network, so firing
                    // _onFailure here would just stampede a doomed handshake. But health must not
                    // report a session live on stale evidence forever: after a sustained run of
                    // consecutive transport failures, mark the session unauthenticated so passive
                    // health tells the truth (SES-2).
                    LogTickleFailed(ex);
                    _consecutiveTransportFailures++;
                    if (_consecutiveTransportFailures >= _maxConsecutiveTransportFailuresBeforeUnhealthy)
                    {
                        _sessionHealthState.SetFailed(
                            $"Tickle failed {_consecutiveTransportFailures} consecutive times — session liveness unverified");
                    }
                }
            }
        }
    }
}

/// <summary>
/// Default factory that creates <see cref="TickleTimer"/> instances.
/// </summary>
internal sealed class TickleTimerFactory : ITickleTimerFactory
{
    private readonly SessionHealthState _sessionHealthState;
    private readonly ILogger<TickleTimer> _logger;
    private readonly ISessionLifecycleNotifier _notifier;
    private readonly TenantContext _tenant;
    private readonly int _healthyIntervalSeconds;
    private readonly int _failureIntervalSeconds;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates a new factory with the given dependencies.
    /// </summary>
    public TickleTimerFactory(
        SessionHealthState sessionHealthState,
        ILogger<TickleTimer> logger,
        ISessionLifecycleNotifier notifier,
        TenantContext tenant,
        int healthyIntervalSeconds,
        int failureIntervalSeconds,
        TimeProvider timeProvider)
    {
        _sessionHealthState = sessionHealthState;
        _logger = logger;
        _notifier = notifier;
        _tenant = tenant;
        _healthyIntervalSeconds = healthyIntervalSeconds;
        _failureIntervalSeconds = failureIntervalSeconds;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public ITickleTimer Create(IIbkrSessionApi sessionApi, Func<CancellationToken, Task> onFailure) =>
        new TickleTimer(sessionApi, onFailure, _sessionHealthState, _logger, _notifier, _tenant, _healthyIntervalSeconds, _failureIntervalSeconds, _timeProvider);
}
