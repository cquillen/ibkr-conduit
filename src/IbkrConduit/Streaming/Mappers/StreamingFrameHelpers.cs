using System.Text.Json;

namespace IbkrConduit.Streaming.Mappers;

/// <summary>Shared parsing helpers for account-scoped WebSocket frames.</summary>
internal static class StreamingFrameHelpers
{
    /// <summary>
    /// Extracts the account ID embedded in a frame's <c>topic</c> (e.g. <c>"ssd+DU1234567"</c>
    /// → <c>"DU1234567"</c>). Returns an empty string when <c>topic</c> is missing, not a
    /// string, or contains no <c>+</c> separator.
    /// </summary>
    public static string ParseAccountIdFromTopic(JsonElement frame)
    {
        if (!frame.TryGetProperty("topic", out var topic) || topic.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        var value = topic.GetString() ?? string.Empty;
        var plusIndex = value.IndexOf('+', StringComparison.Ordinal);
        return plusIndex >= 0 ? value[(plusIndex + 1)..] : string.Empty;
    }
}
