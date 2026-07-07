using System.Text.Json;

namespace IbkrConduit.Streaming.Mappers;

/// <summary>Maps an <c>sts</c> WebSocket frame to a <see cref="SessionStatusEvent"/>.</summary>
internal static class SessionStatusMapper
{
    public static SessionStatusEvent Map(JsonElement element)
    {
        if (!element.TryGetProperty("args", out var args) || args.ValueKind != JsonValueKind.Object)
        {
            return new SessionStatusEvent();
        }

        return new SessionStatusEvent
        {
            Authenticated = ReadBool(args, "authenticated"),
            Competing = ReadBool(args, "competing"),
            FailReason = ReadString(args, "fail"),
        };
    }

    /// <summary>
    /// Reads a JSON boolean field defensively: a real <c>true</c>/<c>false</c> maps to that value;
    /// an absent (or non-boolean) field maps to <c>null</c> so absence is never fabricated into a
    /// verdict (ADR-0001).
    /// </summary>
    private static bool? ReadBool(JsonElement args, string propertyName) =>
        args.TryGetProperty(propertyName, out var prop) && prop.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? prop.GetBoolean()
            : null;

    /// <summary>
    /// Reads a JSON string field defensively: a present string (including empty) is preserved as-is;
    /// an absent (or non-string) field maps to <c>null</c> (ADR-0001 presence semantics).
    /// </summary>
    private static string? ReadString(JsonElement args, string propertyName) =>
        args.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
}
