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
    /// <param name="frame">The raw <c>spl</c> frame whose <c>args</c> object carries the per-account P&amp;L.</param>
    /// <param name="onElementDropped">
    /// Invoked once for each <c>args</c> entry that fails to deserialize. Failures are isolated per
    /// entry (PRB-3.2) so one malformed account never discards the frame's other accounts. The caller
    /// reports the drop through the streaming drop taxonomy (count with <c>cause=mapper</c>, log
    /// against the wire topic); when <see langword="null"/> the bad entry is skipped silently.
    /// </param>
    public static IEnumerable<PnlUpdate> MapMany(
        JsonElement frame, Action<Exception>? onElementDropped = null)
    {
        if (!frame.TryGetProperty("args", out var args) || args.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var property in args.EnumerateObject())
        {
            // Deserialize each account entry inside its own guard and materialize before yielding, so
            // a malformed entry is skipped in isolation (log-and-skip via onElementDropped) instead of
            // throwing mid-enumeration and discarding every later account in the frame (PRB-3.2).
            PnlUpdate? pnl;
            try
            {
                pnl = property.Value.Deserialize<PnlUpdate>(StreamingSerialization.Options);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                onElementDropped?.Invoke(ex);
                continue;
            }

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
