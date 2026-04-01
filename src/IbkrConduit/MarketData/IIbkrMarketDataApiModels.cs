using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrConduit.MarketData;

/// <summary>
/// Raw market data snapshot as returned by the IBKR API.
/// Field data is stored in the <see cref="Fields"/> dictionary using numeric string keys.
/// </summary>
/// <param name="Conid">The contract identifier.</param>
/// <param name="ConidExtended">Extended contract identifier.</param>
/// <param name="Updated">Unix timestamp (milliseconds) of the last update.</param>
/// <param name="ServerId">The server ID that provided the data.</param>
/// <param name="MarketDataAvailability">Market data availability status.</param>
[ExcludeFromCodeCoverage]
public record MarketDataSnapshotRaw(
    [property: JsonPropertyName("conid")] int Conid,
    [property: JsonPropertyName("conidEx")] string? ConidExtended,
    [property: JsonPropertyName("_updated")] long? Updated,
    [property: JsonPropertyName("server_id")] string? ServerId,
    [property: JsonPropertyName("6509")] string? MarketDataAvailability)
{
    /// <summary>
    /// All additional fields from the response, keyed by field ID string.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Fields { get; init; }
}

/// <summary>
/// Consumer-facing market data snapshot with typed properties for common fields
/// and a dictionary for all fields.
/// </summary>
[ExcludeFromCodeCoverage]
public record MarketDataSnapshot
{
    /// <summary>The contract identifier.</summary>
    public int Conid { get; init; }

    /// <summary>Unix timestamp (milliseconds) of the last update.</summary>
    public long? Updated { get; init; }

    /// <summary>Market data availability status.</summary>
    public string? MarketDataAvailability { get; init; }

    /// <summary>Last traded price (field 31).</summary>
    public string? LastPrice { get; init; }

    /// <summary>Best bid price (field 84).</summary>
    public string? BidPrice { get; init; }

    /// <summary>Best ask price (field 86).</summary>
    public string? AskPrice { get; init; }

    /// <summary>Ask size (field 85).</summary>
    public string? AskSize { get; init; }

    /// <summary>Bid size (field 88).</summary>
    public string? BidSize { get; init; }

    /// <summary>Last trade size (field 7059).</summary>
    public string? LastSize { get; init; }

    /// <summary>Session high (field 70).</summary>
    public string? High { get; init; }

    /// <summary>Session low (field 71).</summary>
    public string? Low { get; init; }

    /// <summary>Opening price (field 7295).</summary>
    public string? Open { get; init; }

    /// <summary>Closing price (field 7296).</summary>
    public string? Close { get; init; }

    /// <summary>Prior close price (field 7741).</summary>
    public string? PriorClose { get; init; }

    /// <summary>Volume (field 87).</summary>
    public string? Volume { get; init; }

    /// <summary>Volume as long (field 7762).</summary>
    public string? VolumeLong { get; init; }

    /// <summary>Change from prior close (field 82).</summary>
    public string? Change { get; init; }

    /// <summary>Change percent from prior close (field 83).</summary>
    public string? ChangePercent { get; init; }

    /// <summary>Market value of position (field 73).</summary>
    public string? MarketValue { get; init; }

    /// <summary>Average price (field 74).</summary>
    public string? AvgPrice { get; init; }

    /// <summary>Unrealized PnL (field 75).</summary>
    public string? UnrealizedPnl { get; init; }

    /// <summary>Realized PnL (field 79).</summary>
    public string? RealizedPnl { get; init; }

    /// <summary>Daily PnL (field 78).</summary>
    public string? DailyPnl { get; init; }

    /// <summary>Implied volatility (field 7633).</summary>
    public string? ImpliedVolatility { get; init; }

    /// <summary>
    /// All fields from the raw response keyed by field ID string.
    /// Use <see cref="MarketDataFields"/> constants for field lookup.
    /// </summary>
    public IReadOnlyDictionary<string, string>? AllFields { get; init; }
}

