using System.Text.Json;

namespace IbkrConduit.Streaming.Mappers;

/// <summary>Maps a <c>system</c> WebSocket frame (initial connection or periodic heartbeat) to a <see cref="SystemEvent"/>.</summary>
internal static class SystemEventMapper
{
    public static SystemEvent Map(JsonElement element)
    {
        var username = element.TryGetProperty("success", out var successProp)
            ? successProp.GetString()
            : null;

        long? heartbeatMs = element.TryGetProperty("hb", out var hbProp) && hbProp.ValueKind == JsonValueKind.Number
            ? hbProp.GetInt64()
            : null;

        bool? isFT = element.TryGetProperty("isFT", out var ftProp) && (ftProp.ValueKind == JsonValueKind.True || ftProp.ValueKind == JsonValueKind.False)
            ? ftProp.GetBoolean()
            : null;

        bool? isPaper = element.TryGetProperty("isPaper", out var paperProp) && (paperProp.ValueKind == JsonValueKind.True || paperProp.ValueKind == JsonValueKind.False)
            ? paperProp.GetBoolean()
            : null;

        return new SystemEvent
        {
            Username = username,
            HeartbeatMs = heartbeatMs,
            IsFT = isFT,
            IsPaper = isPaper,
        };
    }
}
