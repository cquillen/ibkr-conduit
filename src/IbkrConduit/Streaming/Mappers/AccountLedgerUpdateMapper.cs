using System.Text.Json;

namespace IbkrConduit.Streaming.Mappers;

/// <summary>Maps an <c>sld</c> WebSocket frame to an <see cref="AccountLedgerUpdate"/> via direct JSON deserialization.</summary>
internal static class AccountLedgerUpdateMapper
{
    public static AccountLedgerUpdate Map(JsonElement element) =>
        JsonSerializer.Deserialize<AccountLedgerUpdate>(element.GetRawText()) ?? new AccountLedgerUpdate();
}
