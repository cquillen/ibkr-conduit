using System.Threading.Channels;

namespace IbkrConduit.Streaming;

/// <summary>
/// Pass-through <see cref="IObservable{T}"/> over a <see cref="ChannelReader{T}"/> of
/// <see cref="ConnectionEvent"/>s. Backs
/// <see cref="IbkrConduit.Client.IStreamingOperations.SubscribeConnectionEvents"/>. Single-observer
/// per <see cref="SingleObserverChannelObservable{T}"/>; a consumer <see cref="IObserver{T}.OnNext"/>
/// that throws tears the subscription down via <see cref="IObserver{T}.OnError"/> (never
/// <see cref="IObserver{T}.OnCompleted"/>, even for an <see cref="OperationCanceledException"/>).
/// </summary>
internal sealed class ConnectionEventObservable : SingleObserverChannelObservable<ConnectionEvent>
{
    private readonly ChannelReader<ConnectionEvent> _reader;

    /// <summary>Creates a new <see cref="ConnectionEventObservable"/>.</summary>
    /// <param name="reader">The channel reader to consume connection events from.</param>
    public ConnectionEventObservable(ChannelReader<ConnectionEvent> reader) => _reader = reader;

    /// <inheritdoc />
    protected override async Task PumpAsync(IObserver<ConnectionEvent> observer, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var connectionEvent in _reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    observer.OnNext(connectionEvent);
                }
                catch (Exception ex)
                {
                    // A consumer OnNext fault (including an OperationCanceledException) is an error,
                    // not a graceful completion: surface it and stop.
                    observer.OnError(ex);
                    return;
                }
            }

            observer.OnCompleted();
        }
        catch (OperationCanceledException)
        {
            observer.OnCompleted();
        }
        catch (Exception ex)
        {
            observer.OnError(ex);
        }
    }
}
