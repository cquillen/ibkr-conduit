using System.Text.Json;

namespace IbkrConduit.Streaming.Mappers;

/// <summary>
/// Maps a <c>str</c> WebSocket frame to zero or more <see cref="TradeExecution"/> records
/// by fanning out the frame's <c>args</c> array.
/// </summary>
internal static class TradeExecutionMapper
{
    /// <summary>
    /// Yields one <see cref="TradeExecution"/> per element of the frame's <c>args</c> array. Missing
    /// or non-array <c>args</c> yields nothing.
    /// </summary>
    /// <param name="frame">The raw <c>str</c> frame whose <c>args</c> array carries the executions.</param>
    /// <param name="onElementDropped">
    /// Invoked once for each <c>args</c> element that fails to deserialize. Failures are isolated
    /// per element (FIL-2) so one malformed execution never discards the frame's tail — a <c>str</c>
    /// snapshot frame carries up to a whole day's fills. The caller reports the drop through the
    /// streaming drop taxonomy (count with <c>cause=mapper</c>, log against the wire topic); when
    /// <see langword="null"/> the bad element is skipped silently.
    /// </param>
    public static IEnumerable<TradeExecution> MapMany(JsonElement frame, Action<Exception>? onElementDropped = null)
    {
        if (!frame.TryGetProperty("args", out var args) || args.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var element in args.EnumerateArray())
        {
            // Deserialize each element inside its own guard and materialize the result before
            // yielding, so a malformed element is skipped in isolation (log-and-skip via
            // onElementDropped) instead of throwing mid-enumeration and discarding every later
            // execution in the frame (FIL-2). The observable-level catch stays the last resort.
            TradeExecution? execution;
            try
            {
                execution = element.Deserialize<TradeExecution>(StreamingSerialization.Options);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                onElementDropped?.Invoke(ex);
                continue;
            }

            if (execution is not null)
            {
                yield return execution;
            }
        }
    }
}
