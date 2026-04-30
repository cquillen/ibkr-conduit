using System.Text.Json;

namespace IbkrConduit.Streaming.Mappers;

/// <summary>Maps an <c>sts</c> WebSocket frame to a <see cref="SessionStatusEvent"/>.</summary>
internal static class SessionStatusMapper
{
    public static SessionStatusEvent Map(JsonElement element)
    {
        if (element.TryGetProperty("args", out var args)
            && args.TryGetProperty("authenticated", out var authProp))
        {
            return new SessionStatusEvent { Authenticated = authProp.GetBoolean() };
        }
        return new SessionStatusEvent();
    }
}
