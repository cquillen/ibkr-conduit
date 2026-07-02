using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrConduit.Streaming.Mappers;

/// <summary>
/// Maps a <c>str</c> WebSocket frame to zero or more <see cref="TradeExecution"/> records
/// by fanning out the frame's <c>args</c> array. IBKR sends <c>price</c> as a quoted
/// string, so <see cref="_options"/> enables reading numbers from strings.
/// </summary>
internal static class TradeExecutionMapper
{
    private static readonly JsonSerializerOptions _options = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>Yields one <see cref="TradeExecution"/> per element of the frame's <c>args</c> array. Missing or non-array <c>args</c> yields nothing.</summary>
    public static IEnumerable<TradeExecution> MapMany(JsonElement frame)
    {
        if (!frame.TryGetProperty("args", out var args) || args.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var element in args.EnumerateArray())
        {
            var execution = element.Deserialize<TradeExecution>(_options);
            if (execution is not null)
            {
                yield return execution;
            }
        }
    }
}
