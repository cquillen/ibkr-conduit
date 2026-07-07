using Microsoft.Extensions.Logging;

namespace IbkrConduit.Streaming;

/// <summary>
/// Source-generated log messages shared by <see cref="ChannelObservable{T}"/> and
/// <see cref="FanOutChannelObservable{T}"/>. Both observables isolate per-frame failures so one
/// bad frame cannot terminate the whole subscription; these are what they log — always keyed by
/// the <em>wire topic</em> (e.g. <c>str</c>), never the DTO type name, so a drop is traceable to
/// the topic that lost it (finding GAP2-4).
/// </summary>
internal static partial class StreamingObservableLog
{
    /// <summary>Logs that a single frame was dropped because its mapper threw (a malformed or unexpected wire shape).</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="topic">The wire topic prefix the dropped frame belonged to.</param>
    /// <param name="message">The caught exception's message.</param>
    /// <param name="exception">The caught exception.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Dropping a malformed {Topic} frame: {Message}")]
    public static partial void LogDroppedFrame(this ILogger logger, string topic, string message, Exception exception);

    /// <summary>
    /// Logs that a consumer's <see cref="IObserver{T}.OnNext"/> threw while handling a frame. Distinct
    /// from <see cref="LogDroppedFrame"/> so an observer (consumer) fault is never mistaken for a
    /// malformed wire frame (finding FIL-3). The subscription is torn down via
    /// <see cref="IObserver{T}.OnError"/> after this logs.
    /// </summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="topic">The wire topic prefix whose frame the observer was handling.</param>
    /// <param name="message">The observer exception's message.</param>
    /// <param name="exception">The observer exception.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Observer threw handling a {Topic} frame; tearing down the subscription via OnError: {Message}")]
    public static partial void LogObserverError(this ILogger logger, string topic, string message, Exception exception);

    /// <summary>
    /// Logs that a delivered <paramref name="topic"/> frame omitted a required money field (WIR-5).
    /// The frame is still delivered (the field is <c>null</c> per ADR-0001); this census signal makes
    /// an IBKR wire-shape drift on a money field observable rather than silent. Paired with the
    /// <c>ibkr.conduit.streaming.money_field.absent</c> counter.
    /// </summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="topic">The wire topic prefix the frame belonged to.</param>
    /// <param name="field">The wire name of the required money field that was absent.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "A {Topic} frame omitted required money field '{Field}' — still delivered as null (possible wire-shape drift)")]
    public static partial void LogMissingMoneyField(this ILogger logger, string topic, string field);
}
