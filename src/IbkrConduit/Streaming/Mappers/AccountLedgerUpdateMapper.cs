using System.Text.Json;

namespace IbkrConduit.Streaming.Mappers;

/// <summary>
/// Maps an <c>sld</c> WebSocket frame to an <see cref="AccountLedgerUpdate"/> by reading
/// the frame's <c>result</c> array and parsing the account ID out of <c>topic</c>
/// (e.g. <c>"sld+DU1234567"</c>).
/// </summary>
internal static class AccountLedgerUpdateMapper
{
    /// <summary>Maps one <c>sld</c> frame to an <see cref="AccountLedgerUpdate"/>. Missing or non-array <c>result</c> yields an empty row list.</summary>
    /// <param name="frame">The raw <c>sld</c> frame whose <c>result</c> array carries the ledger rows.</param>
    /// <param name="onRowDropped">
    /// Invoked once for each <c>result</c> row that fails to deserialize. Failures are isolated per row
    /// (WIR-1) so one malformed currency row never discards the frame's other currencies. The caller
    /// reports the drop through the streaming drop taxonomy (count with <c>cause=mapper</c>, log
    /// against the wire topic); when <see langword="null"/> the bad row is skipped silently.
    /// </param>
    /// <param name="onRequiredMoneyFieldAbsent">
    /// Invoked once per absent required money field (<c>netLiquidationValue</c>) on each mapped
    /// substantive row — the WIR-5 census signal. Only a substantive row (one reporting a
    /// <c>cashbalance</c>) is censused; a blank 10-second no-change entry (only <c>key</c> +
    /// <c>timestamp</c>) is exempt, so it never raises a false census. A dropped row is never censused.
    /// When <see langword="null"/> the census is skipped.
    /// </param>
    public static AccountLedgerUpdate Map(
        JsonElement frame,
        Action<Exception>? onRowDropped = null,
        Action<string>? onRequiredMoneyFieldAbsent = null)
    {
        var rows = new List<AccountLedgerRow>();
        if (frame.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in result.EnumerateArray())
            {
                // Deserialize each row inside its own guard so a malformed row is skipped in isolation
                // (log-and-skip via onRowDropped) instead of throwing and discarding the frame's other
                // currency rows (WIR-1).
                AccountLedgerRow? row;
                try
                {
                    row = element.Deserialize<AccountLedgerRow>(StreamingSerialization.Options);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    onRowDropped?.Invoke(ex);
                    continue;
                }

                if (row is not null)
                {
                    // Census only substantive rows (a cashbalance marks a non-blank interval), so a
                    // blank 10-second no-change entry never raises a false netLiquidationValue census.
                    if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("cashbalance", out _))
                    {
                        MoneyFieldCensus.ReportAbsent(
                            element, MoneyFieldCensus.AccountLedgerFields, onRequiredMoneyFieldAbsent);
                    }

                    rows.Add(row);
                }
            }
        }

        return new AccountLedgerUpdate { AccountId = StreamingFrameHelpers.ParseAccountIdFromTopic(frame), Result = rows };
    }
}
