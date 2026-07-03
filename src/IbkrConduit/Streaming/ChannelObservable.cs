using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Streaming;

/// <summary>
/// Minimal <see cref="IObservable{T}"/> implementation backed by a <see cref="ChannelReader{T}"/>
/// with a mapping function from <see cref="JsonElement"/> to <typeparamref name="T"/>. A
/// mapper or <see cref="IObserver{T}.OnNext"/> failure on one frame is caught, logged, and
/// skipped rather than tearing down the subscription via <see cref="IObserver{T}.OnError"/> —
/// see <see cref="PumpAsync"/>.
/// </summary>
/// <typeparam name="T">The type of items emitted to observers.</typeparam>
internal sealed class ChannelObservable<T> : IObservable<T>
{
    private readonly ChannelReader<JsonElement> _reader;
    private readonly Func<JsonElement, T> _mapper;
    private readonly ILogger _logger;

    /// <summary>
    /// Creates a new <see cref="ChannelObservable{T}"/>.
    /// </summary>
    /// <param name="reader">The channel reader to consume messages from.</param>
    /// <param name="mapper">Function to map raw JSON to typed model.</param>
    /// <param name="logger">Logger used to report frames dropped due to a mapper/OnNext failure.</param>
    public ChannelObservable(ChannelReader<JsonElement> reader, Func<JsonElement, T> mapper, ILogger logger)
    {
        _reader = reader;
        _mapper = mapper;
        _logger = logger;
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
                // Isolate one frame's mapper/OnNext failure from the rest of the stream: an
                // unexpected shape on a single frame must not kill the whole subscription
                // (e.g. a JsonException from an as-yet-unseen empty-string numeric field).
                // OnError is reserved for the outer catch below, which only fires for
                // channel-level faults, not per-frame ones.
                try
                {
                    var mapped = _mapper(item);
                    observer.OnNext(mapped);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogDroppedFrame(typeof(T).Name, ex.Message, ex);
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
