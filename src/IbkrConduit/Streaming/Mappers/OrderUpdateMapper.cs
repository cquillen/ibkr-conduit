using System.Text.Json;

namespace IbkrConduit.Streaming.Mappers;

/// <summary>
/// Maps a <c>sor</c> WebSocket frame to zero or more <see cref="OrderUpdate"/> records
/// by fanning out the frame's <c>args</c> array.
/// </summary>
internal static class OrderUpdateMapper
{
    /// <summary>Yields one <see cref="OrderUpdate"/> per element of the frame's <c>args</c> array. Missing or non-array <c>args</c> yields nothing.</summary>
    /// <param name="frame">The raw <c>sor</c> frame whose <c>args</c> array carries the order updates.</param>
    /// <param name="onRequiredMoneyFieldAbsent">
    /// Invoked once per absent required money field (<c>totalSize</c>, <c>price</c>) on each mapped
    /// order — the WIR-5 census signal. Because <c>sor</c> frames are sparse deltas (ADR-0001) that
    /// legitimately omit fields, the census runs only on a status-bearing frame (a full order-state
    /// frame); a bare identity delta with no <c>status</c> is exempt so a normal delta never raises a
    /// false census. The order is still delivered (the field is <c>null</c> per ADR-0001). When
    /// <see langword="null"/> the census is skipped.
    /// </param>
    public static IEnumerable<OrderUpdate> MapMany(
        JsonElement frame, Action<string>? onRequiredMoneyFieldAbsent = null)
    {
        if (!frame.TryGetProperty("args", out var args) || args.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var element in args.EnumerateArray())
        {
            var order = element.Deserialize<OrderUpdate>(StreamingSerialization.Options);
            if (order is not null)
            {
                // A sparse identity delta (no status) legitimately omits money fields (ADR-0001), so
                // only census status-bearing order-state frames to avoid false positives on deltas.
                if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("status", out _))
                {
                    MoneyFieldCensus.ReportAbsent(
                        element, MoneyFieldCensus.OrderUpdateFields, onRequiredMoneyFieldAbsent);
                }

                yield return order;
            }
        }
    }
}
