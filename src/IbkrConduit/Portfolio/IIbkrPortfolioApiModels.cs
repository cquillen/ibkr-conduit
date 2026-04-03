using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrConduit.Portfolio;

/// <summary>
/// Represents an IBKR account from the /portfolio/accounts endpoint.
/// Generated from recorded API response — 24 fields matching actual wire format.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record Account
{
    /// <summary>The account identifier (e.g., "U1234567").</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>The account identifier.</summary>
    [JsonPropertyName("accountId")]
    public string AccountId { get; init; } = string.Empty;

    /// <summary>The account alias.</summary>
    [JsonPropertyName("accountVan")]
    public string AccountVan { get; init; } = string.Empty;

    /// <summary>Title of the account.</summary>
    [JsonPropertyName("accountTitle")]
    public string AccountTitle { get; init; } = string.Empty;

    /// <summary>Display name for the account holder.</summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>User customizable account alias.</summary>
    [JsonPropertyName("accountAlias")]
    public string? AccountAlias { get; init; }

    /// <summary>When the account was opened in unix time.</summary>
    [JsonPropertyName("accountStatus")]
    public long AccountStatus { get; init; }

    /// <summary>Base currency of the account.</summary>
    [JsonPropertyName("currency")]
    public string Currency { get; init; } = string.Empty;

    /// <summary>Account type (e.g., "INDIVIDUAL", "DEMO").</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>Account trading structure.</summary>
    [JsonPropertyName("tradingType")]
    public string TradingType { get; init; } = string.Empty;

    /// <summary>Returns the organizational structure of the account.</summary>
    [JsonPropertyName("businessType")]
    public string BusinessType { get; init; } = string.Empty;

    /// <summary>Returns the entity of Interactive Brokers the account is tied to.</summary>
    [JsonPropertyName("ibEntity")]
    public string IbEntity { get; init; } = string.Empty;

    /// <summary>If an account is a sub-account to a Financial Advisor.</summary>
    [JsonPropertyName("faclient")]
    public bool Faclient { get; init; }

    /// <summary>Status of the account. O: Open; P or N: Pending; A: Abandoned; R: Rejected; C: Closed.</summary>
    [JsonPropertyName("clearingStatus")]
    public string ClearingStatus { get; init; } = string.Empty;

    /// <summary>Is a Covestor Account.</summary>
    [JsonPropertyName("covestor")]
    public bool Covestor { get; init; }

    /// <summary>Returns if the client account may trade.</summary>
    [JsonPropertyName("noClientTrading")]
    public bool NoClientTrading { get; init; }

    /// <summary>Returns if the account is tracking Virtual FX or not.</summary>
    [JsonPropertyName("trackVirtualFXPortfolio")]
    public bool TrackVirtualFXPortfolio { get; init; }

    /// <summary>Whether the account has brokerage access.</summary>
    [JsonPropertyName("brokerageAccess")]
    public bool BrokerageAccess { get; init; }

    /// <summary>Prepaid crypto flag.</summary>
    [JsonPropertyName("PrepaidCrypto-Z")]
    public bool PrepaidCryptoZ { get; init; }

    /// <summary>Prepaid crypto flag.</summary>
    [JsonPropertyName("PrepaidCrypto-P")]
    public bool PrepaidCryptoP { get; init; }

    /// <summary>Account customer type (e.g., "INDIVIDUAL").</summary>
    [JsonPropertyName("acctCustType")]
    public string AcctCustType { get; init; } = string.Empty;

    /// <summary>Category of the account.</summary>
    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;

    /// <summary>Parent account reference for advisor/multiplex structures.</summary>
    [JsonPropertyName("parent")]
    public AccountParent? Parent { get; init; }

    /// <summary>Returns an account description.</summary>
    [JsonPropertyName("desc")]
    public string Desc { get; init; } = string.Empty;

    /// <summary>Additional unmapped properties from the API response.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Parent account reference within an Account object.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record AccountParent
{
    /// <summary>Money Manager Client accounts.</summary>
    [JsonPropertyName("mmc")]
    public List<object>? Mmc { get; init; }

    /// <summary>Account number for Money Manager Client.</summary>
    [JsonPropertyName("accountId")]
    public string AccountId { get; init; } = string.Empty;

    /// <summary>Returns if this is a Multiplex Parent Account.</summary>
    [JsonPropertyName("isMParent")]
    public bool IsMParent { get; init; }

    /// <summary>Returns if this is a Multiplex Child Account.</summary>
    [JsonPropertyName("isMChild")]
    public bool IsMChild { get; init; }

    /// <summary>Is a Multiplex Account.</summary>
    [JsonPropertyName("isMultiplex")]
    public bool IsMultiplex { get; init; }

    /// <summary>Additional unmapped properties from the API response.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

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
/// <param name="ExerciseStyle">The exercise style for options (e.g., "A" for American, "E" for European).</param>
/// <param name="ConExchMap">List of exchange mappings for the contract.</param>
/// <param name="UndConid">The underlying contract identifier (0 if not applicable).</param>
/// <param name="Model">The model code (empty string if not applicable).</param>
[ExcludeFromCodeCoverage]
public record Position(
    [property: JsonPropertyName("acctId")] string AccountId,
    [property: JsonPropertyName("conid")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    long Conid,
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
    [property: JsonPropertyName("isUS")] bool? IsUs,
    [property: JsonPropertyName("exerciseStyle")] string? ExerciseStyle = null,
    [property: JsonPropertyName("conExchMap")] List<string>? ConExchMap = null,
    [property: JsonPropertyName("undConid")] long UndConid = 0,
    [property: JsonPropertyName("model")] string? Model = null)
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
/// <param name="Severity">Severity level indicator (0 = normal).</param>
[ExcludeFromCodeCoverage]
public record AccountSummaryEntry(
    [property: JsonPropertyName("amount")] decimal? Amount,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("isNull")] bool IsNull,
    [property: JsonPropertyName("timestamp")] long? Timestamp,
    [property: JsonPropertyName("value")] string? Value,
    [property: JsonPropertyName("severity")] int Severity = 0)
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
/// Generated from recorded API response — 30 fields matching actual wire format.
/// </summary>
/// <param name="CommodityMarketValue">Market value of commodity positions.</param>
/// <param name="FutureMarketValue">Market value of futures positions.</param>
/// <param name="SettledCash">Settled cash amount.</param>
/// <param name="ExchangeRate">Exchange rate to base currency.</param>
/// <param name="SessionId">Session identifier.</param>
/// <param name="CashBalance">Cash balance in this currency.</param>
/// <param name="CorporateBondsMarketValue">Market value of corporate bonds.</param>
/// <param name="WarrantsMarketValue">Market value of warrants.</param>
/// <param name="NetLiquidationValue">Net liquidation value in this currency.</param>
/// <param name="Interest">Accrued interest.</param>
/// <param name="UnrealizedPnl">Unrealized profit and loss.</param>
/// <param name="StockMarketValue">Market value of stock positions.</param>
/// <param name="MoneyFunds">Money market fund value.</param>
/// <param name="Currency">The currency code.</param>
/// <param name="RealizedPnl">Realized profit and loss.</param>
/// <param name="Funds">Fund value.</param>
/// <param name="AcctCode">The account code.</param>
/// <param name="IssuerOptionsMarketValue">Market value of issuer options.</param>
/// <param name="Key">Ledger key identifier.</param>
/// <param name="Timestamp">Unix timestamp of the ledger entry.</param>
/// <param name="Severity">Severity level indicator.</param>
/// <param name="StockOptionMarketValue">Market value of stock options.</param>
/// <param name="FuturesOnlyPnl">Futures-only profit and loss.</param>
/// <param name="TBondsMarketValue">Market value of treasury bonds.</param>
/// <param name="FutureOptionMarketValue">Market value of future options.</param>
/// <param name="CashBalanceFxSegment">Cash balance in FX segment.</param>
/// <param name="SecondKey">Secondary key (typically currency code).</param>
/// <param name="TBillsMarketValue">Market value of treasury bills.</param>
/// <param name="EndOfBundle">End-of-bundle indicator.</param>
/// <param name="Dividends">Dividend income.</param>
/// <param name="CryptocurrencyValue">Cryptocurrency position value.</param>
[ExcludeFromCodeCoverage]
public record LedgerEntry(
    [property: JsonPropertyName("commoditymarketvalue")] decimal CommodityMarketValue,
    [property: JsonPropertyName("futuremarketvalue")] decimal FutureMarketValue,
    [property: JsonPropertyName("settledcash")] decimal SettledCash,
    [property: JsonPropertyName("exchangerate")] decimal ExchangeRate,
    [property: JsonPropertyName("sessionid")] int SessionId,
    [property: JsonPropertyName("cashbalance")] decimal CashBalance,
    [property: JsonPropertyName("corporatebondsmarketvalue")] decimal CorporateBondsMarketValue,
    [property: JsonPropertyName("warrantsmarketvalue")] decimal WarrantsMarketValue,
    [property: JsonPropertyName("netliquidationvalue")] decimal NetLiquidationValue,
    [property: JsonPropertyName("interest")] decimal Interest,
    [property: JsonPropertyName("unrealizedpnl")] decimal UnrealizedPnl,
    [property: JsonPropertyName("stockmarketvalue")] decimal StockMarketValue,
    [property: JsonPropertyName("moneyfunds")] decimal MoneyFunds,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("realizedpnl")] decimal RealizedPnl,
    [property: JsonPropertyName("funds")] decimal Funds,
    [property: JsonPropertyName("acctcode")] string? AcctCode,
    [property: JsonPropertyName("issueroptionsmarketvalue")] decimal IssuerOptionsMarketValue,
    [property: JsonPropertyName("key")] string? Key,
    [property: JsonPropertyName("timestamp")] long Timestamp,
    [property: JsonPropertyName("severity")] int Severity,
    [property: JsonPropertyName("stockoptionmarketvalue")] decimal StockOptionMarketValue,
    [property: JsonPropertyName("futuresonlypnl")] decimal FuturesOnlyPnl,
    [property: JsonPropertyName("tbondsmarketvalue")] decimal TBondsMarketValue,
    [property: JsonPropertyName("futureoptionmarketvalue")] decimal FutureOptionMarketValue,
    [property: JsonPropertyName("cashbalancefxsegment")] decimal CashBalanceFxSegment,
    [property: JsonPropertyName("secondkey")] string? SecondKey,
    [property: JsonPropertyName("tbillsmarketvalue")] decimal TBillsMarketValue,
    [property: JsonPropertyName("endofbundle")] int EndOfBundle,
    [property: JsonPropertyName("dividends")] decimal Dividends,
    [property: JsonPropertyName("cryptocurrencyvalue")] decimal CryptocurrencyValue)
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
[ExcludeFromCodeCoverage]
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
[ExcludeFromCodeCoverage]
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
[ExcludeFromCodeCoverage]
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
[ExcludeFromCodeCoverage]
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
[ExcludeFromCodeCoverage]
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
[ExcludeFromCodeCoverage]
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
[ExcludeFromCodeCoverage]
public record TransactionHistoryRequest(
    [property: JsonPropertyName("acctIds")] List<string> AccountIds,
    [property: JsonPropertyName("conids")] List<string> Conids,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("days")] int? Days);

