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

        // Extract conid from the topic string: "smd+265598" -> 265598
        if (element.TryGetProperty("topic", out var topicProp))
        {
            var topic = topicProp.GetString();
            if (topic != null)
            {
                var plusIndex = topic.IndexOf('+');
                if (plusIndex >= 0 && int.TryParse(topic[(plusIndex + 1)..], out var parsedConid))
                {
                    conid = parsedConid;
                }
            }
        }

        // Also try conid property directly
        if (conid == 0 && element.TryGetProperty("conid", out var conidProp))
        {
            conid = conidProp.GetInt32();
        }

        if (element.TryGetProperty("_updated", out var updatedProp))
        {
            updated = updatedProp.GetInt64();
        }

        // Extract numeric field keys into the Fields dictionary
        foreach (var prop in element.EnumerateObject())
        {
            if (prop.Name == "topic" || prop.Name == "conid" || prop.Name == "_updated")
            {
                continue;
            }

            // Numeric keys are market data field IDs
            if (int.TryParse(prop.Name, out _))
            {
                fields[prop.Name] = prop.Value.ToString();
            }
        }

        return new MarketDataTick
        {
            Conid = conid,
            Updated = updated,
            Fields = fields.Count > 0 ? fields : null,
        };
    }
}
