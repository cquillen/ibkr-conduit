using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrConduit.Accounts;

/// <summary>
/// Response from GET /iserver/accounts.
/// </summary>
/// <param name="Accounts">List of account identifiers.</param>
/// <param name="SelectedAccount">The currently selected account.</param>
[ExcludeFromCodeCoverage]
public record IserverAccountsResponse(
    [property: JsonPropertyName("accounts")] List<string> Accounts,
    [property: JsonPropertyName("selectedAccount")] string SelectedAccount)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Request body for POST /iserver/account to switch the active account.
/// </summary>
/// <param name="AcctId">The account identifier to switch to.</param>
[ExcludeFromCodeCoverage]
public record SwitchAccountRequest(
    [property: JsonPropertyName("acctId")] string AcctId);

/// <summary>
/// Response from POST /iserver/account.
/// </summary>
/// <param name="Set">Whether the account was set successfully.</param>
/// <param name="SelectedAccount">The newly selected account identifier.</param>
[ExcludeFromCodeCoverage]
public record SwitchAccountResponse(
    [property: JsonPropertyName("set")] bool Set,
    [property: JsonPropertyName("selectedAccount")] string SelectedAccount)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Request body for POST /iserver/dynaccount to set the dynamic account.
/// </summary>
/// <param name="AcctId">The account identifier to set as dynamic.</param>
[ExcludeFromCodeCoverage]
public record DynAccountRequest(
    [property: JsonPropertyName("acctId")] string AcctId);

/// <summary>
/// Response from POST /iserver/dynaccount.
/// </summary>
/// <param name="Set">Whether the dynamic account was set successfully.</param>
/// <param name="SelectedAccount">The newly selected account identifier.</param>
[ExcludeFromCodeCoverage]
public record DynAccountResponse(
    [property: JsonPropertyName("set")] bool Set,
    [property: JsonPropertyName("selectedAccount")] string SelectedAccount)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Result from GET /iserver/account/search/{pattern}.
/// </summary>
/// <param name="AccountId">The account identifier.</param>
/// <param name="AccountTitle">The account title.</param>
/// <param name="AccountType">The account type.</param>
[ExcludeFromCodeCoverage]
public record AccountSearchResult(
    [property: JsonPropertyName("accountId")] string AccountId,
    [property: JsonPropertyName("accountTitle")] string AccountTitle,
    [property: JsonPropertyName("accountType")] string AccountType)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Response from GET /iserver/account/{accountId}.
/// </summary>
/// <param name="AccountId">The account identifier.</param>
/// <param name="AccountTitle">The account title.</param>
/// <param name="AccountType">The account type.</param>
[ExcludeFromCodeCoverage]
public record IserverAccountInfo(
    [property: JsonPropertyName("accountId")] string AccountId,
    [property: JsonPropertyName("accountTitle")] string AccountTitle,
    [property: JsonPropertyName("accountType")] string AccountType)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
