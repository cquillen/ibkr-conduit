using System.Text.Json;

namespace IbkrConduit.Streaming.Mappers;

/// <summary>Maps an <c>ssd</c> WebSocket frame to an <see cref="AccountSummaryUpdate"/> via direct JSON deserialization.</summary>
internal static class AccountSummaryUpdateMapper
{
    public static AccountSummaryUpdate Map(JsonElement element) =>
        JsonSerializer.Deserialize<AccountSummaryUpdate>(element.GetRawText()) ?? new AccountSummaryUpdate();
}
