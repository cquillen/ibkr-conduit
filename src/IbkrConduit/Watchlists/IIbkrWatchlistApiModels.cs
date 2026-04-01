using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrConduit.Watchlists;

/// <summary>
/// Request body for POST /iserver/watchlist to create a watchlist.
/// </summary>
/// <param name="Id">The watchlist name/identifier.</param>
/// <param name="Rows">The rows (instruments) in the watchlist.</param>
[ExcludeFromCodeCoverage]
public record CreateWatchlistRequest(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("rows")] List<WatchlistRow> Rows);

/// <summary>
/// A row in a watchlist creation request.
/// </summary>
/// <param name="C">The contract identifier (conid).</param>
/// <param name="H">The header/label for this row.</param>
[ExcludeFromCodeCoverage]
public record WatchlistRow(
    [property: JsonPropertyName("C")] int C,
    [property: JsonPropertyName("H")] string H);

/// <summary>
/// Response from POST /iserver/watchlist.
/// </summary>
/// <param name="Id">The identifier of the created watchlist.</param>
[ExcludeFromCodeCoverage]
public record CreateWatchlistResponse(
    [property: JsonPropertyName("id")] string Id)
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
/// <param name="Instruments">Number of instruments in the watchlist.</param>
[ExcludeFromCodeCoverage]
public record WatchlistSummary(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("modified")] long Modified,
    [property: JsonPropertyName("instruments")] int Instruments)
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
/// <param name="Name">The watchlist name.</param>
/// <param name="Rows">The rows (instruments) in the watchlist.</param>
[ExcludeFromCodeCoverage]
public record WatchlistDetail(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("rows")] List<WatchlistDetailRow> Rows)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// A row in a watchlist detail response.
/// </summary>
/// <param name="C">The contract identifier (conid).</param>
/// <param name="H">The header/label for this row.</param>
/// <param name="Sym">The symbol for this row.</param>
[ExcludeFromCodeCoverage]
public record WatchlistDetailRow(
    [property: JsonPropertyName("C")] int C,
    [property: JsonPropertyName("H")] string H,
    [property: JsonPropertyName("sym")] string? Sym)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Response from DELETE /iserver/watchlist.
/// </summary>
/// <param name="Deleted">Whether the watchlist was deleted successfully.</param>
/// <param name="Id">The identifier of the deleted watchlist.</param>
[ExcludeFromCodeCoverage]
public record DeleteWatchlistResponse(
    [property: JsonPropertyName("deleted")] bool Deleted,
    [property: JsonPropertyName("id")] string Id)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
