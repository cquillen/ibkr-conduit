using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrConduit.Contracts;

/// <summary>
/// A contract search result from the /iserver/secdef/search endpoint.
/// </summary>
/// <param name="Conid">The IBKR contract identifier.</param>
/// <param name="CompanyHeader">The company header text.</param>
/// <param name="CompanyName">The company name.</param>
/// <param name="Description">A description of the contract.</param>
/// <param name="Symbol">The ticker symbol.</param>
/// <param name="ExtendedConid">The extended contract identifier.</param>
/// <param name="SecurityType">The security type (e.g., "STK", "OPT").</param>
/// <param name="ListingExchange">The primary listing exchange.</param>
/// <param name="Sections">Optional list of contract sections (e.g., for derivatives).</param>
[ExcludeFromCodeCoverage]
public record ContractSearchResult(
    [property: JsonPropertyName("conid")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    int Conid,
    [property: JsonPropertyName("companyHeader")] string CompanyHeader,
    [property: JsonPropertyName("companyName")] string CompanyName,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("conidEx")] string ExtendedConid,
    [property: JsonPropertyName("secType")] string SecurityType,
    [property: JsonPropertyName("listingExchange")] string ListingExchange,
    [property: JsonPropertyName("sections")] List<ContractSection>? Sections);

/// <summary>
/// A section within a contract search result, representing a derivative type or sub-instrument.
/// </summary>
/// <param name="SecurityType">The security type of this section.</param>
/// <param name="Months">Available contract months, if applicable.</param>
/// <param name="Symbol">The symbol for this section, if different from the parent.</param>
/// <param name="Exchange">The exchange for this section.</param>
/// <param name="Conid">The contract ID for this section, if applicable.</param>
[ExcludeFromCodeCoverage]
public record ContractSection(
    [property: JsonPropertyName("secType")] string SecurityType,
    [property: JsonPropertyName("months")] string? Months,
    [property: JsonPropertyName("symbol")] string? Symbol,
    [property: JsonPropertyName("exchange")] string? Exchange,
    [property: JsonPropertyName("conid")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    int? Conid);

/// <summary>
/// Detailed contract information from the /iserver/contract/{conid}/info endpoint.
/// </summary>
/// <param name="Conid">The IBKR contract identifier.</param>
/// <param name="Symbol">The ticker symbol.</param>
/// <param name="CompanyName">The company name.</param>
/// <param name="Exchange">The primary exchange.</param>
/// <param name="ListingExchange">The listing exchange.</param>
/// <param name="Currency">The trading currency.</param>
/// <param name="InstrumentType">The instrument type (e.g., "STK").</param>
/// <param name="ValidExchanges">Comma-separated list of valid exchanges.</param>
[ExcludeFromCodeCoverage]
public record ContractDetails(
    [property: JsonPropertyName("con_id")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    int Conid,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("company_name")] string CompanyName,
    [property: JsonPropertyName("exchange")] string Exchange,
    [property: JsonPropertyName("listing_exchange")] string ListingExchange,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("instrument_type")] string InstrumentType,
    [property: JsonPropertyName("valid_exchanges")] string ValidExchanges);

