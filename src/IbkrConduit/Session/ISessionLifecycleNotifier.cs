namespace IbkrConduit.Session;

/// <summary>
/// Notifies subscribers when the brokerage session has been refreshed.
/// Used by the WebSocket client to reconnect after session re-authentication.
/// </summary>
internal interface ISessionLifecycleNotifier
{
    /// <summary>
    /// Subscribes to session refresh notifications.
    /// Returns an <see cref="IDisposable"/> that removes the subscription when disposed.
    /// </summary>
    /// <param name="onSessionRefreshed">Callback invoked when the session is refreshed.</param>
    /// <returns>A disposable that removes the subscription.</returns>
    IDisposable Subscribe(Func<CancellationToken, Task> onSessionRefreshed);

    /// <summary>
    /// Notifies all subscribers that the session has been refreshed.
    /// Called by <see cref="SessionManager"/> after successful re-authentication.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Subscribes to "tickle succeeded" notifications. Fired by <see cref="TickleTimer"/>
    /// after every successful tickle, regardless of whether the previous tickle succeeded
    /// or failed. Subscribers should treat this as a periodic liveness signal — the
    /// WebSocket client uses it as a watchdog to re-trigger reconnect when the WS is dead.
    /// Returns an <see cref="IDisposable"/> that removes the subscription when disposed.
    /// </summary>
    /// <param name="onTickleSucceeded">Callback invoked when a tickle succeeds.</param>
    /// <returns>A disposable that removes the subscription.</returns>
    IDisposable SubscribeTickleSucceeded(Func<CancellationToken, Task> onTickleSucceeded);

    /// <summary>
    /// Notifies all "tickle succeeded" subscribers. Called by <see cref="TickleTimer"/>
    /// after each successful tickle.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyTickleSucceededAsync(CancellationToken cancellationToken);
}
