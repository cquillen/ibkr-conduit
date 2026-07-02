namespace IbkrConduit.Streaming;

/// <summary>
/// A live streaming subscription. Dispose (or call <see cref="UnsubscribeAsync"/>) to send the
/// topic's IBKR unsubscribe message, stop delivery, and complete <see cref="Stream"/>.
/// </summary>
/// <typeparam name="T">The type of items emitted by the subscription.</typeparam>
public interface IIbkrSubscription<out T> : IAsyncDisposable
{
    /// <summary>The live stream of items for this subscription.</summary>
    IObservable<T> Stream { get; }

    /// <summary>
    /// Sends the topic's unsubscribe wire message (when one exists and no other live subscription
    /// still shares it), stops local delivery, and completes <see cref="Stream"/>. Idempotent and
    /// best-effort: a failed wire send is logged, not thrown, and local teardown still completes.
    /// <see cref="System.IAsyncDisposable.DisposeAsync"/> calls this.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the unsubscribe wire send.</param>
    ValueTask UnsubscribeAsync(CancellationToken cancellationToken = default);
}
