using Microsoft.Extensions.Logging;

namespace IbkrConduit.Streaming;

/// <summary>
/// Source-generated log message shared by <see cref="ChannelObservable{T}"/> and
/// <see cref="FanOutChannelObservable{T}"/>. Both observables catch mapper/<c>OnNext</c>
/// failures per frame so one malformed frame cannot terminate the whole subscription; this
/// is what they log when that happens.
/// </summary>
internal static partial class StreamingObservableLog
{
    /// <summary>Logs that a single malformed frame was caught and skipped so the stream can keep running.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="topic">The item type name (or topic) the dropped frame belonged to.</param>
    /// <param name="message">The caught exception's message.</param>
    /// <param name="exception">The caught exception.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Dropping a malformed {Topic} frame: {Message}")]
    public static partial void LogDroppedFrame(this ILogger logger, string topic, string message, Exception exception);
}