/// <summary>
/// Historical market data response containing OHLCV bars.
/// </summary>
/// <param name="Symbol">The symbol.</param>
/// <param name="Text">Description text.</param>
/// <param name="PriceFactor">Price factor for display.</param>
/// <param name="StartTime">Start time of the data range.</param>
/// <param name="HighStr">High value as string.</param>
/// <param name="LowStr">Low value as string.</param>
/// <param name="TimePeriod">The requested time period.</param>
/// <param name="BarLength">Bar length in seconds.</param>
/// <param name="MdAvailability">Market data availability.</param>
/// <param name="MktDataDelay">Market data delay in seconds.</param>
/// <param name="OutsideRth">Whether data includes outside regular trading hours.</param>
/// <param name="VolumeFactor">Volume factor for display.</param>
/// <param name="PriceDisplayRule">Price display rule identifier.</param>
/// <param name="PriceDisplayValue">Price display value.</param>
/// <param name="NegativeCapable">Whether negative values are possible.</param>
/// <param name="MessageVersion">Message version.</param>
/// <param name="Data">The OHLCV bar data.</param>
/// <param name="Points">Number of data points.</param>
/// <param name="TravelTime">Server processing time in milliseconds.</param>
[ExcludeFromCodeCoverage]
public record HistoricalDataResponse(
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("priceFactor")] int? PriceFactor,
    [property: JsonPropertyName("startTime")] string? StartTime,
    [property: JsonPropertyName("high")] string? HighStr,
    [property: JsonPropertyName("low")] string? LowStr,
    [property: JsonPropertyName("timePeriod")] string? TimePeriod,
    [property: JsonPropertyName("barLength")] int? BarLength,
    [property: JsonPropertyName("mdAvailability")] string? MdAvailability,
    [property: JsonPropertyName("mktDataDelay")] int? MktDataDelay,
    [property: JsonPropertyName("outsideRth")] bool? OutsideRth,
    [property: JsonPropertyName("volumeFactor")] int? VolumeFactor,
    [property: JsonPropertyName("priceDisplayRule")] int? PriceDisplayRule,
    [property: JsonPropertyName("priceDisplayValue")] string? PriceDisplayValue,
    [property: JsonPropertyName("negativeCapable")] bool? NegativeCapable,
    [property: JsonPropertyName("messageVersion")] int? MessageVersion,
    [property: JsonPropertyName("data")] List<HistoricalBar>? Data,
    [property: JsonPropertyName("points")] int? Points,
    [property: JsonPropertyName("travelTime")] int? TravelTime);

/// <summary>
/// A single OHLCV bar in historical data.
/// </summary>
/// <param name="Open">Opening price.</param>
/// <param name="Close">Closing price.</param>
/// <param name="High">High price.</param>
/// <param name="Low">Low price.</param>
/// <param name="Volume">Volume traded.</param>
/// <param name="Timestamp">Unix timestamp (milliseconds).</param>
[ExcludeFromCodeCoverage]
public record HistoricalBar(
    [property: JsonPropertyName("o")] decimal Open,
    [property: JsonPropertyName("c")] decimal Close,
    [property: JsonPropertyName("h")] decimal High,
    [property: JsonPropertyName("l")] decimal Low,
    [property: JsonPropertyName("v")] decimal Volume,
    [property: JsonPropertyName("t")] long Timestamp);

/// <summary>
/// Request body for unsubscribing from a single contract's market data.
/// </summary>
/// <param name="Conid">The contract identifier to unsubscribe.</param>
[ExcludeFromCodeCoverage]
public record UnsubscribeRequest(
    [property: JsonPropertyName("conid")] int Conid);

/// <summary>
/// Response from the unsubscribe endpoint.
/// </summary>
/// <param name="Success">Whether the unsubscribe request was successful.</param>
[ExcludeFromCodeCoverage]
public record UnsubscribeResponse(
    [property: JsonPropertyName("success")] bool Success);

/// <summary>
/// Response from the unsubscribe-all endpoint.
/// </summary>
/// <param name="Unsubscribed">Whether all subscriptions were cancelled.</param>
[ExcludeFromCodeCoverage]
public record UnsubscribeAllResponse(
    [property: JsonPropertyName("unsubscribed")] bool Unsubscribed);

