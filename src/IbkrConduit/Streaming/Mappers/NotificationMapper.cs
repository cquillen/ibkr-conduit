using System.Text.Json;

namespace IbkrConduit.Streaming.Mappers;

/// <summary>Maps an <c>ntf</c> WebSocket frame to a <see cref="NotificationEvent"/>.</summary>
internal static class NotificationMapper
{
    public static NotificationEvent Map(JsonElement element)
    {
        if (!element.TryGetProperty("args", out var args))
        {
            return new NotificationEvent();
        }
        return new NotificationEvent
        {
            Id = args.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? string.Empty : string.Empty,
            Title = args.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? string.Empty : string.Empty,
            Text = args.TryGetProperty("text", out var textProp) ? textProp.GetString() ?? string.Empty : string.Empty,
            Url = args.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null,
        };
    }
}
