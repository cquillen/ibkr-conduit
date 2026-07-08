using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Streaming;

/// <summary>
/// <see cref="IObservable{T}"/> backed by a <see cref="ChannelReader{T}"/> of raw frames, where
/// each frame maps to zero or more <typeparamref name="T"/> items. One
/// <see cref="IObserver{T}.OnNext"/> is raised per mapped item. Used for topics whose frame carries
/// an <c>args</c> array (e.g. <c>str</c> trade executions), in contrast to
/// <see cref="ChannelObservable{T}"/> which maps one frame to exactly one item. A mapper failure on
/// one frame is caught, counted (<c>cause=mapper</c>), logged against the wire topic, and skipped.
/// A consumer <see cref="IObserver{T}.OnNext"/> failure is counted (<c>cause=observer</c>), logged,
/// and surfaced via <see cref="IObserver{T}.OnError"/> — never swallowed as a malformed frame and
/// never masqueraded as graceful completion (findings FIL-3, GAP2-4; single-observer per
/// <see cref="SingleObserverChannelObservable{T}"/>).
/// </summary>
/// <typeparam name="T">The type of items emitted to observers.</typeparam>
internal sealed class FanOutChannelObservable<T> : SingleObserverChannelObservable<T>
{
    private readonly ChannelReader<JsonElement> _reader;
    private readonly Func<JsonElement, IEnumerable<T>> _mapper;
    private readonly ILogger _logger;
    private readonly StreamingMetrics _metrics;
    private readonly string _topic;

    /// <summary>Creates a new <see cref="FanOutChannelObservable{T}"/>.</summary>
    /// <param name="reader">The channel reader to consume frames from.</param>
    /// <param name="mapper">Function mapping one frame to zero or more items.</param>
    /// <param name="logger">Logger used to report frames dropped due to a mapper/observer failure.</param>
    /// <param name="metrics">Reporter that counts every dropped frame so no loss is silent.</param>
    /// <param name="topic">The wire topic prefix this subscription streams (used for drop counter/log tags).</param>
    public FanOutChannelObservable(
        ChannelReader<JsonElement> reader,
        Func<JsonElement, IEnumerable<T>> mapper,
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
                while (!cancellationToken.IsCancellationRequested && _reader.TryRead(out var frame))
                {
                    List<T> items;
                    try
                    {
                        // Materialize before delivery so a mapper failure is distinguishable from an
                        // observer failure below. Per-element mapper isolation (FIL-2) is VCR-03's
                        // scope; here a mapper throw drops the whole frame, counted and logged as
                        // cause=mapper.
                        items = _mapper(frame).ToList();
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _metrics.RecordDrop(_topic, StreamingMetrics.MapperCause);
                        _logger.LogDroppedFrame(_topic, ex.Message, ex);
                        continue;
                    }

                    foreach (var item in items)
                    {
                        try
                        {
                            observer.OnNext(item);
                        }
                        catch (Exception ex)
                        {
                            // Consumer OnNext fault (including OperationCanceledException): count,
                            // log distinctly, surface via OnError, and stop pumping.
                            _metrics.RecordDrop(_topic, StreamingMetrics.ObserverCause);
                            _logger.LogObserverError(_topic, ex.Message, ex);
                            observer.OnError(ex);
                            return;
                        }
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