/// <summary>
/// Request body for the iserver market scanner.
/// </summary>
/// <param name="Instrument">Instrument type (e.g., "STK", "ETF").</param>
/// <param name="Type">Scanner type (e.g., "TOP_TRADE_COUNT", "TOP_PERC_GAIN").</param>
/// <param name="Location">Location filter (e.g., "STK.US.MAJOR").</param>
/// <param name="Filter">Optional array of filter criteria.</param>
[ExcludeFromCodeCoverage]
public record ScannerRequest(
    [property: JsonPropertyName("instrument")] string Instrument,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("location")] string Location,
    [property: JsonPropertyName("filter")] List<ScannerFilter>? Filter);

/// <summary>
/// A single filter criterion for a scanner request.
/// </summary>
/// <param name="Code">The filter code (e.g., "priceAbove").</param>
/// <param name="Value">The filter value.</param>
[ExcludeFromCodeCoverage]
public record ScannerFilter(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("value")] int Value);

/// <summary>
/// Response from the iserver market scanner endpoint.
/// </summary>
/// <param name="Contracts">The list of matched contracts.</param>
/// <param name="ScanDataColumnName">Column name for the scan data.</param>
[ExcludeFromCodeCoverage]
public record ScannerResponse(
    [property: JsonPropertyName("contracts")] List<ScannerContract>? Contracts,
    [property: JsonPropertyName("scan_data_column_name")] string? ScanDataColumnName);

