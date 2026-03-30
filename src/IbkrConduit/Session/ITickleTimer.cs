using System;
using System.Threading;
using System.Threading.Tasks;

namespace IbkrConduit.Session;

/// <summary>
/// Periodically sends tickle requests to keep the brokerage session alive.
/// Notifies a callback when the session is detected as dead.
/// </summary>
internal interface ITickleTimer
{
    /// <summary>
    /// Starts the periodic tickle timer.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the periodic tickle timer and awaits the background task.
    /// </summary>
    Task StopAsync();
}

/// <summary>
/// Factory for creating <see cref="ITickleTimer"/> instances.
/// Decouples session management from direct <see cref="TickleTimer"/> construction.
/// </summary>
internal interface ITickleTimerFactory
{
    /// <summary>
    /// Creates a new tickle timer with the given session API and failure callback.
    /// </summary>
    ITickleTimer Create(IIbkrSessionApi sessionApi, Func<CancellationToken, Task> onFailure);
}
