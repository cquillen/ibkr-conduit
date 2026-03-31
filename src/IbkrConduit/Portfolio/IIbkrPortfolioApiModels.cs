using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrConduit.Portfolio;

/// <summary>
/// Represents an IBKR account from the /portfolio/accounts endpoint.
/// </summary>
/// <param name="Id">The account identifier (e.g., "U1234567").</param>
/// <param name="AccountTitle">The account title/description.</param>
/// <param name="Type">The account type (e.g., "INDIVIDUAL").</param>
public record Account(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("accountTitle")] string AccountTitle,
    [property: JsonPropertyName("type")] string Type);

/// <summary>
/// Represents a position in a portfolio account.
/// </summary>
/// <param name="AccountId">The account identifier.</param>
/// <param name="Conid">The contract identifier.</param>
/// <param name="ContractDescription">Human-readable contract description.</param>
/// <param name="Quantity">The position quantity.</param>
/// <param name="MarketPrice">Current market price.</param>
/// <param name="MarketValue">Current market value.</param>
/// <param name="AverageCost">Average cost basis.</param>
/// <param name="AveragePrice">Average price per share.</param>
/// <param name="RealizedPnl">Realized profit and loss.</param>
/// <param name="UnrealizedPnl">Unrealized profit and loss.</param>
/// <param name="Currency">The currency code.</param>
/// <param name="Name">The instrument name.</param>
/// <param name="AssetClass">The asset class (e.g., "STK", "OPT").</param>
/// <param name="Sector">The sector, if applicable.</param>
/// <param name="Ticker">The ticker symbol.</param>
/// <param name="Multiplier">The contract multiplier, if applicable.</param>
/// <param name="IsUs">Whether the instrument is US-listed.</param>
public record Position(
    [property: JsonPropertyName("acctId")] string AccountId,
    [property: JsonPropertyName("conid")] int Conid,
    [property: JsonPropertyName("contractDesc")] string ContractDescription,
    [property: JsonPropertyName("position")] decimal Quantity,
    [property: JsonPropertyName("mktPrice")] decimal MarketPrice,
    [property: JsonPropertyName("mktValue")] decimal MarketValue,
    [property: JsonPropertyName("avgCost")] decimal AverageCost,
    [property: JsonPropertyName("avgPrice")] decimal AveragePrice,
    [property: JsonPropertyName("realizedPnl")] decimal RealizedPnl,
    [property: JsonPropertyName("unrealizedPnl")] decimal UnrealizedPnl,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("assetClass")] string AssetClass,
    [property: JsonPropertyName("sector")] string? Sector,
    [property: JsonPropertyName("ticker")] string Ticker,
    [property: JsonPropertyName("multiplier")] decimal? Multiplier,
    [property: JsonPropertyName("isUS")] bool? IsUs)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Represents an entry in the account summary response.
/// Keys are dynamic field names like "netliquidationvalue", "totalcashvalue", etc.
/// </summary>
/// <param name="Amount">The numeric amount.</param>
/// <param name="Currency">The currency code.</param>
/// <param name="IsNull">Whether the value is null/unavailable.</param>
/// <param name="Timestamp">Unix timestamp of the value.</param>
/// <param name="Value">String representation of the value.</param>
public record AccountSummaryEntry(
    [property: JsonPropertyName("amount")] decimal? Amount,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("isNull")] bool IsNull,
    [property: JsonPropertyName("timestamp")] long? Timestamp,
    [property: JsonPropertyName("value")] string? Value)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Represents a ledger entry for a currency in the account.
/// Keys in the parent dictionary are currency codes like "USD", "EUR", "BASE".
/// </summary>
/// <param name="CashBalance">Cash balance in this currency.</param>
/// <param name="NetLiquidationValue">Net liquidation value in this currency.</param>
/// <param name="SettledCash">Settled cash amount.</param>
/// <param name="ExchangeRate">Exchange rate to base currency.</param>
/// <param name="StockMarketValue">Market value of stock positions.</param>
/// <param name="CorporateBondsMarketValue">Market value of corporate bonds.</param>
/// <param name="WarrantsMarketValue">Market value of warrants.</param>
/// <param name="FutureMarketValue">Market value of futures positions.</param>
/// <param name="CommodityMarketValue">Market value of commodity positions.</param>
public record LedgerEntry(
    [property: JsonPropertyName("cashbalance")] decimal CashBalance,
    [property: JsonPropertyName("netliquidationvalue")] decimal NetLiquidationValue,
    [property: JsonPropertyName("settledcash")] decimal SettledCash,
    [property: JsonPropertyName("exchangerate")] decimal ExchangeRate,
    [property: JsonPropertyName("stockmarketvalue")] decimal StockMarketValue,
    [property: JsonPropertyName("corporatebondsmarketvalue")] decimal CorporateBondsMarketValue,
    [property: JsonPropertyName("warrantsmarketvalue")] decimal WarrantsMarketValue,
    [property: JsonPropertyName("futuremarketvalue")] decimal FutureMarketValue,
    [property: JsonPropertyName("commoditymarketvalue")] decimal CommodityMarketValue)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Represents account metadata from the /portfolio/{accountId}/meta endpoint.
