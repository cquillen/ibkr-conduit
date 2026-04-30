using System.Text.Json;

namespace IbkrConduit.Streaming.Mappers;

/// <summary>Maps a <c>sor</c> WebSocket frame to an <see cref="OrderUpdate"/> via direct JSON deserialization.</summary>
internal static class OrderUpdateMapper
{
    public static OrderUpdate Map(JsonElement element) =>
        JsonSerializer.Deserialize<OrderUpdate>(element.GetRawText()) ?? new OrderUpdate();
}
