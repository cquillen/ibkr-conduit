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

/// <summary>
/// Response from GET /acesws/{accountId}/signatures-and-owners.
/// </summary>
/// <param name="AccountId">The account identifier.</param>
/// <param name="Users">List of users associated with the account.</param>
/// <param name="Applicant">Applicant signature information.</param>
[ExcludeFromCodeCoverage]
public record SignaturesAndOwnersResponse(
    [property: JsonPropertyName("accountId")] string AccountId,
    [property: JsonPropertyName("users")] List<AccountUser> Users,
    [property: JsonPropertyName("applicant")] AccountApplicant? Applicant = null);

/// <summary>
/// A user associated with an account.
/// </summary>
/// <param name="RoleId">The user's role (e.g., "OWNER").</param>
/// <param name="HasRightCodeInd">Whether the user has right code indicator.</param>
/// <param name="UserName">The user's login name.</param>
/// <param name="Entity">Entity details for the user.</param>
[ExcludeFromCodeCoverage]
public record AccountUser(
    [property: JsonPropertyName("roleId")] string RoleId,
    [property: JsonPropertyName("hasRightCodeInd")] bool HasRightCodeInd,
    [property: JsonPropertyName("userName")] string UserName,
    [property: JsonPropertyName("entity")] AccountEntity? Entity = null);

/// <summary>
/// Entity details for an account user (individual or organization).
/// </summary>
/// <param name="FirstName">First name (for individuals).</param>
/// <param name="LastName">Last name (for individuals).</param>
/// <param name="EntityType">The entity type (e.g., "INDIVIDUAL").</param>
/// <param name="EntityName">The full entity name.</param>
/// <param name="DateOfBirth">Date of birth in YYYY-MM-DD format.</param>
[ExcludeFromCodeCoverage]
public record AccountEntity(
    [property: JsonPropertyName("firstName")] string? FirstName = null,
    [property: JsonPropertyName("lastName")] string? LastName = null,
    [property: JsonPropertyName("entityType")] string? EntityType = null,
    [property: JsonPropertyName("entityName")] string? EntityName = null,
    [property: JsonPropertyName("dateOfBirth")] string? DateOfBirth = null);

/// <summary>
/// Applicant information with signatures.
/// </summary>
/// <param name="Signatures">List of signature strings.</param>
[ExcludeFromCodeCoverage]
public record AccountApplicant(
    [property: JsonPropertyName("signatures")] List<string> Signatures);

/// <summary>
/// Request body for POST /iserver/dynaccount to set the dynamic account.
/// </summary>
/// <param name="AcctId">The account identifier to set.</param>
[ExcludeFromCodeCoverage]
public record SetDynamicAccountRequest(
    [property: JsonPropertyName("acctId")] string AcctId);

/// <summary>
/// Response from POST /iserver/dynaccount.
/// </summary>
/// <param name="Set">Whether the account change was set.</param>
/// <param name="AcctId">The account that was switched to.</param>
[ExcludeFromCodeCoverage]
public record SetDynamicAccountResponse(
    [property: JsonPropertyName("set")] bool? Set = null,
    [property: JsonPropertyName("acctId")] string? AcctId = null);

/// <summary>
/// Response from GET /iserver/account/search/{pattern}.
/// </summary>
/// <param name="MatchedAccounts">Accounts matching the search pattern.</param>
/// <param name="Pattern">The search pattern used for the request.</param>
[ExcludeFromCodeCoverage]
public record DynamicAccountSearchResponse(
    [property: JsonPropertyName("matchedAccounts")] List<DynamicAccountSearchResult> MatchedAccounts,
    [property: JsonPropertyName("pattern")] string? Pattern = null);

/// <summary>
/// An account entry returned from a dynamic account search.
/// </summary>
/// <param name="AccountId">The account identifier.</param>
/// <param name="Alias">Alternative name for the account (often same as AccountId).</param>
/// <param name="AllocationId">Internal allocation identifier for the account.</param>
[ExcludeFromCodeCoverage]
public record DynamicAccountSearchResult(
    [property: JsonPropertyName("accountId")] string? AccountId = null,
    [property: JsonPropertyName("alias")] string? Alias = null,
    [property: JsonPropertyName("allocationId")] string? AllocationId = null);

/// <summary>
/// A cash balance entry within an account summary overview.
/// </summary>
/// <param name="Currency">The currency code (e.g., "USD").</param>
/// <param name="Balance">The current cash balance in this currency.</param>
/// <param name="SettledCash">The settled cash amount in this currency.</param>
[ExcludeFromCodeCoverage]
public record AccountSummaryCashBalance(
    [property: JsonPropertyName("currency")] string? Currency = null,
    [property: JsonPropertyName("balance")] double? Balance = null,
    [property: JsonPropertyName("settledCash")] double? SettledCash = null);

/// <summary>
/// Response from GET /iserver/account/{accountId}/summary.
/// Provides a high-level overview of account balances, margins, and buying power.
/// </summary>
/// <param name="AccountType">The type of account.</param>
/// <param name="Status">The account status.</param>
/// <param name="Balance">Current account balance.</param>
/// <param name="Sma">Special Memorandum Account value.</param>
/// <param name="BuyingPower">Total buying power available.</param>
/// <param name="AvailableFunds">Funds available for trading.</param>
/// <param name="ExcessLiquidity">Excess liquidity in the account.</param>
/// <param name="NetLiquidationValue">Net liquidation value of the account.</param>
/// <param name="EquityWithLoanValue">Equity with loan value.</param>
/// <param name="RegTLoan">Regulation T loan amount.</param>
/// <param name="SecuritiesGvp">Gross value of securities positions.</param>
/// <param name="TotalCashValue">Total cash value across all currencies.</param>
/// <param name="AccruedInterest">Accrued interest amount.</param>
/// <param name="RegTMargin">Regulation T margin requirement.</param>
/// <param name="InitialMargin">Initial margin requirement.</param>
/// <param name="MaintenanceMargin">Maintenance margin requirement.</param>
/// <param name="CashBalances">Per-currency cash balance breakdown.</param>
[ExcludeFromCodeCoverage]
public record AccountSummaryOverview(
    [property: JsonPropertyName("accountType")] string? AccountType = null,
    [property: JsonPropertyName("status")] string? Status = null,
    [property: JsonPropertyName("balance")] double? Balance = null,
    [property: JsonPropertyName("SMA")] double? Sma = null,
    [property: JsonPropertyName("buyingPower")] double? BuyingPower = null,
    [property: JsonPropertyName("availableFunds")] double? AvailableFunds = null,
    [property: JsonPropertyName("excessLiquidity")] double? ExcessLiquidity = null,
    [property: JsonPropertyName("netLiquidationValue")] double? NetLiquidationValue = null,
    [property: JsonPropertyName("equityWithLoanValue")] double? EquityWithLoanValue = null,
    [property: JsonPropertyName("regTLoan")] double? RegTLoan = null,
    [property: JsonPropertyName("securitiesGVP")] double? SecuritiesGvp = null,
    [property: JsonPropertyName("totalCashValue")] double? TotalCashValue = null,
    [property: JsonPropertyName("accruedInterest")] double? AccruedInterest = null,
    [property: JsonPropertyName("regTMargin")] double? RegTMargin = null,
    [property: JsonPropertyName("initialMargin")] double? InitialMargin = null,
    [property: JsonPropertyName("maintenanceMargin")] double? MaintenanceMargin = null,
    [property: JsonPropertyName("cashBalances")] List<AccountSummaryCashBalance>? CashBalances = null)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

