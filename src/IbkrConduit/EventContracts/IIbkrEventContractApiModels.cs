using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrConduit.EventContracts;

/// <summary>
/// Response from GET /forecast/category/tree containing the full category hierarchy.
/// </summary>
/// <param name="Categories">Dictionary of category ID to category details.</param>
[ExcludeFromCodeCoverage]
public record EventContractCategoryTreeResponse(
    [property: JsonPropertyName("categories")] Dictionary<string, EventContractCategory> Categories)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// A category in the event contract category tree.
/// </summary>
/// <param name="Name">The category display name.</param>
/// <param name="ParentId">The parent category identifier.</param>
/// <param name="Markets">Optional list of markets within this category.</param>
[ExcludeFromCodeCoverage]
public record EventContractCategory(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("parent_id")] string ParentId,
    [property: JsonPropertyName("markets")] List<EventContractCategoryMarket>? Markets = null)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// A market entry within a category in the category tree.
/// </summary>
/// <param name="Name">The market display name.</param>
/// <param name="Symbol">The market symbol.</param>
/// <param name="Exchange">The exchange (e.g., "FORECASTX").</param>
/// <param name="Conid">The contract identifier for the market.</param>
/// <param name="ProductConid">The product contract identifier.</param>
[ExcludeFromCodeCoverage]
public record EventContractCategoryMarket(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("exchange")] string Exchange,
    [property: JsonPropertyName("conid")] int Conid,
    [property: JsonPropertyName("product_conid")] int ProductConid)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Response from GET /forecast/contract/market containing contracts for a market.
/// </summary>
/// <param name="MarketName">The market display name.</param>
/// <param name="Exchange">The exchange (e.g., "FORECASTX").</param>
/// <param name="Symbol">The market symbol.</param>
/// <param name="LogoCategory">The logo category identifier.</param>
/// <param name="ExcludeHistoricalData">Whether historical data is excluded.</param>
/// <param name="Payout">The payout amount per contract.</param>
/// <param name="Contracts">The list of contracts in this market.</param>
[ExcludeFromCodeCoverage]
public record EventContractMarketResponse(
    [property: JsonPropertyName("market_name")] string MarketName,
    [property: JsonPropertyName("exchange")] string Exchange,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("logo_category")] string LogoCategory,
    [property: JsonPropertyName("exclude_historical_data")] bool ExcludeHistoricalData,
    [property: JsonPropertyName("payout")] double Payout,
    [property: JsonPropertyName("contracts")] List<EventContract> Contracts)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// An individual event contract within a market.
/// </summary>
/// <param name="Conid">The contract identifier.</param>
/// <param name="Side">The contract side ("Y" for Yes, "N" for No).</param>
/// <param name="Expiration">The expiration date (e.g., "20270127").</param>
/// <param name="Strike">The strike price.</param>
/// <param name="StrikeLabel">Human-readable strike label (e.g., "Above 3.125%").</param>
/// <param name="ExpiryLabel">Human-readable expiry label (e.g., "January 27, 2027").</param>
/// <param name="UnderlyingConid">The underlying contract identifier.</param>
/// <param name="TimeSpecifier">The time specifier (e.g., "2027.1.28").</param>
[ExcludeFromCodeCoverage]
public record EventContract(
    [property: JsonPropertyName("conid")] int Conid,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("expiration")] string Expiration,
    [property: JsonPropertyName("strike")] double Strike,
    [property: JsonPropertyName("strike_label")] string StrikeLabel,
    [property: JsonPropertyName("expiry_label")] string ExpiryLabel,
    [property: JsonPropertyName("underlying_conid")] int UnderlyingConid,
    [property: JsonPropertyName("time_specifier")] string TimeSpecifier)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Response from GET /forecast/contract/rules containing market rules for a contract.
