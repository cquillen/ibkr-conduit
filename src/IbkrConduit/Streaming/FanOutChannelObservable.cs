using System.Text.Json;
using System.Threading.Channels;

namespace IbkrConduit.Streaming;

/// <summary>
/// <see cref="IObservable{T}"/> backed by a <see cref="ChannelReader{T}"/> of raw frames,
/// where each frame maps to zero or more <typeparamref name="T"/> items. One
/// <see cref="IObserver{T}.OnNext"/> is raised per mapped item. Used for topics whose
/// frame carries an <c>args</c> array (e.g. <c>str</c> trade executions), in contrast to
/// <see cref="ChannelObservable{T}"/> which maps one frame to exactly one item.
/// </summary>
/// <typeparam name="T">The type of items emitted to observers.</typeparam>
internal sealed class FanOutChannelObservable<T> : IObservable<T>
{
    private readonly ChannelReader<JsonElement> _reader;
    private readonly Func<JsonElement, IEnumerable<T>> _mapper;

    /// <summary>Creates a new <see cref="FanOutChannelObservable{T}"/>.</summary>
    /// <param name="reader">The channel reader to consume frames from.</param>
    /// <param name="mapper">Function mapping one frame to zero or more items.</param>
    public FanOutChannelObservable(ChannelReader<JsonElement> reader, Func<JsonElement, IEnumerable<T>> mapper)
    {
        _reader = reader;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<T> observer)
    {
        var cts = new CancellationTokenSource();
        _ = PumpAsync(observer, cts.Token);
        return new CancellationDisposable(cts);
    }

    private async Task PumpAsync(IObserver<T> observer, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in _reader.ReadAllAsync(cancellationToken))
            {
                foreach (var item in _mapper(frame))
                {
                    observer.OnNext(item);
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
