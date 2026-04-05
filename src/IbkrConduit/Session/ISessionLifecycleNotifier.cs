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
}