/// <summary>
/// Request body for the POST /portfolio/allocation endpoint (consolidated allocation).
/// </summary>
/// <param name="AccountIds">The account IDs to consolidate allocation for.</param>
[ExcludeFromCodeCoverage]
public record ConsolidatedAllocationRequest(
    [property: JsonPropertyName("acctIds")] List<string> AccountIds);

/// <summary>
/// Request body for the POST /pa/allperiods endpoint.
/// </summary>
/// <param name="AccountIds">The account IDs to query all-period performance for.</param>
[ExcludeFromCodeCoverage]
public record AllPeriodsRequest(
    [property: JsonPropertyName("acctIds")] List<string> AccountIds);

/// <summary>
/// Represents a combination (spread) position from the /portfolio/{accountId}/combo/positions endpoint.
/// </summary>
/// <param name="Name">Internal combo name (e.g., "CP.CP66a00d50").</param>
/// <param name="Description">Ratio and leg conIds description (e.g., "1*708474422-1*710225103").</param>
/// <param name="Legs">The legs composing the combination.</param>
/// <param name="Positions">Position details for each leg.</param>
[ExcludeFromCodeCoverage]
public record ComboPosition(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("legs")] List<ComboLeg>? Legs,
    [property: JsonPropertyName("positions")] List<Position>? Positions)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Represents a single leg within a combination position.
