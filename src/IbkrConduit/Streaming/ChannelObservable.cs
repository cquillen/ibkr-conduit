using System.Text.Json;
using System.Threading.Channels;

namespace IbkrConduit.Streaming;

/// <summary>
/// Minimal <see cref="IObservable{T}"/> implementation backed by a <see cref="ChannelReader{T}"/>
/// with a mapping function from <see cref="JsonElement"/> to <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of items emitted to observers.</typeparam>
internal sealed class ChannelObservable<T> : IObservable<T>
{
    private readonly ChannelReader<JsonElement> _reader;
    private readonly Func<JsonElement, T> _mapper;

    /// <summary>
    /// Creates a new <see cref="ChannelObservable{T}"/>.
    /// </summary>
    /// <param name="reader">The channel reader to consume messages from.</param>
    /// <param name="mapper">Function to map raw JSON to typed model.</param>
    public ChannelObservable(ChannelReader<JsonElement> reader, Func<JsonElement, T> mapper)
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
            await foreach (var item in _reader.ReadAllAsync(cancellationToken))
            {
                var mapped = _mapper(item);
                observer.OnNext(mapped);
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
