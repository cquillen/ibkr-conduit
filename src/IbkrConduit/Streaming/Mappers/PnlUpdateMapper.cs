using System.Text.Json;

namespace IbkrConduit.Streaming.Mappers;

/// <summary>Maps a <c>spl</c> WebSocket frame to a <see cref="PnlUpdate"/> via direct JSON deserialization.</summary>
internal static class PnlUpdateMapper
{
    public static PnlUpdate Map(JsonElement element) =>
        JsonSerializer.Deserialize<PnlUpdate>(element.GetRawText()) ?? new PnlUpdate();
}
