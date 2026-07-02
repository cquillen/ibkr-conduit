namespace IbkrConduit.Streaming;

/// <summary>
/// Default <see cref="IIbkrSubscription{T}"/>: wraps the stream observable and an async unsubscribe
/// delegate, guaranteeing the delegate runs at most once regardless of how disposal is triggered.
/// </summary>
/// <typeparam name="T">The type of items emitted by the subscription.</typeparam>
internal sealed class IbkrSubscription<T> : IIbkrSubscription<T>
{
    private readonly Func<CancellationToken, ValueTask> _unsubscribe;
    private int _unsubscribed;

    /// <summary>Creates a new <see cref="IbkrSubscription{T}"/>.</summary>
    /// <param name="stream">The stream observable exposed via <see cref="Stream"/>.</param>
    /// <param name="unsubscribe">The delegate that tears the subscription down.</param>
    public IbkrSubscription(IObservable<T> stream, Func<CancellationToken, ValueTask> unsubscribe)
    {
        Stream = stream;
        _unsubscribe = unsubscribe;
    }

    /// <inheritdoc />
    public IObservable<T> Stream { get; }

    /// <inheritdoc />
    public ValueTask UnsubscribeAsync(CancellationToken cancellationToken = default) =>
        Interlocked.Exchange(ref _unsubscribed, 1) == 0
            ? _unsubscribe(cancellationToken)
            : ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => UnsubscribeAsync(CancellationToken.None);
}
