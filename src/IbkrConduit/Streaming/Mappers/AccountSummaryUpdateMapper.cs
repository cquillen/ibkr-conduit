using System.Text.Json;

namespace IbkrConduit.Streaming.Mappers;

/// <summary>
/// Maps an <c>ssd</c> WebSocket frame to an <see cref="AccountSummaryUpdate"/> by reading
/// the frame's <c>result</c> array and parsing the account ID out of <c>topic</c>
/// (e.g. <c>"ssd+DU1234567"</c>).
/// </summary>
internal static class AccountSummaryUpdateMapper
{
    /// <summary>Maps one <c>ssd</c> frame to an <see cref="AccountSummaryUpdate"/>. Missing or non-array <c>result</c> yields an empty row list.</summary>
    /// <param name="frame">The raw <c>ssd</c> frame whose <c>result</c> array carries the summary rows.</param>
    /// <param name="onRowDropped">
    /// Invoked once for each <c>result</c> row that fails to deserialize. Failures are isolated per row
    /// (WIR-1) so one malformed row never discards the whole frame — a real <c>ssd</c> frame carries
    /// ~135 rows. The caller reports the drop through the streaming drop taxonomy (count with
    /// <c>cause=mapper</c>, log against the wire topic); when <see langword="null"/> the bad row is
    /// skipped silently.
    /// </param>
    /// <param name="onRequiredMoneyFieldAbsent">
    /// Invoked once per absent required money field (<c>monetaryValue</c>) on each mapped monetary row
    /// — the WIR-5 census signal. Only a monetary row (one that names a <c>currency</c>) is censused;
    /// a non-monetary row (which carries <c>value</c>) is exempt, so a Cushion-style row never raises a
    /// false census. A dropped row is never censused. When <see langword="null"/> the census is skipped.
    /// </param>
    public static AccountSummaryUpdate Map(
        JsonElement frame,
        Action<Exception>? onRowDropped = null,
        Action<string>? onRequiredMoneyFieldAbsent = null)
    {
        var rows = new List<AccountSummaryRow>();
        if (frame.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in result.EnumerateArray())
            {
                // Deserialize each row inside its own guard so a malformed row is skipped in isolation
                // (log-and-skip via onRowDropped) instead of throwing and discarding the whole frame's
                // remaining rows (WIR-1).
                AccountSummaryRow? row;
                try
                {
                    row = element.Deserialize<AccountSummaryRow>(StreamingSerialization.Options);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    onRowDropped?.Invoke(ex);
                    continue;
                }

                if (row is not null)
                {
                    // Census only monetary rows (a currency names the pricing/balance key), so a
                    // non-monetary row (with `value`) never raises a false monetaryValue census.
                    if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("currency", out _))
                    {
                        MoneyFieldCensus.ReportAbsent(
                            element, MoneyFieldCensus.AccountSummaryFields, onRequiredMoneyFieldAbsent);
                    }

                    rows.Add(row);
                }
            }
        }

        return new AccountSummaryUpdate { AccountId = StreamingFrameHelpers.ParseAccountIdFromTopic(frame), Result = rows };
    }
}
