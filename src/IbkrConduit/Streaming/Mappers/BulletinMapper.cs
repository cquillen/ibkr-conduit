using System.Text.Json;

namespace IbkrConduit.Streaming.Mappers;

/// <summary>Maps a <c>blt</c> WebSocket frame to a <see cref="BulletinEvent"/>.</summary>
internal static class BulletinMapper
{
    public static BulletinEvent Map(JsonElement element)
    {
        if (!element.TryGetProperty("args", out var args))
        {
            return new BulletinEvent();
        }
        return new BulletinEvent
        {
            Id = args.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? string.Empty : string.Empty,
            Message = args.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? string.Empty : string.Empty,
        };
    }
}