/// <summary>
/// Security definition info from the /iserver/secdef/info endpoint (derivatives).
/// </summary>
/// <param name="Conid">The IBKR contract identifier.</param>
/// <param name="Symbol">The ticker symbol.</param>
/// <param name="SecurityType">The security type (e.g., "OPT", "FUT").</param>
/// <param name="Exchange">The exchange.</param>
/// <param name="ListingExchange">The listing exchange.</param>
/// <param name="Right">The option right ("C" or "P"), if applicable.</param>
/// <param name="Strike">The strike price as a string, if applicable.</param>
/// <param name="MaturityDate">The maturity date (YYYYMMDD), if applicable.</param>
[ExcludeFromCodeCoverage]
public record SecurityDefinitionInfo(
    [property: JsonPropertyName("conid")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    int Conid,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("secType")] string SecurityType,
    [property: JsonPropertyName("exchange")] string Exchange,
    [property: JsonPropertyName("listingExchange")] string ListingExchange,
    [property: JsonPropertyName("right")] string? Right,
    [property: JsonPropertyName("strike")] string? Strike,
    [property: JsonPropertyName("maturityDate")] string? MaturityDate)
{
    /// <summary>
    /// Captures any additional JSON properties not mapped to named parameters.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
/// Option strike prices from the /iserver/secdef/strikes endpoint.
/// </summary>
/// <param name="Call">Array of call strike prices.</param>
/// <param name="Put">Array of put strike prices.</param>
[ExcludeFromCodeCoverage]
public record OptionStrikes(
    [property: JsonPropertyName("call")] List<decimal> Call,
    [property: JsonPropertyName("put")] List<decimal> Put)
{
    /// <summary>
    /// Captures any additional JSON properties not mapped to named parameters.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
/// Request body for the POST /iserver/contract/rules endpoint.
/// </summary>
/// <param name="Conid">The contract identifier.</param>
/// <param name="Exchange">The exchange (optional).</param>
/// <param name="IsBuy">Whether this is a buy order.</param>
/// <param name="ModifyOrder">Whether this is a modify order request.</param>
/// <param name="OrderId">The order ID if modifying an existing order.</param>
[ExcludeFromCodeCoverage]
public record TradingRulesRequest(
    [property: JsonPropertyName("conid")] int Conid,
    [property: JsonPropertyName("exchange")] string? Exchange,
    [property: JsonPropertyName("isBuy")] bool? IsBuy,
    [property: JsonPropertyName("modifyOrder")] bool? ModifyOrder,
    [property: JsonPropertyName("orderId")] int? OrderId);

/// <summary>
/// Trading rules from the POST /iserver/contract/rules endpoint.
/// </summary>
/// <param name="DefaultSize">The default order size.</param>
/// <param name="SizeIncrement">The size increment.</param>
/// <param name="CashSize">The cash size.</param>
/// <param name="CashCurrency">The cash currency.</param>
[ExcludeFromCodeCoverage]
public record TradingRules(
    [property: JsonPropertyName("defaultSize")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    decimal DefaultSize,
    [property: JsonPropertyName("sizeIncrement")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    decimal SizeIncrement,
    [property: JsonPropertyName("cashSize")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    decimal CashSize,
    [property: JsonPropertyName("cashCurrency")] string? CashCurrency)
{
    /// <summary>
    /// Captures any additional JSON properties not mapped to named parameters.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
/// Response from the GET /trsrv/secdef endpoint containing security definitions.
/// </summary>
/// <param name="Secdef">The list of security definitions.</param>
[ExcludeFromCodeCoverage]
public record SecurityDefinitionResponse(
    [property: JsonPropertyName("secdef")] List<SecurityDefinition> Secdef)
{
    /// <summary>
    /// Captures any additional JSON properties not mapped to named parameters.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
/// A security definition from the /trsrv/secdef endpoint.
/// </summary>
/// <param name="Conid">The IBKR contract identifier.</param>
/// <param name="Currency">The trading currency.</param>
/// <param name="Name">The instrument name.</param>
/// <param name="AssetClass">The asset class (e.g., "STK", "OPT").</param>
/// <param name="Expiry">The expiration date, if applicable.</param>
/// <param name="LastTradingDay">The last trading day, if applicable.</param>
/// <param name="Group">The instrument group.</param>
/// <param name="PutOrCall">Put or call indicator, if applicable.</param>
/// <param name="Sector">The sector.</param>
/// <param name="SectorGroup">The sector group.</param>
/// <param name="Strike">The strike price.</param>
/// <param name="Ticker">The ticker symbol.</param>
/// <param name="UndConid">The underlying contract ID.</param>
[ExcludeFromCodeCoverage]
public record SecurityDefinition(
    [property: JsonPropertyName("conid")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    int Conid,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("assetClass")] string? AssetClass,
    [property: JsonPropertyName("expiry")] string? Expiry,
    [property: JsonPropertyName("lastTradingDay")] string? LastTradingDay,
    [property: JsonPropertyName("group")] string? Group,
    [property: JsonPropertyName("putOrCall")] string? PutOrCall,
    [property: JsonPropertyName("sector")] string? Sector,
    [property: JsonPropertyName("sectorGroup")] string? SectorGroup,
    [property: JsonPropertyName("strike")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    decimal Strike,
    [property: JsonPropertyName("ticker")] string? Ticker,
    [property: JsonPropertyName("undConid")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    int UndConid)
{
    /// <summary>
    /// Captures any additional JSON properties not mapped to named parameters.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
/// A contract identifier with exchange from the /trsrv/all-conids endpoint.
/// </summary>
/// <param name="Ticker">The ticker symbol.</param>
/// <param name="Conid">The IBKR contract identifier.</param>
/// <param name="Exchange">The exchange.</param>
[ExcludeFromCodeCoverage]
public record ExchangeConid(
    [property: JsonPropertyName("ticker")] string Ticker,
    [property: JsonPropertyName("conid")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    int Conid,
    [property: JsonPropertyName("exchange")] string Exchange)
{
    /// <summary>
    /// Captures any additional JSON properties not mapped to named parameters.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
/// A futures contract from the /trsrv/futures endpoint.
/// </summary>
/// <param name="Symbol">The futures symbol.</param>
/// <param name="Conid">The IBKR contract identifier.</param>
/// <param name="UnderlyingConid">The underlying contract identifier.</param>
/// <param name="ExpirationDate">The expiration date as YYYYMMDD numeric value (e.g., 20261218). IBKR returns this as a JSON number.</param>
/// <param name="LastTradingDay">The last trading day (YYYYMMDD).</param>
[ExcludeFromCodeCoverage]
public record FutureContract(
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("conid")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    int Conid,
    [property: JsonPropertyName("underlyingConid")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    int UnderlyingConid,
    [property: JsonPropertyName("expirationDate")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    long? ExpirationDate,
    [property: JsonPropertyName("ltd")] string? LastTradingDay)
{
    /// <summary>
    /// Captures any additional JSON properties not mapped to named parameters.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
/// A stock contract from the /trsrv/stocks endpoint.
/// </summary>
/// <param name="Name">The company name.</param>
/// <param name="ChineseName">The Chinese company name, if available.</param>
/// <param name="AssetClass">The asset class.</param>
/// <param name="Contracts">The list of contract details per exchange.</param>
[ExcludeFromCodeCoverage]
public record StockContract(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("chineseName")] string? ChineseName,
    [property: JsonPropertyName("assetClass")] string AssetClass,
    [property: JsonPropertyName("contracts")] List<StockContractDetail> Contracts)
{
    /// <summary>
    /// Captures any additional JSON properties not mapped to named parameters.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
/// A stock contract detail (per-exchange listing) from the /trsrv/stocks endpoint.
/// </summary>
/// <param name="Conid">The IBKR contract identifier.</param>
/// <param name="Exchange">The exchange.</param>
/// <param name="IsUs">Whether this is a US-listed contract.</param>
[ExcludeFromCodeCoverage]
public record StockContractDetail(
    [property: JsonPropertyName("conid")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    int Conid,
    [property: JsonPropertyName("exchange")] string Exchange,
    [property: JsonPropertyName("isUS")] bool IsUs)
{
    /// <summary>
    /// Captures any additional JSON properties not mapped to named parameters.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
/// A trading schedule from the /trsrv/secdef/schedule endpoint.
/// </summary>
/// <param name="Id">The schedule identifier.</param>
/// <param name="TradeTimings">The list of trading time windows.</param>
[ExcludeFromCodeCoverage]
public record TradingSchedule(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("tradeTimings")] List<TradeTiming> TradeTimings)
{
    /// <summary>
    /// Captures any additional JSON properties not mapped to named parameters.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
/// A trading time window within a trading schedule.
/// </summary>
/// <param name="OpeningTime">The opening time as a Unix timestamp in milliseconds.</param>
/// <param name="ClosingTime">The closing time as a Unix timestamp in milliseconds.</param>
/// <param name="CancelDayOrders">Whether day orders are cancelled ("Y" or "N").</param>
[ExcludeFromCodeCoverage]
public record TradeTiming(
    [property: JsonPropertyName("openingTime")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    long OpeningTime,
    [property: JsonPropertyName("closingTime")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    long ClosingTime,
    [property: JsonPropertyName("cancelDayOrders")] string? CancelDayOrders)
{
    /// <summary>
    /// Captures any additional JSON properties not mapped to named parameters.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
/// A currency pair from the /iserver/currency/pairs endpoint.
/// </summary>
/// <param name="Symbol">The currency pair symbol (e.g., "EUR.USD").</param>
/// <param name="Conid">The IBKR contract identifier.</param>
/// <param name="SecurityType">The security type (e.g., "CASH").</param>
/// <param name="Exchange">The exchange.</param>
[ExcludeFromCodeCoverage]
public record CurrencyPair(
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("conid")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    int Conid,
    [property: JsonPropertyName("secType")] string SecurityType,
    [property: JsonPropertyName("exchange")] string Exchange)
{
    /// <summary>
    /// Captures any additional JSON properties not mapped to named parameters.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
/// Exchange rate response from the /iserver/exchangerate endpoint.
/// </summary>
/// <param name="Rate">The exchange rate value.</param>
[ExcludeFromCodeCoverage]
public record ExchangeRateResponse(
    [property: JsonPropertyName("rate")] decimal Rate)
{
    /// <summary>
    /// Captures any additional JSON properties not mapped to named parameters.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
