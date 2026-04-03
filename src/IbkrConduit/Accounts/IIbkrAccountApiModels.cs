using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrConduit.Accounts;

/// <summary>
/// Response from GET /iserver/accounts.
/// </summary>
/// <param name="Accounts">List of account identifiers.</param>
/// <param name="SelectedAccount">The currently selected account.</param>
/// <param name="AcctProps">Per-account property flags (keyed by account ID).</param>
/// <param name="Aliases">Per-account alias mappings (keyed by account ID).</param>
/// <param name="AllowFeatures">Feature flags for the session.</param>
/// <param name="ChartPeriods">Allowed chart periods per asset class.</param>
/// <param name="Groups">Account groups.</param>
/// <param name="Profiles">Account profiles.</param>
/// <param name="ServerInfo">Server identification details.</param>
/// <param name="SessionId">Current session identifier.</param>
/// <param name="IsFt">Whether this is a financial tools session.</param>
/// <param name="IsPaper">Whether this is a paper trading account.</param>
[ExcludeFromCodeCoverage]
public record IserverAccountsResponse(
    [property: JsonPropertyName("accounts")] List<string> Accounts,
    [property: JsonPropertyName("selectedAccount")] string SelectedAccount,
    [property: JsonPropertyName("acctProps")] JsonElement? AcctProps = null,
    [property: JsonPropertyName("aliases")] JsonElement? Aliases = null,
    [property: JsonPropertyName("allowFeatures")] JsonElement? AllowFeatures = null,
    [property: JsonPropertyName("chartPeriods")] JsonElement? ChartPeriods = null,
    [property: JsonPropertyName("groups")] JsonElement? Groups = null,
    [property: JsonPropertyName("profiles")] JsonElement? Profiles = null,
    [property: JsonPropertyName("serverInfo")] JsonElement? ServerInfo = null,
    [property: JsonPropertyName("sessionId")] string? SessionId = null,
    [property: JsonPropertyName("isFT")] bool? IsFt = null,
    [property: JsonPropertyName("isPaper")] bool? IsPaper = null)
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
/// <param name="Success">Success message describing the switch result.</param>
[ExcludeFromCodeCoverage]
public record SwitchAccountResponse(
    [property: JsonPropertyName("success")] string? Success)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

