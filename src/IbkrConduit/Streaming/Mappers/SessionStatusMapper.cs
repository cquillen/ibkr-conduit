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
            Authenticated = ReadTolerantBool(args, "authenticated"),
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
    /// Reads the <c>authenticated</c> liveness verdict tolerantly, mirroring the shapes
    /// <see cref="Serialization.FlexibleBoolJsonConverter"/> accepts: a real <c>true</c>/<c>false</c>,
    /// a quoted <c>"true"</c>/<c>"false"</c>/<c>"1"</c>/<c>"0"</c> (case-insensitive), or a bare
    /// number (<c>0</c> is false, non-zero is true). IBKR type-drifts boolean-ish flags to these
    /// alternate shapes, so a string-encoded session-death frame must still surface its verdict
    /// rather than being dropped as malformed (GAP2-1). An absent, empty, or genuinely unrecognized
    /// value maps to <c>null</c> — the frame still surfaces, and absence is never fabricated into a
    /// verdict (ADR-0001) nor thrown (which would re-open the drop this fix closes).
    /// </summary>
    private static bool? ReadTolerantBool(JsonElement args, string propertyName)
    {
        if (!args.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => prop.TryGetInt64(out var n) ? n != 0 : null,
            JsonValueKind.String => ParseBoolString(prop.GetString()),
            _ => null,
        };
    }

    /// <summary>
    /// Parses the quoted boolean forms IBKR emits (<c>"1"</c>/<c>"true"</c> → <c>true</c>,
    /// <c>"0"</c>/<c>"false"</c> → <c>false</c>, case-insensitive). Empty/whitespace and any
    /// unrecognized token map to <c>null</c>.
    /// </summary>
    private static bool? ParseBoolString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "1" or "true" => true,
            "0" or "false" => false,
            _ => null,
        };
    }

    /// <summary>
    /// Reads a JSON string field defensively: a present string (including empty) is preserved as-is;
    /// an absent (or non-string) field maps to <c>null</c> (ADR-0001 presence semantics).
    /// </summary>
    private static string? ReadString(JsonElement args, string propertyName) =>
        args.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
}
