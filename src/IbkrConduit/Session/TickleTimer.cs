using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Diagnostics;
using IbkrConduit.Health;
using Microsoft.Extensions.Logging;

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

    private readonly IIbkrSessionApi _sessionApi;
    private readonly Func<CancellationToken, Task> _onFailure;
    private readonly SessionHealthState _sessionHealthState;
    private readonly ILogger<TickleTimer> _logger;
    private readonly ISessionLifecycleNotifier _notifier;
    private readonly int _healthyIntervalSeconds;
    private readonly int _failureIntervalSeconds;
    private readonly TimeProvider _timeProvider;
    private readonly object _startStopLock = new();
    private CancellationTokenSource? _cts;
    private Task? _backgroundTask;
    private bool _lastTickleSucceeded = true;

    /// <summary>
    /// Creates a new tickle timer.
    /// </summary>
    /// <param name="sessionApi">Refit client for session endpoints.</param>
    /// <param name="onFailure">Callback invoked when the session is detected as dead.</param>
    /// <param name="sessionHealthState">Shared session health state to update after each tickle.</param>
    /// <param name="logger">Logger for tickle events.</param>
    /// <param name="notifier">Session lifecycle notifier — used to fire "tickle succeeded" events that the WebSocket client subscribes to as a reconnect watchdog.</param>
    /// <param name="healthyIntervalSeconds">Interval between tickle requests after a successful tickle, in seconds. Default is 60.</param>
    /// <param name="failureIntervalSeconds">Interval between tickle requests after a failed tickle, in seconds. Default is 5. Used to recover quickly from network outages and transient backend errors.</param>
    /// <param name="timeProvider">Time provider for delay abstraction. Default is <see cref="TimeProvider.System"/>.</param>
    public TickleTimer(
        IIbkrSessionApi sessionApi,
        Func<CancellationToken, Task> onFailure,
        SessionHealthState sessionHealthState,
        ILogger<TickleTimer> logger,
        ISessionLifecycleNotifier notifier,
        int healthyIntervalSeconds = 60,
        int failureIntervalSeconds = 5,
        TimeProvider? timeProvider = null)
    {
        _sessionApi = sessionApi;
        _onFailure = onFailure;
        _sessionHealthState = sessionHealthState;
        _logger = logger;
        _notifier = notifier;
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

    [LoggerMessage(Level = LogLevel.Error, Message = "Failure callback threw an exception")]
    private partial void LogFailureCallbackError(Exception exception);

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

            try
            {
                using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Session.Tickle");

                var response = await _sessionApi.TickleAsync(cancellationToken);
                var authStatus = response.Iserver?.AuthStatus;
                var isAuthenticated = authStatus?.Authenticated ?? false;
                activity?.SetTag("authenticated", isAuthenticated);

                UpdateSessionHealthState(authStatus);

                if (!isAuthenticated)
                {
                    _tickleCount.Add(1, new KeyValuePair<string, object?>("success", false));
                    _tickleFailureCount.Add(1);
                    _lastTickleSucceeded = false;
                    LogSessionNotAuthenticated();
                    await _onFailure(cancellationToken);
                }
                else
                {
                    _tickleCount.Add(1, new KeyValuePair<string, object?>("success", true));
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
                _tickleCount.Add(1, new KeyValuePair<string, object?>("success", false));
                _tickleFailureCount.Add(1);
                _lastTickleSucceeded = false;
                LogTickleFailed(ex);
                try
                {
                    await _onFailure(cancellationToken);
                }
                catch (Exception cbEx)
                {
                    LogFailureCallbackError(cbEx);
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
        int healthyIntervalSeconds,
        int failureIntervalSeconds,
        TimeProvider timeProvider)
    {
        _sessionHealthState = sessionHealthState;
        _logger = logger;
        _notifier = notifier;
        _healthyIntervalSeconds = healthyIntervalSeconds;
        _failureIntervalSeconds = failureIntervalSeconds;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public ITickleTimer Create(IIbkrSessionApi sessionApi, Func<CancellationToken, Task> onFailure) =>
        new TickleTimer(sessionApi, onFailure, _sessionHealthState, _logger, _notifier, _healthyIntervalSeconds, _failureIntervalSeconds, _timeProvider);
}
