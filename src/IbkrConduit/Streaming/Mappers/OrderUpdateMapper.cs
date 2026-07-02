using System.Text.Json;

namespace IbkrConduit.Streaming.Mappers;

/// <summary>
/// Maps a <c>sor</c> WebSocket frame to zero or more <see cref="OrderUpdate"/> records
/// by fanning out the frame's <c>args</c> array.
/// </summary>
internal static class OrderUpdateMapper
{
    /// <summary>Yields one <see cref="OrderUpdate"/> per element of the frame's <c>args</c> array. Missing or non-array <c>args</c> yields nothing.</summary>
    public static IEnumerable<OrderUpdate> MapMany(JsonElement frame)
    {
        if (!frame.TryGetProperty("args", out var args) || args.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var element in args.EnumerateArray())
        {
            var order = element.Deserialize<OrderUpdate>();
            if (order is not null)
            {
                yield return order;
            }
        }
    }
}
