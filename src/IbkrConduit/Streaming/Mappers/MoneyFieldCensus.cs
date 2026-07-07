using System.Text.Json;

namespace IbkrConduit.Streaming.Mappers;

/// <summary>
/// Reports required streaming money fields that a delivered frame omits (WIR-5). A field counts as
/// absent only when the raw wire object does not carry the property at all — a present-but-empty
/// value (e.g. <c>"price":""</c> on a market order) is presence, not absence, so a well-formed frame
/// with an empty money field never raises a false census. The census is a diagnostic signal only: it
/// never alters the mapped DTO (the field still deserializes to <c>null</c> per ADR-0001).
/// </summary>
internal static class MoneyFieldCensus
{
    /// <summary>
    /// str execution frames are complete records (one item per execution), so a real fill is always
    /// expected to carry a quantity and a price. Their absence is the WIR-5 wire-drift signal.
    /// </summary>
    public static readonly string[] TradeExecutionFields = ["size", "price"];

    /// <summary>
    /// sor frames are sparse deltas (ADR-0001) that omit fields wholesale, so no money field is
    /// required on a bare identity delta. On a status-bearing frame (a full order-state frame),
    /// however, the order economics are expected — these are the fields the census checks there.
    /// </summary>
    public static readonly string[] OrderUpdateFields = ["totalSize", "price"];

    /// <summary>
    /// Invokes <paramref name="onAbsent"/> once for each of <paramref name="requiredFields"/> that
    /// the <paramref name="element"/> does not carry as a property. No-op when
    /// <paramref name="onAbsent"/> is <see langword="null"/> or the element is not a JSON object.
    /// </summary>
    /// <param name="element">The raw wire object for a single mapped frame element.</param>
    /// <param name="requiredFields">The topic's required money field names.</param>
    /// <param name="onAbsent">Callback invoked with each absent field's wire name.</param>
    public static void ReportAbsent(
        JsonElement element, IReadOnlyList<string> requiredFields, Action<string>? onAbsent)
    {
        if (onAbsent is null || element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var field in requiredFields)
        {
            if (!element.TryGetProperty(field, out _))
            {
                onAbsent(field);
            }
        }
    }
}