/// <summary>
/// A single contract from a scanner result.
/// </summary>
/// <param name="ServerId">The contract's index in the scanner sort order.</param>
/// <param name="Symbol">The contract's ticker symbol.</param>
/// <param name="Conidex">The contract ID as a string.</param>
/// <param name="ConId">The contract ID as an integer.</param>
/// <param name="CompanyName">The company long name.</param>
/// <param name="ContractDescription">The contract description or local symbol.</param>
/// <param name="ListingExchange">The primary listing exchange.</param>
/// <param name="SecType">The security type.</param>
/// <param name="ScanData">The scan data value for this contract.</param>
[ExcludeFromCodeCoverage]
public record ScannerContract(
    [property: JsonPropertyName("server_id")] string? ServerId,
    [property: JsonPropertyName("symbol")] string? Symbol,
    [property: JsonPropertyName("conidex")] string? Conidex,
    [property: JsonPropertyName("con_id")] int? ConId,
    [property: JsonPropertyName("company_name")] string? CompanyName,
    [property: JsonPropertyName("contract_description_1")] string? ContractDescription,
    [property: JsonPropertyName("listing_exchange")] string? ListingExchange,
    [property: JsonPropertyName("sec_type")] string? SecType,
    [property: JsonPropertyName("scan_data")] string? ScanData)
{
    /// <summary>
    /// Additional fields not explicitly mapped.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Request body for the HMDS market scanner.
/// </summary>
/// <param name="Instrument">Instrument type (e.g., "BOND", "STK").</param>
/// <param name="Locations">Location filter (e.g., "BOND.US").</param>
/// <param name="ScanCode">Scanner type code.</param>
/// <param name="SecType">Security type.</param>
/// <param name="MaxItems">Maximum number of items to return (default 250).</param>
/// <param name="Filters">Array of filter objects.</param>
[ExcludeFromCodeCoverage]
public record HmdsScannerRequest(
    [property: JsonPropertyName("instrument")] string Instrument,
    [property: JsonPropertyName("locations")] string Locations,
    [property: JsonPropertyName("scanCode")] string ScanCode,
    [property: JsonPropertyName("secType")] string SecType,
    [property: JsonPropertyName("maxItems")] int? MaxItems,
    [property: JsonPropertyName("filters")] List<Dictionary<string, object>>? Filters);

/// <summary>
/// Response from the HMDS market scanner endpoint.
/// </summary>
/// <param name="Total">Total number of matching contracts.</param>
/// <param name="Size">Number of contracts returned.</param>
/// <param name="Offset">Offset in the result set.</param>
/// <param name="ScanTime">Time the scan was performed.</param>
/// <param name="Id">Scanner identifier.</param>
/// <param name="Contracts">Wrapper containing the contract array.</param>
[ExcludeFromCodeCoverage]
public record HmdsScannerResponse(
    [property: JsonPropertyName("total")] string? Total,
    [property: JsonPropertyName("size")] string? Size,
    [property: JsonPropertyName("offset")] string? Offset,
    [property: JsonPropertyName("scanTime")] string? ScanTime,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("Contracts")] HmdsScannerContractWrapper? Contracts)
{
    /// <summary>
    /// Additional fields not explicitly mapped.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Wrapper for the HMDS scanner contract array.
/// </summary>
/// <param name="Contract">The list of scanned contracts.</param>
[ExcludeFromCodeCoverage]
public record HmdsScannerContractWrapper(
    [property: JsonPropertyName("Contract")] List<HmdsScannerContract>? Contract);

/// <summary>
/// A single contract from the HMDS scanner result.
/// </summary>
/// <param name="InScanTime">The time the contract was scanned (UTC).</param>
/// <param name="ContractId">The contract identifier.</param>
[ExcludeFromCodeCoverage]
public record HmdsScannerContract(
    [property: JsonPropertyName("inScanTime")] string? InScanTime,
    [property: JsonPropertyName("contractID")] string? ContractId)
{
    /// <summary>
    /// Additional fields not explicitly mapped.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Scanner parameters describing available scanner types, instruments, locations, and filters.
/// </summary>
/// <param name="ScanTypeList">Available scanner types.</param>
/// <param name="InstrumentList">Available instrument types.</param>
/// <param name="FilterList">Available filters.</param>
/// <param name="LocationTree">Available location hierarchy.</param>
[ExcludeFromCodeCoverage]
public record ScannerParameters(
    [property: JsonPropertyName("scan_type_list")] List<ScannerType>? ScanTypeList,
    [property: JsonPropertyName("instrument_list")] List<ScannerInstrument>? InstrumentList,
    [property: JsonPropertyName("filter_list")] List<ScannerFilterDefinition>? FilterList,
    [property: JsonPropertyName("location_tree")] List<ScannerLocation>? LocationTree)
{
    /// <summary>
    /// Additional fields not explicitly mapped.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// A scanner type definition.
/// </summary>
/// <param name="DisplayName">Human-readable name.</param>
/// <param name="Code">Code value for scanner requests.</param>
/// <param name="Instruments">Instruments this scanner type supports.</param>
[ExcludeFromCodeCoverage]
public record ScannerType(
    [property: JsonPropertyName("display_name")] string? DisplayName,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("instruments")] List<string>? Instruments);

/// <summary>
/// A scanner instrument definition.
/// </summary>
/// <param name="DisplayName">Human-readable name.</param>
/// <param name="Type">Code value for scanner requests.</param>
/// <param name="Filters">Available filters for this instrument.</param>
[ExcludeFromCodeCoverage]
public record ScannerInstrument(
    [property: JsonPropertyName("display_name")] string? DisplayName,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("filters")] List<string>? Filters);

/// <summary>
/// A scanner filter definition.
/// </summary>
/// <param name="Group">Filter group.</param>
/// <param name="DisplayName">Human-readable name.</param>
/// <param name="Code">Code value for scanner requests.</param>
/// <param name="Type">Value type (e.g., range or single).</param>
[ExcludeFromCodeCoverage]
public record ScannerFilterDefinition(
    [property: JsonPropertyName("group")] string? Group,
    [property: JsonPropertyName("display_name")] string? DisplayName,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("type")] string? Type);

/// <summary>
/// A scanner location in the location tree.
/// </summary>
/// <param name="DisplayName">Human-readable name.</param>
/// <param name="Type">Code value for scanner requests.</param>
/// <param name="Locations">Child locations.</param>
[ExcludeFromCodeCoverage]
public record ScannerLocation(
    [property: JsonPropertyName("display_name")] string? DisplayName,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("locations")] List<ScannerLocation>? Locations);