/// </summary>
/// <param name="Id">The account identifier.</param>
/// <param name="AccountId">The account ID string.</param>
/// <param name="AccountTitle">The account title.</param>
/// <param name="AccountAlias">The account alias, if set.</param>
/// <param name="Type">The account type.</param>
/// <param name="Currency">The base currency.</param>
public record AccountInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("accountId")] string AccountId,
    [property: JsonPropertyName("accountTitle")] string? AccountTitle,
    [property: JsonPropertyName("accountAlias")] string? AccountAlias,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("currency")] string? Currency)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Represents allocation data for an account from the /portfolio/{accountId}/allocation endpoint.
/// </summary>
/// <param name="AssetClass">Allocation breakdown by asset class.</param>
/// <param name="Sector">Allocation breakdown by sector.</param>
/// <param name="Group">Allocation breakdown by group.</param>
public record AccountAllocation(
    [property: JsonPropertyName("assetClass")] Dictionary<string, Dictionary<string, decimal>>? AssetClass,
    [property: JsonPropertyName("sector")] Dictionary<string, Dictionary<string, decimal>>? Sector,
    [property: JsonPropertyName("group")] Dictionary<string, Dictionary<string, decimal>>? Group)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Represents position and contract info from the /portfolio/positions/{conid} endpoint.
/// </summary>
/// <param name="Conid">The contract identifier.</param>
/// <param name="Ticker">The ticker symbol.</param>
/// <param name="Name">The instrument name.</param>
/// <param name="AssetClass">The asset class.</param>
/// <param name="ListingExchange">The listing exchange.</param>
/// <param name="Currency">The currency.</param>
public record PositionContractInfo(
    [property: JsonPropertyName("conid")] int? Conid,
    [property: JsonPropertyName("ticker")] string? Ticker,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("assetClass")] string? AssetClass,
    [property: JsonPropertyName("listingExchange")] string? ListingExchange,
    [property: JsonPropertyName("currency")] string? Currency)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Represents account performance data from the /pa/performance endpoint.
/// </summary>
/// <param name="CurrencyType">The currency type used for performance data.</param>
/// <param name="Rc">Return code.</param>
public record AccountPerformance(
    [property: JsonPropertyName("currencyType")] string? CurrencyType,
    [property: JsonPropertyName("rc")] int? Rc)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Represents transaction history from the /pa/transactions endpoint.
/// </summary>
/// <param name="Id">Transaction identifier.</param>
/// <param name="CurrencyType">The currency type.</param>
/// <param name="Rc">Return code.</param>
public record TransactionHistory(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("currencyType")] string? CurrencyType,
    [property: JsonPropertyName("rc")] int? Rc)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Request body for the /pa/performance endpoint.
/// </summary>
/// <param name="AccountIds">The account IDs to query performance for.</param>
/// <param name="Period">The time period (e.g., "1D", "1W", "1M", "1Y").</param>
public record PerformanceRequest(
    [property: JsonPropertyName("acctIds")] List<string> AccountIds,
    [property: JsonPropertyName("period")] string Period);

/// <summary>
/// Request body for the /pa/transactions endpoint.
/// </summary>
/// <param name="AccountIds">The account IDs to query transactions for.</param>
/// <param name="Conids">The contract IDs to filter by.</param>
/// <param name="Currency">The currency code.</param>
/// <param name="Days">Number of days of history to return.</param>
public record TransactionHistoryRequest(
    [property: JsonPropertyName("acctIds")] List<string> AccountIds,
    [property: JsonPropertyName("conids")] List<string> Conids,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("days")] int? Days);