/// </summary>
/// <param name="AssetClass">The asset class (e.g., "OPT").</param>
/// <param name="Description">Description of the market and its rules.</param>
/// <param name="MarketName">The market display name.</param>
/// <param name="MeasuredPeriod">The measurement period label.</param>
/// <param name="Threshold">The threshold value as a string.</param>
/// <param name="SourceAgency">The data source agency.</param>
/// <param name="DataAndResolutionLink">URL to data and resolution information.</param>
/// <param name="LastTradeTime">Unix timestamp for last trade time.</param>
/// <param name="ProductCode">The product code symbol.</param>
/// <param name="MarketRulesLink">URL to market rules PDF.</param>
/// <param name="ReleaseTime">Unix timestamp for data release time.</param>
/// <param name="PayoutTime">Unix timestamp for payout time.</param>
/// <param name="Payout">The payout amount as a string (e.g., "$1.00").</param>
/// <param name="PriceIncrement">The minimum price increment (e.g., "$0.01").</param>
/// <param name="ExchangeTimezone">The exchange timezone (e.g., "US/Central").</param>
[ExcludeFromCodeCoverage]
public record EventContractRulesResponse(
    [property: JsonPropertyName("asset_class")] string AssetClass,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("market_name")] string MarketName,
    [property: JsonPropertyName("measured_period")] string MeasuredPeriod,
    [property: JsonPropertyName("threshold")] string Threshold,
    [property: JsonPropertyName("source_agency")] string SourceAgency,
    [property: JsonPropertyName("data_and_resolution_link")] string DataAndResolutionLink,
    [property: JsonPropertyName("last_trade_time")] long LastTradeTime,
    [property: JsonPropertyName("product_code")] string ProductCode,
    [property: JsonPropertyName("market_rules_link")] string MarketRulesLink,
    [property: JsonPropertyName("release_time")] long ReleaseTime,
    [property: JsonPropertyName("payout_time")] long PayoutTime,
    [property: JsonPropertyName("payout")] string Payout,
    [property: JsonPropertyName("price_increment")] string PriceIncrement,
    [property: JsonPropertyName("exchange_timezone")] string ExchangeTimezone)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Response from GET /forecast/contract/details containing details for a specific contract.
/// </summary>
/// <param name="ConidYes">The conid for the Yes side.</param>
/// <param name="ConidNo">The conid for the No side.</param>
/// <param name="Question">The contract question text.</param>
/// <param name="Side">The contract side ("Y" or "N").</param>
/// <param name="StrikeLabel">Human-readable strike label.</param>
/// <param name="Strike">The strike price.</param>
/// <param name="Exchange">The exchange (e.g., "FORECASTX").</param>
/// <param name="Expiration">The expiration date (e.g., "20270127").</param>
/// <param name="Symbol">The market symbol.</param>
/// <param name="Category">The category identifier.</param>
/// <param name="LogoCategory">The logo category identifier.</param>
/// <param name="MeasuredPeriod">The measurement period label.</param>
/// <param name="MarketName">The market display name.</param>
/// <param name="UnderlyingConid">The underlying contract identifier.</param>
/// <param name="PayoutAmount">The payout amount per contract.</param>
/// <param name="ProductConid">The product contract identifier.</param>
/// <param name="IsRestricted">Whether the contract is restricted.</param>
[ExcludeFromCodeCoverage]
public record EventContractDetailsResponse(
    [property: JsonPropertyName("conid_yes")] int ConidYes,
    [property: JsonPropertyName("conid_no")] int ConidNo,
    [property: JsonPropertyName("question")] string Question,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("strike_label")] string StrikeLabel,
    [property: JsonPropertyName("strike")] double Strike,
    [property: JsonPropertyName("exchange")] string Exchange,
    [property: JsonPropertyName("expiration")] string Expiration,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("logo_category")] string LogoCategory,
    [property: JsonPropertyName("measured_period")] string MeasuredPeriod,
    [property: JsonPropertyName("market_name")] string MarketName,
    [property: JsonPropertyName("underlying_conid")] int UnderlyingConid,
    [property: JsonPropertyName("payout")] double PayoutAmount,
    [property: JsonPropertyName("product_conid")] int ProductConid,
    [property: JsonPropertyName("is_restricted")] bool IsRestricted)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Response from GET /forecast/contract/schedules containing trading schedules.
/// </summary>
/// <param name="Timezone">The exchange timezone (e.g., "US/Central").</param>
/// <param name="TradingSchedules">The list of daily trading schedules.</param>
[ExcludeFromCodeCoverage]
public record EventContractSchedulesResponse(
    [property: JsonPropertyName("timezone")] string Timezone,
    [property: JsonPropertyName("trading_schedules")] List<EventContractDaySchedule> TradingSchedules)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// A single day's trading schedule.
/// </summary>
/// <param name="DayOfWeek">The day of the week (e.g., "Monday").</param>
/// <param name="TradingTimes">The trading time windows for this day.</param>
[ExcludeFromCodeCoverage]
public record EventContractDaySchedule(
    [property: JsonPropertyName("day_of_week")] string DayOfWeek,
    [property: JsonPropertyName("trading_times")] List<EventContractTradingTime> TradingTimes)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// A trading time window within a daily schedule.
/// </summary>
/// <param name="Open">The opening time (e.g., "12:00 AM").</param>
/// <param name="Close">The closing time (e.g., "4:15 PM").</param>
[ExcludeFromCodeCoverage]
public record EventContractTradingTime(
    [property: JsonPropertyName("open")] string Open,
    [property: JsonPropertyName("close")] string Close)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
