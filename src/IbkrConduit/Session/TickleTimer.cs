using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Diagnostics;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Session;

/// <summary>
/// Uses <see cref="PeriodicTimer"/> to send tickle requests at a fixed interval.
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
    private readonly ILogger<TickleTimer> _logger;
    private readonly int _intervalSeconds;
    private CancellationTokenSource? _cts;
    private Task? _backgroundTask;

    /// <summary>
    /// Creates a new tickle timer.
    /// </summary>
    /// <param name="sessionApi">Refit client for session endpoints.</param>
    /// <param name="onFailure">Callback invoked when the session is detected as dead.</param>
    /// <param name="logger">Logger for tickle events.</param>
    /// <param name="intervalSeconds">Interval between tickle requests in seconds. Default is 60.</param>
    public TickleTimer(
        IIbkrSessionApi sessionApi,
        Func<CancellationToken, Task> onFailure,
        ILogger<TickleTimer> logger,
        int intervalSeconds = 60)
    {
        _sessionApi = sessionApi;
        _onFailure = onFailure;
        _logger = logger;
        _intervalSeconds = intervalSeconds;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _backgroundTask = RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
        }

        if (_backgroundTask != null)
        {
            try
            {
                await _backgroundTask;
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
            }
        }

        _cts?.Dispose();
        _cts = null;
        _backgroundTask = null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tickle response indicates session is not authenticated")]
    private partial void LogSessionNotAuthenticated();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Tickle successful — session is authenticated")]
    private partial void LogTickleSuccessful();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tickle failed with exception")]
    private partial void LogTickleFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failure callback threw an exception")]
    private partial void LogFailureCallbackError(Exception exception);

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(_intervalSeconds));

        while (await periodicTimer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Session.Tickle");

                var response = await _sessionApi.TickleAsync(cancellationToken);
                var isAuthenticated = response.Iserver?.AuthStatus?.Authenticated ?? false;
                activity?.SetTag("authenticated", isAuthenticated);

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
                throw; // Propagate cancellation
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
    private readonly ILogger<TickleTimer> _logger;
    private readonly int _intervalSeconds;

    /// <summary>
    /// Creates a new factory with the given logger and interval.
    /// </summary>
    public TickleTimerFactory(ILogger<TickleTimer> logger, int intervalSeconds = 60)
    {
        _logger = logger;
        _intervalSeconds = intervalSeconds;
    }

    /// <inheritdoc />
    public ITickleTimer Create(IIbkrSessionApi sessionApi, Func<CancellationToken, Task> onFailure) =>
        new TickleTimer(sessionApi, onFailure, _logger, _intervalSeconds);
}
