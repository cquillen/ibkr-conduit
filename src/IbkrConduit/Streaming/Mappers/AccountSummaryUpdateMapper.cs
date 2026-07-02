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
    public static AccountSummaryUpdate Map(JsonElement frame)
    {
        var rows = new List<AccountSummaryRow>();
        if (frame.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in result.EnumerateArray())
            {
                var row = element.Deserialize<AccountSummaryRow>();
                if (row is not null)
                {
                    rows.Add(row);
                }
            }
        }

        return new AccountSummaryUpdate { AccountId = StreamingFrameHelpers.ParseAccountIdFromTopic(frame), Result = rows };
    }
}
