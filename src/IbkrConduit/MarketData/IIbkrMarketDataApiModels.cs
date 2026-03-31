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
public record HistoricalBar(
    [property: JsonPropertyName("o")] decimal Open,
    [property: JsonPropertyName("c")] decimal Close,
    [property: JsonPropertyName("h")] decimal High,
    [property: JsonPropertyName("l")] decimal Low,
    [property: JsonPropertyName("v")] decimal Volume,
    [property: JsonPropertyName("t")] long Timestamp);
