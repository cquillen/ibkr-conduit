using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrConduit.Allocation;

/// <summary>
/// A key-value data entry for an allocation sub-account (e.g., NetLiquidation, AvailableEquity).
/// </summary>
/// <param name="Key">The data key (e.g., "NetLiquidation", "AvailableEquity").</param>
/// <param name="Value">The data value as a string.</param>
[ExcludeFromCodeCoverage]
public record AllocationAccountDataEntry(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("value")] string Value)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// An allocatable sub-account with its balance data.
/// </summary>
/// <param name="Name">The account identifier.</param>
/// <param name="Data">Balance data entries for the account.</param>
[ExcludeFromCodeCoverage]
public record AllocationAccountData(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("data")] List<AllocationAccountDataEntry> Data)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Response from GET /iserver/account/allocation/accounts.
/// </summary>
/// <param name="Accounts">Array of sub-accounts with balance data.</param>
[ExcludeFromCodeCoverage]
public record AllocationAccountsResponse(
    [property: JsonPropertyName("accounts")] List<AllocationAccountData> Accounts)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Summary of a single allocation group from GET /iserver/account/allocation/group.
/// </summary>
/// <param name="Name">The group name.</param>
/// <param name="AllocationMethod">The allocation method code.</param>
/// <param name="Size">Number of sub-accounts in the group.</param>
[ExcludeFromCodeCoverage]
public record AllocationGroupSummary(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("allocation_method")] string AllocationMethod,
    [property: JsonPropertyName("size")] int Size)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Response from GET /iserver/account/allocation/group.
/// </summary>
/// <param name="Data">Array of allocation group summaries.</param>
[ExcludeFromCodeCoverage]
public record AllocationGroupListResponse(
    [property: JsonPropertyName("data")] List<AllocationGroupSummary> Data)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// An account entry within an allocation group detail.
/// </summary>
/// <param name="Name">The account identifier.</param>
/// <param name="Amount">The distribution amount for user-defined methods.</param>
[ExcludeFromCodeCoverage]
public record AllocationGroupAccount(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("amount")] decimal Amount)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Detailed allocation group from POST /iserver/account/allocation/group/single.
/// Also used as the request body for adding and modifying groups.
/// </summary>
/// <param name="Name">The group name.</param>
/// <param name="Accounts">The accounts in this group.</param>
/// <param name="DefaultMethod">The allocation method code.</param>
[ExcludeFromCodeCoverage]
public record AllocationGroupDetail(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("accounts")] List<AllocationGroupAccount> Accounts,
    [property: JsonPropertyName("default_method")] string DefaultMethod)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Request body for adding or modifying an allocation group.
/// </summary>
/// <param name="Name">The group name (or new name if renaming).</param>
/// <param name="Accounts">The accounts in this group.</param>
/// <param name="DefaultMethod">The allocation method code.</param>
/// <param name="PrevName">Previous group name (only for rename via PUT).</param>
[ExcludeFromCodeCoverage]
public record AllocationGroupRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("accounts")] List<AllocationGroupAccount> Accounts,
    [property: JsonPropertyName("default_method")] string DefaultMethod,
    [property: JsonPropertyName("prev_name")] string? PrevName = null);

/// <summary>
/// Request body referencing a group by name (for single/delete operations).
/// </summary>
/// <param name="Name">The allocation group name.</param>
[ExcludeFromCodeCoverage]
public record AllocationGroupNameRequest(
    [property: JsonPropertyName("name")] string Name);

/// <summary>
/// Response from GET /iserver/account/allocation/presets.
/// </summary>
/// <param name="GroupAutoClosePositions">Whether groups auto-close positions.</param>
/// <param name="DefaultMethodForAll">Default allocation method for all groups.</param>
/// <param name="ProfilesAutoClosePositions">Whether profiles auto-close positions.</param>
/// <param name="StrictCreditCheck">Whether strict credit check is enabled.</param>
/// <param name="GroupProportionalAllocation">Whether proportional allocation is used.</param>
[ExcludeFromCodeCoverage]
public record AllocationPresetsResponse(
    [property: JsonPropertyName("group_auto_close_positions")] bool GroupAutoClosePositions,
    [property: JsonPropertyName("default_method_for_all")] string DefaultMethodForAll,
    [property: JsonPropertyName("profiles_auto_close_positions")] bool ProfilesAutoClosePositions,
    [property: JsonPropertyName("strict_credit_check")] bool StrictCreditCheck,
    [property: JsonPropertyName("group_proportional_allocation")] bool GroupProportionalAllocation)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Request body for POST /iserver/account/allocation/presets.
/// </summary>
/// <param name="DefaultMethodForAll">Default allocation method for all groups.</param>
/// <param name="GroupAutoClosePositions">Whether groups auto-close positions.</param>
/// <param name="ProfilesAutoClosePositions">Whether profiles auto-close positions.</param>
/// <param name="StrictCreditCheck">Whether strict credit check is enabled.</param>
/// <param name="GroupProportionalAllocation">Whether proportional allocation is used.</param>
[ExcludeFromCodeCoverage]
public record AllocationPresetsRequest(
    [property: JsonPropertyName("default_method_for_all")] string DefaultMethodForAll,
    [property: JsonPropertyName("group_auto_close_positions")] bool GroupAutoClosePositions,
    [property: JsonPropertyName("profiles_auto_close_positions")] bool ProfilesAutoClosePositions,
    [property: JsonPropertyName("strict_credit_check")] bool StrictCreditCheck,
    [property: JsonPropertyName("group_proportional_allocation")] bool GroupProportionalAllocation);

/// <summary>
/// Generic success response from FA allocation mutation endpoints.
/// </summary>
/// <param name="Success">Whether the operation succeeded.</param>
[ExcludeFromCodeCoverage]
public record AllocationSuccessResponse(
    [property: JsonPropertyName("success")] bool Success)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
