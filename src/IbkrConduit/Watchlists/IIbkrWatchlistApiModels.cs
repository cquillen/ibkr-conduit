using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrConduit.Watchlists;

/// <summary>
/// Request body for POST /iserver/watchlist to create a watchlist.
/// </summary>
/// <param name="Id">The watchlist name/identifier.</param>
/// <param name="Name">The display name of the watchlist.</param>
/// <param name="Rows">The rows (instruments) in the watchlist.</param>
[ExcludeFromCodeCoverage]
public record CreateWatchlistRequest(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("rows")] List<WatchlistRow> Rows);

/// <summary>
/// A row in a watchlist creation request.
/// </summary>
/// <param name="C">The contract identifier (conid).</param>
/// <param name="H">The header/label for this row (optional, used for blank separators).</param>
[ExcludeFromCodeCoverage]
public record WatchlistRow(
    [property: JsonPropertyName("C")] int C,
    [property: JsonPropertyName("H")] string? H = null);

/// <summary>
/// Response from POST /iserver/watchlist.
/// </summary>
/// <param name="Id">The identifier of the created watchlist.</param>
/// <param name="Hash">The hash value of the watchlist.</param>
/// <param name="Name">The display name of the watchlist.</param>
/// <param name="ReadOnly">Whether the watchlist is read-only.</param>
/// <param name="Instruments">The instruments in the watchlist.</param>
[ExcludeFromCodeCoverage]
public record CreateWatchlistResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("hash")] string Hash,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("readOnly")] bool ReadOnly,
    [property: JsonPropertyName("instruments")] List<WatchlistInstrument> Instruments)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Wrapper response from GET /iserver/watchlists.
/// </summary>
/// <param name="Data">The data payload containing watchlist summaries.</param>
/// <param name="Action">The action type (e.g., "content").</param>
/// <param name="Mid">The message identifier.</param>
[ExcludeFromCodeCoverage]
public record GetWatchlistsResponse(
    [property: JsonPropertyName("data")] GetWatchlistsData Data,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("MID")] string Mid)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Data payload within the GET /iserver/watchlists response.
/// </summary>
/// <param name="ScannersOnly">Whether only scanners are shown.</param>
/// <param name="ShowScanners">Whether scanners are visible.</param>
/// <param name="BulkDelete">Whether bulk delete is enabled.</param>
/// <param name="UserLists">The user's watchlists.</param>
[ExcludeFromCodeCoverage]
public record GetWatchlistsData(
    [property: JsonPropertyName("scanners_only")] bool ScannersOnly,
    [property: JsonPropertyName("show_scanners")] bool ShowScanners,
    [property: JsonPropertyName("bulk_delete")] bool BulkDelete,
    [property: JsonPropertyName("user_lists")] List<WatchlistSummary> UserLists)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Summary of a watchlist from GET /iserver/watchlists.
/// </summary>
/// <param name="Id">The watchlist identifier.</param>
/// <param name="Name">The watchlist name.</param>
/// <param name="Modified">Last modification timestamp.</param>
/// <param name="IsOpen">Whether the watchlist is currently open.</param>
/// <param name="ReadOnly">Whether the watchlist is read-only.</param>
/// <param name="Type">The type of the list (e.g., "watchlist").</param>
[ExcludeFromCodeCoverage]
public record WatchlistSummary(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("modified")] long Modified,
    [property: JsonPropertyName("is_open")] bool IsOpen,
    [property: JsonPropertyName("read_only")] bool ReadOnly,
    [property: JsonPropertyName("type")] string Type)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Detailed watchlist from GET /iserver/watchlist.
/// </summary>
/// <param name="Id">The watchlist identifier.</param>
/// <param name="Hash">The hash value of the watchlist.</param>
/// <param name="Name">The watchlist name.</param>
/// <param name="ReadOnly">Whether the watchlist is read-only.</param>
/// <param name="Instruments">The instruments in the watchlist.</param>
[ExcludeFromCodeCoverage]
public record WatchlistDetail(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("hash")] string Hash,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("readOnly")] bool ReadOnly,
    [property: JsonPropertyName("instruments")] List<WatchlistInstrument> Instruments)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// An instrument within a watchlist detail response.
/// </summary>
/// <param name="St">The security type abbreviation (e.g., "STK").</param>
/// <param name="C">The contract identifier as a string.</param>
/// <param name="Conid">The contract identifier as an integer.</param>
/// <param name="Name">The display name of the instrument.</param>
/// <param name="FullName">The full symbol name (e.g., "SPY").</param>
/// <param name="AssetClass">The asset class (e.g., "STK").</param>
/// <param name="Ticker">The ticker symbol.</param>
/// <param name="ChineseName">The Chinese name of the instrument.</param>
[ExcludeFromCodeCoverage]
public record WatchlistInstrument(
    [property: JsonPropertyName("ST")] string St,
    [property: JsonPropertyName("C")] string C,
    [property: JsonPropertyName("conid")] int Conid,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("fullName")] string FullName,
    [property: JsonPropertyName("assetClass")] string AssetClass,
    [property: JsonPropertyName("ticker")] string Ticker,
    [property: JsonPropertyName("chineseName")] string? ChineseName)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Wrapper response from DELETE /iserver/watchlist.
/// </summary>
/// <param name="Data">The data payload containing the deleted watchlist ID.</param>
/// <param name="Action">The action type (e.g., "context").</param>
/// <param name="Mid">The message identifier.</param>
[ExcludeFromCodeCoverage]
public record DeleteWatchlistResponse(
    [property: JsonPropertyName("data")] DeleteWatchlistData Data,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("MID")] string Mid)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Data payload within the DELETE /iserver/watchlist response.
/// </summary>
/// <param name="Deleted">The identifier of the deleted watchlist.</param>
[ExcludeFromCodeCoverage]
public record DeleteWatchlistData(
    [property: JsonPropertyName("deleted")] string Deleted)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
