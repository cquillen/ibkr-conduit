using System.Globalization;
using System.Text.Json;

namespace IbkrConduit.Streaming.Mappers;

/// <summary>Maps an <c>smd+conid</c> WebSocket frame to a <see cref="MarketDataTick"/>, extracting the conid from the topic and collecting numeric field IDs.</summary>
internal static class MarketDataTickMapper
{
    public static MarketDataTick Map(JsonElement element)
    {
        var conid = 0;
        long? updated = null;
        var fields = new Dictionary<string, string>();
        Dictionary<string, JsonElement>? additional = null;

        // Extract conid from the topic string: "smd+265598" -> 265598
        if (element.TryGetProperty("topic", out var topicProp) && topicProp.ValueKind == JsonValueKind.String)
        {
            var topic = topicProp.GetString();
            if (topic != null)
            {
                var plusIndex = topic.IndexOf('+', StringComparison.Ordinal);
                if (plusIndex >= 0 && int.TryParse(topic[(plusIndex + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedConid))
                {
                    conid = parsedConid;
                }
            }
        }

        // Also try the conid property directly. IBKR type-drifts numeric-ish fields to quoted strings
        // (WIR-5), so tolerate either a JSON number or a string rather than throwing on GetInt32.
        if (conid == 0 && element.TryGetProperty("conid", out var conidProp))
        {
            conid = ReadInt32(conidProp) ?? 0;
        }

        // _updated is likewise string-tolerant (WIR-5) so a quoted timestamp never throws.
        if (element.TryGetProperty("_updated", out var updatedProp))
        {
            updated = ReadInt64(updatedProp);
        }

        foreach (var prop in element.EnumerateObject())
        {
            if (prop.Name is "topic" or "conid" or "_updated")
            {
                continue;
            }

            if (int.TryParse(prop.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                // Numeric keys are market data field IDs.
                fields[prop.Name] = prop.Value.ToString();
            }
            else
            {
                // Non-numeric unmapped keys (e.g. conidEx, server_id) survive in the overflow bag
                // rather than being silently discarded (WIR-5). Clone detaches the value from the
                // frame's backing document so it stays valid after the document is released.
                (additional ??= [])[prop.Name] = prop.Value.Clone();
            }
        }

        return new MarketDataTick
        {
            Conid = conid,
            Updated = updated,
            Fields = fields.Count > 0 ? fields : null,
            AdditionalData = additional,
        };
    }

    /// <summary>Reads an <see cref="int"/> from a number or a quoted-string JSON value, or null when neither parses.</summary>
    private static int? ReadInt32(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt32(out var n) => n,
            JsonValueKind.String when int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
            _ => null,
        };

    /// <summary>Reads a <see cref="long"/> from a number or a quoted-string JSON value, or null when neither parses.</summary>
    private static long? ReadInt64(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt64(out var n) => n,
            JsonValueKind.String when long.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
            _ => null,
        };
}
