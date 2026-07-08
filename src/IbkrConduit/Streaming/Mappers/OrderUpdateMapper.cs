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
    /// <param name="onElementDropped">
    /// Invoked once for each <c>args</c> element that fails to deserialize. Failures are isolated per
    /// element (WIR-1) so one malformed order never discards the frame's tail — a <c>sor</c> snapshot
    /// frame carries a whole day of orders. The caller reports the drop through the streaming drop
    /// taxonomy (count with <c>cause=mapper</c>, log against the wire topic); when
    /// <see langword="null"/> the bad element is skipped silently.
    /// </param>
    /// <param name="onRequiredMoneyFieldAbsent">
    /// Invoked once per absent required money field (<c>totalSize</c>, <c>price</c>) on each mapped
    /// order — the WIR-5 census signal. Because <c>sor</c> frames are sparse deltas (ADR-0001) that
    /// legitimately omit fields, the census runs only on a status-bearing frame (a full order-state
    /// frame); a bare identity delta with no <c>status</c> is exempt so a normal delta never raises a
    /// false census. The order is still delivered (the field is <c>null</c> per ADR-0001); a dropped
    /// element is never censused. When <see langword="null"/> the census is skipped.
    /// </param>
    public static IEnumerable<OrderUpdate> MapMany(
        JsonElement frame,
        Action<Exception>? onElementDropped = null,
        Action<string>? onRequiredMoneyFieldAbsent = null)
    {
        if (!frame.TryGetProperty("args", out var args) || args.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var element in args.EnumerateArray())
        {
            // Deserialize each element inside its own guard and materialize the result before
            // yielding, so a malformed element is skipped in isolation (log-and-skip via
            // onElementDropped) instead of throwing mid-enumeration and discarding every later order
            // in the frame (WIR-1). The observable-level catch stays the last resort.
            OrderUpdate? order;
            try
            {
                order = element.Deserialize<OrderUpdate>(StreamingSerialization.Options);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                onElementDropped?.Invoke(ex);
                continue;
            }

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
