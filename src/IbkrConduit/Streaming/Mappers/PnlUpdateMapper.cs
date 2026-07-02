using System.Text.Json;

namespace IbkrConduit.Streaming.Mappers;

/// <summary>
/// Maps a <c>spl</c> WebSocket frame to zero or more <see cref="PnlUpdate"/> records by
/// fanning out the frame's <c>args</c> object, one entry per account. Each property is
/// keyed <c>"{accountId}.Core"</c>; the account itself is not present in the value.
/// </summary>
internal static class PnlUpdateMapper
{
    /// <summary>
    /// Yields one <see cref="PnlUpdate"/> per property of the frame's <c>args</c> object,
    /// with <see cref="PnlUpdate.AccountId"/> parsed from the property name (the portion
    /// before the first <c>.</c>). Missing or non-object <c>args</c> yields nothing.
    /// </summary>
    public static IEnumerable<PnlUpdate> MapMany(JsonElement frame)
    {
        if (!frame.TryGetProperty("args", out var args) || args.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var property in args.EnumerateObject())
        {
            var pnl = property.Value.Deserialize<PnlUpdate>();
            if (pnl is not null)
            {
                yield return pnl with { AccountId = ParseAccountId(property.Name) };
            }
        }
    }

    private static string ParseAccountId(string key)
    {
        var dotIndex = key.IndexOf('.', StringComparison.Ordinal);
        return dotIndex >= 0 ? key[..dotIndex] : key;
    }
}
