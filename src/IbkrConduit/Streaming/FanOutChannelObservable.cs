using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Streaming;

/// <summary>
/// <see cref="IObservable{T}"/> backed by a <see cref="ChannelReader{T}"/> of raw frames,
/// where each frame maps to zero or more <typeparamref name="T"/> items. One
/// <see cref="IObserver{T}.OnNext"/> is raised per mapped item. Used for topics whose
/// frame carries an <c>args</c> array (e.g. <c>str</c> trade executions), in contrast to
/// <see cref="ChannelObservable{T}"/> which maps one frame to exactly one item. A mapper or
/// <see cref="IObserver{T}.OnNext"/> failure on one frame is caught, logged, and skipped
/// rather than tearing down the subscription via <see cref="IObserver{T}.OnError"/> — see
/// <see cref="PumpAsync"/>.
/// </summary>
/// <typeparam name="T">The type of items emitted to observers.</typeparam>
internal sealed class FanOutChannelObservable<T> : IObservable<T>
{
    private readonly ChannelReader<JsonElement> _reader;
    private readonly Func<JsonElement, IEnumerable<T>> _mapper;
    private readonly ILogger _logger;

    /// <summary>Creates a new <see cref="FanOutChannelObservable{T}"/>.</summary>
    /// <param name="reader">The channel reader to consume frames from.</param>
    /// <param name="mapper">Function mapping one frame to zero or more items.</param>
    /// <param name="logger">Logger used to report frames dropped due to a mapper/OnNext failure.</param>
    public FanOutChannelObservable(ChannelReader<JsonElement> reader, Func<JsonElement, IEnumerable<T>> mapper, ILogger logger)
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
            await foreach (var frame in _reader.ReadAllAsync(cancellationToken))
            {
                // Isolate one frame's mapper/OnNext failure from the rest of the stream: an
                // unexpected shape on a single frame (or one bad element within its args
                // array) must not kill the whole subscription. OnError is reserved for the
                // outer catch below, which only fires for channel-level faults, not
                // per-frame ones.
                try
                {
                    foreach (var item in _mapper(frame))
                    {
                        observer.OnNext(item);
                    }
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
