using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit;
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
    private readonly int _intervalSeconds;
    private readonly TimeProvider _timeProvider;
    private readonly object _startStopLock = new();
    private CancellationTokenSource? _cts;
    private Task? _backgroundTask;

    /// <summary>
    /// Creates a new tickle timer.
    /// </summary>
    /// <param name="sessionApi">Refit client for session endpoints.</param>
    /// <param name="onFailure">Callback invoked when the session is detected as dead.</param>
    /// <param name="sessionHealthState">Shared session health state to update after each tickle.</param>
    /// <param name="logger">Logger for tickle events.</param>
    /// <param name="intervalSeconds">Interval between tickle requests in seconds. Default is 60.</param>
    /// <param name="timeProvider">Time provider for delay abstraction. Default is <see cref="TimeProvider.System"/>.</param>
    public TickleTimer(
        IIbkrSessionApi sessionApi,
        Func<CancellationToken, Task> onFailure,
        SessionHealthState sessionHealthState,
        ILogger<TickleTimer> logger,
        int intervalSeconds = 60,
        TimeProvider? timeProvider = null)
    {
        _sessionApi = sessionApi;
        _onFailure = onFailure;
        _sessionHealthState = sessionHealthState;
        _logger = logger;
        _intervalSeconds = intervalSeconds;
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
            await _timeProvider.Delay(_intervalSeconds * 1000, cancellationToken);

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
                    LogSessionNotAuthenticated();
                    await _onFailure(cancellationToken);
                }
                else
                {
                    _tickleCount.Add(1, new KeyValuePair<string, object?>("success", true));
                    LogTickleSuccessful();
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
    private readonly int _intervalSeconds;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates a new factory with the given logger, interval, and time provider.
    /// </summary>
    public TickleTimerFactory(
        SessionHealthState sessionHealthState,
        ILogger<TickleTimer> logger,
        int intervalSeconds = 60,
        TimeProvider? timeProvider = null)
    {
        _sessionHealthState = sessionHealthState;
        _logger = logger;
        _intervalSeconds = intervalSeconds;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public ITickleTimer Create(IIbkrSessionApi sessionApi, Func<CancellationToken, Task> onFailure) =>
        new TickleTimer(sessionApi, onFailure, _sessionHealthState, _logger, _intervalSeconds, _timeProvider);
}
