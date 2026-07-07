namespace IbkrConduit.Streaming;

/// <summary>
/// Base for the channel-backed observables that implement <see cref="IIbkrSubscription{T}.Stream"/>.
/// Enforces the <see href="../../docs/adr/0002-streaming-delivery-guarantee.md">ADR-0002</see>
/// single-observer contract: a second concurrent <see cref="Subscribe"/> throws
/// <see cref="InvalidOperationException"/> rather than silently splitting delivery across two
/// pumps competing on one <see cref="System.Threading.Channels.ChannelReader{T}"/>. Disposing the
/// subscription cancels its pump and frees the slot, so a fresh <see cref="Subscribe"/> then
/// succeeds. Subclasses implement the per-frame pump in <see cref="PumpAsync"/>.
/// </summary>
/// <typeparam name="T">The type of items emitted to observers.</typeparam>
internal abstract class SingleObserverChannelObservable<T> : IObservable<T>
{
    private int _observerAttached;

    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<T> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        if (Interlocked.CompareExchange(ref _observerAttached, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                $"{GetType().Name}.Stream is single-observer: a second concurrent Subscribe is not " +
                "allowed. Dispose the existing subscription before subscribing again, or fan out to " +
                "multiple consumers in your own code.");
        }

        var cts = new CancellationTokenSource();
        _ = PumpAsync(observer, cts.Token);
        return new SubscriptionSlot(new CancellationDisposable(cts), this);
    }

    private void ReleaseSlot() => Interlocked.Exchange(ref _observerAttached, 0);

    /// <summary>Pumps items from the backing channel to <paramref name="observer"/> until the channel completes or the token cancels.</summary>
    /// <param name="observer">The single observer to deliver to.</param>
    /// <param name="cancellationToken">Cancelled when the subscription is disposed.</param>
    protected abstract Task PumpAsync(IObserver<T> observer, CancellationToken cancellationToken);

    /// <summary>
    /// Disposable returned to the single observer: cancels the pump via the composed
    /// <see cref="CancellationDisposable"/> and frees the single-observer slot exactly once.
    /// </summary>
    private sealed class SubscriptionSlot : IDisposable
    {
        private CancellationDisposable? _cancellation;
        private SingleObserverChannelObservable<T>? _owner;

        public SubscriptionSlot(CancellationDisposable cancellation, SingleObserverChannelObservable<T> owner)
        {
            _cancellation = cancellation;
            _owner = owner;
        }

        public void Dispose()
        {
            var cancellation = Interlocked.Exchange(ref _cancellation, null);
            if (cancellation is null)
            {
                return;
            }

            cancellation.Dispose();
            Interlocked.Exchange(ref _owner, null)?.ReleaseSlot();
        }
    }
}
