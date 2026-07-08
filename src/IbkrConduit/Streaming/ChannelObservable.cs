using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Streaming;

/// <summary>
/// Minimal <see cref="IObservable{T}"/> implementation backed by a <see cref="ChannelReader{T}"/>
/// with a mapping function from <see cref="JsonElement"/> to <typeparamref name="T"/>. A mapper
/// failure on one frame is caught, counted (<c>cause=mapper</c>), logged against the wire topic,
/// and skipped rather than tearing down the subscription. A consumer <see cref="IObserver{T}.OnNext"/>
/// failure is treated distinctly: it is counted (<c>cause=observer</c>), logged, and surfaced via
/// <see cref="IObserver{T}.OnError"/> — never swallowed as a malformed frame and never masqueraded
/// as graceful completion (findings FIL-3, GAP2-4; single-observer per
/// <see cref="SingleObserverChannelObservable{T}"/>).
/// </summary>
/// <typeparam name="T">The type of items emitted to observers.</typeparam>
internal sealed class ChannelObservable<T> : SingleObserverChannelObservable<T>
{
    private readonly ChannelReader<JsonElement> _reader;
    private readonly Func<JsonElement, T> _mapper;
    private readonly ILogger _logger;
    private readonly StreamingMetrics _metrics;
    private readonly string _topic;

    /// <summary>
    /// Creates a new <see cref="ChannelObservable{T}"/>.
    /// </summary>
    /// <param name="reader">The channel reader to consume messages from.</param>
    /// <param name="mapper">Function to map raw JSON to typed model.</param>
    /// <param name="logger">Logger used to report frames dropped due to a mapper/observer failure.</param>
    /// <param name="metrics">Reporter that counts every dropped frame so no loss is silent.</param>
    /// <param name="topic">The wire topic prefix this subscription streams (used for drop counter/log tags).</param>
    public ChannelObservable(
        ChannelReader<JsonElement> reader,
        Func<JsonElement, T> mapper,
        ILogger logger,
        StreamingMetrics metrics,
        string topic)
    {
        _reader = reader;
        _mapper = mapper;
        _logger = logger;
        _metrics = metrics;
        _topic = topic;
    }

    /// <inheritdoc />
    protected override async Task PumpAsync(IObserver<T> observer, CancellationToken cancellationToken)
    {
        try
        {
            while (await _reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                // Check cancellation before each TryRead so a disposed subscription stops draining
                // without consuming a frame it will not deliver — leaving buffered frames intact for
                // a subsequent Subscribe rather than dropping them to a dead observer (STR-2).
                while (!cancellationToken.IsCancellationRequested && _reader.TryRead(out var item))
                {
                    T mapped;
                    try
                    {
                        mapped = _mapper(item);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Malformed/unexpected wire shape on this frame — count it and skip, keeping
                        // the rest of the stream alive.
                        _metrics.RecordDrop(_topic, StreamingMetrics.MapperCause);
                        _logger.LogDroppedFrame(_topic, ex.Message, ex);
                        continue;
                    }

                    try
                    {
                        observer.OnNext(mapped);
                    }
                    catch (Exception ex)
                    {
                        // The consumer's OnNext threw. Per the Rx contract this is an error, not a
                        // wire problem: count it, log it distinctly, and surface it via OnError
                        // (never OnCompleted — even for an OperationCanceledException). Then stop.
                        _metrics.RecordDrop(_topic, StreamingMetrics.ObserverCause);
                        _logger.LogObserverError(_topic, ex.Message, ex);
                        observer.OnError(ex);
                        return;
                    }
                }

                // Cancellation observed mid-drain (e.g. during a blocking OnNext): terminate.
                cancellationToken.ThrowIfCancellationRequested();
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
