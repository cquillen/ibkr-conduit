using System.Text.Json;

namespace IbkrConduit.Streaming.Mappers;

/// <summary>
/// Maps a <c>str</c> WebSocket frame to zero or more <see cref="TradeExecution"/> records
/// by fanning out the frame's <c>args</c> array.
/// </summary>
internal static class TradeExecutionMapper
{
    /// <summary>Yields one <see cref="TradeExecution"/> per element of the frame's <c>args</c> array. Missing or non-array <c>args</c> yields nothing.</summary>
    public static IEnumerable<TradeExecution> MapMany(JsonElement frame)
    {
        if (!frame.TryGetProperty("args", out var args) || args.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var element in args.EnumerateArray())
        {
            var execution = element.Deserialize<TradeExecution>(StreamingSerialization.Options);
            if (execution is not null)
            {
                yield return execution;
            }
        }
    }
}