/// </summary>
/// <param name="Conid">The contract identifier for this leg.</param>
/// <param name="Ratio">The ratio (positive for long, negative for short).</param>
[ExcludeFromCodeCoverage]
public record ComboLeg(
    [property: JsonPropertyName("conid")] string? Conid,
    [property: JsonPropertyName("ratio")] int? Ratio)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Represents a sub-account from the /portfolio/subaccounts endpoint (FA/IBroker).
/// </summary>
/// <param name="Id">The sub-account identifier.</param>
/// <param name="AccountId">The account ID string.</param>
/// <param name="AccountTitle">The account title.</param>
/// <param name="AccountType">The account type.</param>
/// <param name="Description">The account description.</param>
[ExcludeFromCodeCoverage]
public record SubAccount(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("accountId")] string? AccountId,
    [property: JsonPropertyName("accountTitle")] string? AccountTitle,
    [property: JsonPropertyName("type")] string? AccountType,
    [property: JsonPropertyName("desc")] string? Description)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Represents all-period performance data from the /pa/allperiods endpoint.
/// </summary>
/// <param name="CurrencyType">The currency type (e.g., "base").</param>
/// <param name="Rc">Return code.</param>
[ExcludeFromCodeCoverage]
public record AllPeriodsPerformance(
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
/// Represents partitioned P&amp;L data from the /iserver/account/pnl/partitioned endpoint.
/// </summary>
/// <param name="Upnl">Updated P&amp;L entries keyed by account/model (e.g., "U1234567.Core").</param>
[ExcludeFromCodeCoverage]
public record PartitionedPnl(
    [property: JsonPropertyName("upnl")] Dictionary<string, PnlEntry>? Upnl)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Represents a single P&amp;L entry within a partitioned P&amp;L response.
/// </summary>
/// <param name="RowType">Positional value (1 for individual accounts).</param>
/// <param name="Dpl">Daily P&amp;L.</param>
/// <param name="Nl">Net liquidity.</param>
/// <param name="Upl">Unrealized P&amp;L.</param>
/// <param name="El">Excess liquidity.</param>
/// <param name="Mv">Margin value.</param>
[ExcludeFromCodeCoverage]
public record PnlEntry(
    [property: JsonPropertyName("rowType")] int? RowType,
    [property: JsonPropertyName("dpl")] decimal? Dpl,
    [property: JsonPropertyName("nl")] decimal? Nl,
    [property: JsonPropertyName("upl")] decimal? Upl,
    [property: JsonPropertyName("el")] decimal? El,
    [property: JsonPropertyName("mv")] decimal? Mv)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
