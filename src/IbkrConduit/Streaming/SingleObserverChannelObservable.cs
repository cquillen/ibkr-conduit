namespace IbkrConduit.Streaming;

/// <summary>
/// Base for the channel-backed observables that implement <see cref="IIbkrSubscription{T}.Stream"/>.
/// Enforces the <see href="../../docs/adr/0002-streaming-delivery-guarantee.md">ADR-0002</see>
/// single-observer contract: a second concurrent <see cref="Subscribe"/> throws
/// <see cref="InvalidOperationException"/> rather than silently splitting delivery across two
/// pumps competing on one <see cref="System.Threading.Channels.ChannelReader{T}"/>. Disposing the
/// subscription cancels its pump and — only once that pump task has exited — frees the slot, so a
/// fresh <see cref="Subscribe"/> then succeeds without racing a still-draining pump (STR-2).
/// Subclasses implement the per-frame pump in <see cref="PumpAsync"/> and must observe cancellation
/// per item so a disposed subscription stops draining buffered frames to a dead observer.
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

        var slot = new SubscriptionSlot(this);
        slot.Start(observer);
        return slot;
    }

    private void ReleaseSlot() => Interlocked.Exchange(ref _observerAttached, 0);

    /// <summary>Pumps items from the backing channel to <paramref name="observer"/> until the channel completes or the token cancels.</summary>
    /// <param name="observer">The single observer to deliver to.</param>
    /// <param name="cancellationToken">Cancelled when the subscription is disposed.</param>
    protected abstract Task PumpAsync(IObserver<T> observer, CancellationToken cancellationToken);

    /// <summary>
    /// Disposable returned to the single observer: cancels the pump and frees the single-observer
    /// slot exactly once — only after the pump task has observed cancellation and exited, so a
    /// subsequent <see cref="Subscribe"/> on the same reader never races a still-draining pump.
    /// </summary>
    private sealed class SubscriptionSlot : IDisposable
    {
        private readonly SingleObserverChannelObservable<T> _owner;
        private readonly CancellationTokenSource _cts = new();

        // The managed-thread id currently executing the observer's callback, or -1 when none. A
        // Dispose invoked from inside the callback runs on this same thread; block-joining the pump
        // there would deadlock, so it is detected by thread identity and the join is skipped.
        private int _callbackThreadId = -1;
        private Task? _pumpTask;
        private int _disposed;

        public SubscriptionSlot(SingleObserverChannelObservable<T> owner) => _owner = owner;

        /// <summary>Starts the pump on the thread pool so its continuations capture no caller sync-context (a re-entrancy-safe join in <see cref="Dispose"/>).</summary>
        public void Start(IObserver<T> observer) =>
            _pumpTask = Task.Run(() => RunAsync(new CallbackTrackingObserver(observer, this)));

        private async Task RunAsync(IObserver<T> observer)
        {
            try
            {
                await _owner.PumpAsync(observer, _cts.Token).ConfigureAwait(false);
            }
            finally
            {
                // Free the single-observer slot only once the pump has exited (STR-2).
                _owner.ReleaseSlot();
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            try
            {
                _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed elsewhere.
            }

            var pumpTask = _pumpTask;
            var reentrant = Volatile.Read(ref _callbackThreadId) == Environment.CurrentManagedThreadId;
            if (pumpTask is not null && !reentrant)
            {
                // Block until the pump observes cancellation and exits (releasing the slot in its
                // finally) so the slot is free before Dispose returns. Skipped when Dispose is
                // re-entrant from the observer callback (on the pump thread), where the pump
                // unwinds and releases the slot on its own.
                try
                {
                    pumpTask.GetAwaiter().GetResult();
                }
                catch
                {
                    // Pump faults surface to the observer via OnError, not to the disposer.
                }

                _cts.Dispose();
            }
        }

        private void EnterCallback() => Volatile.Write(ref _callbackThreadId, Environment.CurrentManagedThreadId);

        private void ExitCallback() => Volatile.Write(ref _callbackThreadId, -1);

        /// <summary>
        /// Wraps the observer to record the physical thread running each callback, so a re-entrant
        /// <see cref="Dispose"/> from inside a callback is detected by thread identity (robust to the
        /// <see cref="System.Threading.ExecutionContext"/> swap a synchronous await continuation causes).
        /// </summary>
        private sealed class CallbackTrackingObserver(IObserver<T> inner, SubscriptionSlot slot) : IObserver<T>
        {
            public void OnNext(T value)
            {
                slot.EnterCallback();
                try
                {
                    inner.OnNext(value);
                }
                finally
                {
                    slot.ExitCallback();
                }
            }

            public void OnError(Exception error)
            {
                slot.EnterCallback();
                try
                {
                    inner.OnError(error);
                }
                finally
                {
                    slot.ExitCallback();
                }
            }

            public void OnCompleted()
            {
                slot.EnterCallback();
                try
                {
                    inner.OnCompleted();
                }
                finally
                {
                    slot.ExitCallback();
                }
            }
        }
    }
}
