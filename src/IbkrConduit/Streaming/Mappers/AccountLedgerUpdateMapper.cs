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
    public static AccountLedgerUpdate Map(JsonElement frame)
    {
        var rows = new List<AccountLedgerRow>();
        if (frame.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in result.EnumerateArray())
            {
                var row = element.Deserialize<AccountLedgerRow>(StreamingSerialization.Options);
                if (row is not null)
                {
                    rows.Add(row);
                }
            }
        }

        return new AccountLedgerUpdate { AccountId = StreamingFrameHelpers.ParseAccountIdFromTopic(frame), Result = rows };
    }
}
