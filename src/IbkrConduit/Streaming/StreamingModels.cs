using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrConduit.Serialization;

namespace IbkrConduit.Streaming;

/// <summary>
/// A real-time market data tick from the WebSocket smd topic.
/// </summary>
[ExcludeFromCodeCoverage]
public record MarketDataTick
{
    /// <summary>Contract identifier.</summary>
    public int Conid { get; init; }

    /// <summary>Epoch timestamp of the update.</summary>
    [JsonPropertyName("_updated")]
    public long? Updated { get; init; }

    /// <summary>All field values keyed by field ID string.</summary>
    public IReadOnlyDictionary<string, string>? Fields { get; init; }

    /// <summary>Additional data not mapped to known properties.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// A real-time order update from the WebSocket sor topic. <c>sor</c> frames are sparse deltas —
/// a later frame carries only <see cref="OrderId"/> plus whatever changed — so every wire-optional
/// field is nullable and <c>null</c> means "not present in this frame", never a fabricated
/// <c>0</c>/empty. A consumer merging deltas must overwrite only on non-null fields.
/// </summary>
[ExcludeFromCodeCoverage]
public record OrderUpdate
{
    /// <summary>
    /// Order identifier. IBKR sends this as a JSON number on some frames and a quoted
    /// string on others; <see cref="FlexibleStringJsonConverter"/> normalizes either shape
    /// to a string.
    /// </summary>
    [JsonPropertyName("orderId")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string OrderId { get; init; } = string.Empty;

    /// <summary>Contract identifier. Null when this <c>sor</c> frame omits it (delta frames are sparse).</summary>
    [JsonPropertyName("conid")]
    public int? Conid { get; init; }

    /// <summary>Ticker symbol. IBKR sends this under the <c>ticker</c> key on <c>sor</c> frames. Null when absent from this frame.</summary>
    [JsonPropertyName("ticker")]
    public string? Symbol { get; init; }

    /// <summary>Order side (BUY/SELL). Null when absent from this frame.</summary>
    [JsonPropertyName("side")]
    public string? Side { get; init; }

    /// <summary>Order size. IBKR sends this under the <c>totalSize</c> key on <c>sor</c> frames. Null when absent from this frame — never a fabricated 0.</summary>
    [JsonPropertyName("totalSize")]
    public decimal? Size { get; init; }

    /// <summary>Order type (e.g., MKT, LMT). Null when absent from this frame.</summary>
    [JsonPropertyName("orderType")]
    public string? OrderType { get; init; }

    /// <summary>Limit price, if applicable. Null when absent from this frame (e.g. a market order, or a delta that omits it).</summary>
    [JsonPropertyName("price")]
    public decimal? Price { get; init; }

    /// <summary>Order status. Null when absent from this frame — never a fabricated empty string.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    /// <summary>Filled quantity. Null when absent from this frame — never a fabricated 0, so a sparse-delta merge can tell "not in this frame" from a genuine zero fill.</summary>
    [JsonPropertyName("filledQuantity")]
    public decimal? FilledQuantity { get; init; }

    /// <summary>Remaining quantity. Null when absent from this frame — never a fabricated 0, so a sparse-delta merge never regresses a partially-filled order to unfilled.</summary>
    [JsonPropertyName("remainingQuantity")]
    public decimal? RemainingQuantity { get; init; }

    /// <summary>
    /// User-defined order reference echoing the <c>cOID</c> supplied at placement
    /// (IBKR <c>order_ref</c>). Present only when a <see cref="IbkrConduit.Orders.OrderRequest.CustomerOrderId"/>
    /// was set on the order; otherwise null.
    /// </summary>
    [JsonPropertyName("order_ref")]
    public string? OrderRef { get; init; }

    /// <summary>Additional data not mapped to known properties.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// A real-time profit and loss update from the WebSocket spl topic.
/// </summary>
[ExcludeFromCodeCoverage]
public record PnlUpdate
{
    /// <summary>Account identifier.</summary>
    [JsonPropertyName("acctId")]
    public string AccountId { get; init; } = string.Empty;

    /// <summary>Daily profit and loss. Null when IBKR omits it or sends an empty value.</summary>
    [JsonPropertyName("dpl")]
    public decimal? DailyPnl { get; init; }

    /// <summary>Unrealized profit and loss. Null when IBKR omits it or sends an empty value.</summary>
    [JsonPropertyName("upl")]
    public decimal? UnrealizedPnl { get; init; }

    /// <summary>Realized profit and loss. Null when IBKR omits it or sends an empty value.</summary>
    [JsonPropertyName("rpl")]
    public decimal? RealizedPnl { get; init; }

    /// <summary>Net liquidation value. Null when IBKR omits it or sends an empty value.</summary>
    [JsonPropertyName("nl")]
    public decimal? NetLiquidation { get; init; }

    /// <summary>Additional data not mapped to known properties.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// A real-time account summary update from the WebSocket ssd topic. The frame carries no
/// account field of its own; <see cref="AccountId"/> is parsed by the mapper from the
/// frame's <c>topic</c> (e.g. <c>"ssd+DU1234567"</c>).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record AccountSummaryUpdate
{
    /// <summary>Account identifier, parsed from the frame's <c>topic</c>.</summary>
    public string AccountId { get; init; } = string.Empty;

    /// <summary>Account summary rows for the requested keys/fields.</summary>
    public IReadOnlyList<AccountSummaryRow> Result { get; init; } = [];
}

/// <summary>
/// A single account summary value from the WebSocket ssd topic's <c>result</c> array.
/// </summary>
/// <param name="Key">Name of the account summary value (e.g. "ExcessLiquidity-S"). Always returned.</param>
/// <param name="Currency">The currency of <paramref name="MonetaryValue"/> (e.g. "USD"), when the key pertains to pricing/balance.</param>
/// <param name="MonetaryValue">A monetary value; returned when the key pertains to pricing/balance.</param>
/// <param name="Severity">Internal-use severity code.</param>
/// <param name="Timestamp">Epoch timestamp when the value was retrieved. Always returned.</param>
[ExcludeFromCodeCoverage]
public sealed record AccountSummaryRow(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("monetaryValue")] decimal? MonetaryValue,
    [property: JsonPropertyName("severity")] int? Severity,
    [property: JsonPropertyName("timestamp")] long? Timestamp)
{
    /// <summary>
    /// A non-monetary account summary value returned when the key does not pertain to pricing/balance
    /// (e.g. <c>{"key":"Cushion","value":"1"}</c>). Kept as the raw wire string. Null on a monetary
    /// row, where the value lives in <see cref="MonetaryValue"/> instead (PRB-3.3, design §12.5).
    /// </summary>
    [JsonPropertyName("value")]
    public string? Value { get; init; }

    /// <summary>
    /// Additional account summary row fields not mapped to a known property — the overflow bag that
    /// mirrors its <c>sld</c> sibling <see cref="AccountLedgerRow.AdditionalData"/> so a wire-shape
    /// addition survives instead of being dropped (design §12.5).
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// A real-time account ledger update from the WebSocket sld topic. The frame carries no
/// account field of its own; <see cref="AccountId"/> is parsed by the mapper from the
/// frame's <c>topic</c> (e.g. <c>"sld+DU1234567"</c>).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record AccountLedgerUpdate
{
    /// <summary>Account identifier, parsed from the frame's <c>topic</c>.</summary>
    public string AccountId { get; init; } = string.Empty;

    /// <summary>Ledger rows, one per currency.</summary>
    public IReadOnlyList<AccountLedgerRow> Result { get; init; } = [];
}

/// <summary>
/// A single currency's ledger row from the WebSocket sld topic's <c>result</c> array. A new
/// message is published every 10 seconds; a currency's entry is "blank" (only <see cref="Key"/>
/// and <see cref="Timestamp"/> set) when nothing changed in the preceding interval.
/// </summary>
/// <param name="AccountCode">The account code the ledger entry belongs to.</param>
/// <param name="Key">Ledger currency key (e.g. "LedgerListBASE", "LedgerListUSD"). Always returned.</param>
/// <param name="SecondKey">Secondary currency key (e.g. "BASE").</param>
/// <param name="CashBalance">Cash balance in the ledger's currency.</param>
/// <param name="SettledCash">Settled cash balance.</param>
/// <param name="NetLiquidationValue">Net liquidation value.</param>
/// <param name="StockMarketValue">Market value of stock positions.</param>
/// <param name="RealizedPnl">Realized profit and loss.</param>
/// <param name="UnrealizedPnl">Unrealized profit and loss.</param>
/// <param name="ExchangeRate">Exchange rate to the base currency.</param>
/// <param name="Dividends">Accrued dividends.</param>
/// <param name="Interest">Accrued interest.</param>
/// <param name="Timestamp">Epoch timestamp when the row was retrieved. Always returned.</param>
[ExcludeFromCodeCoverage]
public sealed record AccountLedgerRow(
    [property: JsonPropertyName("acctCode")] string? AccountCode,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("secondKey")] string? SecondKey,
    [property: JsonPropertyName("cashbalance")] decimal? CashBalance,
    [property: JsonPropertyName("settledCash")] decimal? SettledCash,
    [property: JsonPropertyName("netLiquidationValue")] decimal? NetLiquidationValue,
    [property: JsonPropertyName("stockMarketValue")] decimal? StockMarketValue,
    [property: JsonPropertyName("realizedPnl")] decimal? RealizedPnl,
    [property: JsonPropertyName("unrealizedPnl")] decimal? UnrealizedPnl,
    [property: JsonPropertyName("exchangeRate")] decimal? ExchangeRate,
    [property: JsonPropertyName("dividends")] decimal? Dividends,
    [property: JsonPropertyName("interest")] decimal? Interest,
    [property: JsonPropertyName("timestamp")] long? Timestamp)
{
    /// <summary>Additional ledger fields not mapped to known properties (e.g. commodityMarketValue, funds, marketValue).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// A real-time trade execution (fill) from the WebSocket <c>str</c> topic. One item
/// is emitted per execution. On subscribe IBKR replays historical executions (up to
/// the requested <c>days</c>) and repeats them after any reconnect, so consumers
/// should dedupe on <see cref="ExecutionId"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public record TradeExecution
{
    /// <summary>Execution identifier of the specific trade. Natural dedupe key.</summary>
    [JsonPropertyName("execution_id")]
    public string ExecutionId { get; init; } = string.Empty;

    /// <summary>Ticker symbol of the traded contract.</summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Whether the contract supports the tax optimizer (Client Portal only). IBKR sends "1"/"0"; parsed to bool.</summary>
    [JsonPropertyName("supports_tax_opt")]
    [JsonConverter(typeof(FlexibleBoolJsonConverter))]
    public bool? SupportsTaxOpt { get; init; }

    /// <summary>Trade side (buy or sell).</summary>
    [JsonPropertyName("side")]
    public string Side { get; init; } = string.Empty;

    /// <summary>Full order description, formatted "{SIDE} {SIZE} @ {PRICE} on {EXCHANGE}".</summary>
    [JsonPropertyName("order_description")]
    public string? OrderDescription { get; init; }

    /// <summary>Trade date-time in UTC, formatted "YYYYMMDD-HH:mm:ss". Kept raw.</summary>
    [JsonPropertyName("trade_time")]
    public string? TradeTime { get; init; }

    /// <summary>Trade date-time of the execution in epoch milliseconds.</summary>
    [JsonPropertyName("trade_time_r")]
    public long? TradeTimeR { get; init; }

    /// <summary>Quantity of shares traded. Null when IBKR omits it or sends an empty value — never a fabricated 0.</summary>
    [JsonPropertyName("size")]
    public decimal? Size { get; init; }

    /// <summary>Custom order identifier (cOID) supplied at order placement, if any.</summary>
    [JsonPropertyName("order_ref")]
    public string? OrderRef { get; init; }

    /// <summary>Execution price. IBKR sends this as a quoted string; parsed to decimal. Null when IBKR omits it or sends an empty value.</summary>
    [JsonPropertyName("price")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? Price { get; init; }

    /// <summary>Exchange the order executed at.</summary>
    [JsonPropertyName("exchange")]
    public string? Exchange { get; init; }

    /// <summary>Total amount traded after applying the contract multiplier. Null when IBKR omits it or sends an empty value.</summary>
    [JsonPropertyName("net_amount")]
    public decimal? NetAmount { get; init; }

    /// <summary>Account the order was traded on.</summary>
    [JsonPropertyName("account")]
    public string Account { get; init; } = string.Empty;

    /// <summary>Account code the order was traded on.</summary>
    [JsonPropertyName("accountCode")]
    public string? AccountCode { get; init; }

    /// <summary>Title of the company for the contract.</summary>
    [JsonPropertyName("company_name")]
    public string? CompanyName { get; init; }

    /// <summary>Underlying asset symbol for derivative contracts.</summary>
    [JsonPropertyName("contract_description_1")]
    public string? ContractDescription1 { get; init; }

    /// <summary>Full description of the derivative.</summary>
    [JsonPropertyName("contract_description_2")]
    public string? ContractDescription2 { get; init; }

    /// <summary>Security type traded (e.g., STK, OPT, FUT).</summary>
    [JsonPropertyName("sec_type")]
    public string? SecType { get; init; }

    /// <summary>Contract identifier for the traded contract.</summary>
    [JsonPropertyName("conid")]
    public int Conid { get; init; }

#pragma warning disable CA1711 // ConidEx is the IBKR API field name — suffix is not a .NET type convention issue
    /// <summary>The conidEx of the order if specified; otherwise the conid.</summary>
    [JsonPropertyName("conidEx")]
    public string? ConidEx { get; init; }
#pragma warning restore CA1711

    /// <summary>Whether the execution was a closing trade. "???" when the position was already open but not a closing order.</summary>
    [JsonPropertyName("open_close")]
    public string? OpenClose { get; init; }

    /// <summary>Whether the trade resulted from a liquidation. IBKR sends "1"/"0"; parsed to bool.</summary>
    [JsonPropertyName("liquidation_trade")]
    [JsonConverter(typeof(FlexibleBoolJsonConverter))]
    public bool? LiquidationTrade { get; init; }

    /// <summary>Whether the order can be used with EventTrader. IBKR sends "1"/"0"; parsed to bool.</summary>
    [JsonPropertyName("is_event_trading")]
    [JsonConverter(typeof(FlexibleBoolJsonConverter))]
    public bool? IsEventTrading { get; init; }

    /// <summary>
    /// The IBKR order id this execution belongs to. IBKR sends it as a JSON number on
    /// <c>str</c> frames; <see cref="FlexibleStringJsonConverter"/> normalizes it to a string
    /// so it correlates with <see cref="OrderUpdate.OrderId"/>. Null when IBKR omits it.
    /// </summary>
    [JsonPropertyName("order_id")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? OrderId { get; init; }

    /// <summary>Commission cost for the trade. IBKR sends a quoted value; parsed to decimal. Present only once IBKR has computed it (a later frame for the same execution), null otherwise.</summary>
    [JsonPropertyName("commission")]
    public decimal? Commission { get; init; }

    /// <summary>Resulting total position size in the contract after this execution. Null when IBKR omits it or sends an empty value.</summary>
    [JsonPropertyName("position")]
    public decimal? Position { get; init; }

    /// <summary>Primary listing exchange of the contract (distinct from <see cref="Exchange"/>, the execution venue).</summary>
    [JsonPropertyName("listing_exchange")]
    public string? ListingExchange { get; init; }

    /// <summary>Allocation account name for the execution.</summary>
    [JsonPropertyName("account_allocation_name")]
    public string? AccountAllocationName { get; init; }

    /// <summary>Clearing firm identifier (e.g., "IB").</summary>
    [JsonPropertyName("clearing_id")]
    public string? ClearingId { get; init; }

    /// <summary>Clearing firm name.</summary>
    [JsonPropertyName("clearing_name")]
    public string? ClearingName { get; init; }

    /// <summary>Additional data not mapped to known properties.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>Pushed when the brokerage authentication state changes (e.g., competing session, server-side timeout).</summary>
[ExcludeFromCodeCoverage]
public sealed record SessionStatusEvent
{
    /// <summary>
    /// True if the brokerage session is currently authenticated, false if it is not, and
    /// <c>null</c> when the <c>sts</c> frame carried no authentication verdict at all — an absent
    /// verdict is never fabricated into <c>false</c>, so a consumer never reads "IBKR said nothing"
    /// as "IBKR said the session is dead".
    /// </summary>
    public bool? Authenticated { get; init; }

    /// <summary>
    /// True when the <c>sts</c> frame reports this session is competing with another login for the
    /// brokerage session, false when it explicitly reports no competition, and <c>null</c> when the
    /// frame carried no competing verdict (ADR-0001 presence semantics — absence is never fabricated
    /// into <c>false</c>). A <c>true</c> value is the push-side signal of a competing takeover; it
    /// also feeds the passive session-health snapshot.
    /// </summary>
    public bool? Competing { get; init; }

    /// <summary>
    /// The failure reason carried on the <c>sts</c> frame (the <c>fail</c> field), or <c>null</c>
    /// when the frame carried no <c>fail</c> field at all. An empty string is preserved as-is
    /// (present but empty), distinct from <c>null</c> (absent), per ADR-0001.
    /// </summary>
    public string? FailReason { get; init; }
}

/// <summary>Urgent message about exchange issues, system problems, or trading information.</summary>
[ExcludeFromCodeCoverage]
public sealed record BulletinEvent
{
    /// <summary>Unique identifier for the bulletin (use to dedupe across reconnects).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The bulletin text.</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>Brief message regarding trading activity.</summary>
[ExcludeFromCodeCoverage]
public sealed record NotificationEvent
{
    /// <summary>Unique identifier for the notification.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The notification headline.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>The notification body text.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Optional URL with more information; null if not provided.</summary>
    public string? Url { get; init; }
}

/// <summary>System-level WebSocket events: initial connection confirmation and periodic 10-second server heartbeats.</summary>
[ExcludeFromCodeCoverage]
public sealed record SystemEvent
{
    /// <summary>The IBKR username confirmed by the server. Set on the initial connection message; null on periodic heartbeats.</summary>
    public string? Username { get; init; }

    /// <summary>
    /// Server-side heartbeat timestamp in unix milliseconds. Set on periodic heartbeats; null on the initial connection message.
    /// </summary>
    public long? HeartbeatMs { get; init; }

    /// <summary>True if the account supports fractional trading. Set on the initial connection message; null on periodic heartbeats.</summary>
    public bool? IsFT { get; init; }

    /// <summary>True if the active account is a paper-trading account. Set on the initial connection message; null on periodic heartbeats.</summary>
    public bool? IsPaper { get; init; }
}

/// <summary>
/// Account configuration sent on initial WebSocket connect and whenever
/// account-level details change. Not financial data — see PnlUpdate /
/// AccountSummaryUpdate / AccountLedgerUpdate for live financial figures.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record AccountStatusEvent
{
    /// <summary>All accounts currently accessible by the user.</summary>
    public IReadOnlyList<string> Accounts { get; init; } = [];

    /// <summary>Per-account properties keyed by account ID. The IBKR sample uses the literal key "All" for properties shared across accounts.</summary>
    public IReadOnlyDictionary<string, AccountProperties> AcctProps { get; init; } = new Dictionary<string, AccountProperties>();

    /// <summary>Account aliases (account ID → display name). Empty when no aliases configured.</summary>
    public IReadOnlyDictionary<string, string> Aliases { get; init; } = new Dictionary<string, string>();

    /// <summary>Allowed features for the account (capability flags).</summary>
    public AccountFeatures? AllowFeatures { get; init; }

    /// <summary>Trading-hours periods supported by each security type, keyed by SecurityType code.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ChartPeriods { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>Groups the account is listed under.</summary>
    public IReadOnlyList<string> Groups { get; init; } = [];

    /// <summary>Profiles the account is listed under.</summary>
    public IReadOnlyList<string> Profiles { get; init; } = [];

    /// <summary>The currently selected account ID.</summary>
    public string SelectedAccount { get; init; } = string.Empty;

    /// <summary>Server identification.</summary>
    public StreamingServerInfo? ServerInfo { get; init; }

    /// <summary>The brokerage session identifier.</summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// True if the account supports fractional trading, false if not, and <c>null</c> when the
    /// <c>act</c> frame omitted the flag — absence is never fabricated into <c>false</c>. Matches
    /// <see cref="SystemEvent.IsFT"/>'s presence semantics.
    /// </summary>
    public bool? IsFT { get; init; }

    /// <summary>
    /// True if the active account is a paper-trading account, false if not, and <c>null</c> when
    /// the <c>act</c> frame omitted the flag — absence is never fabricated into <c>false</c>, so a
    /// consumer's real-money interlock never reads "IBKR said nothing" as "IBKR said live account".
    /// Matches <see cref="SystemEvent.IsPaper"/>'s presence semantics.
    /// </summary>
    public bool? IsPaper { get; init; }
}

/// <summary>Per-account capability flags.</summary>
[ExcludeFromCodeCoverage]
public sealed record AccountProperties
{
    /// <summary>True if the account has child accounts.</summary>
    public bool HasChildAccounts { get; init; }

    /// <summary>True if the account supports cash-quantity orders.</summary>
    public bool SupportsCashQty { get; init; }

    /// <summary>True if FX conversion is disabled for the account.</summary>
    public bool NoFXConv { get; init; }

    /// <summary>True if the account is a proprietary trading account.</summary>
    public bool IsProp { get; init; }

    /// <summary>True if the account supports fractional-share trading.</summary>
    public bool SupportsFractions { get; init; }

    /// <summary>True if customer-time order routing is allowed.</summary>
    public bool AllowCustomerTime { get; init; }

    /// <summary>True if this is a Lite account operating under a Pro umbrella.</summary>
    public bool LiteUnderPro { get; init; }

    /// <summary>True if automatic FX conversion is enabled for the account.</summary>
    public bool AutoFx { get; init; }
}

/// <summary>Account-level feature flags from the <c>allowFeatures</c> object.</summary>
[ExcludeFromCodeCoverage]
public sealed record AccountFeatures
{
    /// <summary>True if Global Financial Information Services (GFIS) is shown.</summary>
    public bool ShowGFIS { get; init; }

    /// <summary>True if the EU cost report is shown.</summary>
    public bool ShowEUCostReport { get; init; }

    /// <summary>True if event-contract trading is allowed.</summary>
    public bool AllowEventContract { get; init; }

    /// <summary>True if FX conversion is allowed.</summary>
    public bool AllowFXConv { get; init; }

    /// <summary>True if Financial Lens features are allowed.</summary>
    public bool AllowFinancialLens { get; init; }

    /// <summary>True if Mobile Trading Assistant (MTA) is allowed.</summary>
    public bool AllowMTA { get; init; }

    /// <summary>True if type-ahead search is allowed.</summary>
    public bool AllowTypeAhead { get; init; }

    /// <summary>True if event trading is allowed.</summary>
    public bool AllowEventTrading { get; init; }

    /// <summary>Snapshot refresh timeout in seconds, when configured.</summary>
    public int? SnapshotRefreshTimeout { get; init; }

    /// <summary>True if the account is a Lite (commission-free) account.</summary>
    public bool LiteUser { get; init; }

    /// <summary>True if web-news access is shown.</summary>
    public bool ShowWebNews { get; init; }

    /// <summary>True if research features are allowed.</summary>
    public bool Research { get; init; }

    /// <summary>True if PnL debugging is enabled.</summary>
    public bool DebugPnl { get; init; }

    /// <summary>True if tax-optimizer features are shown.</summary>
    public bool ShowTaxOpt { get; init; }

    /// <summary>True if the impact dashboard is shown.</summary>
    public bool ShowImpactDashboard { get; init; }

    /// <summary>True if dynamic-account selection is allowed.</summary>
    public bool AllowDynAccount { get; init; }

    /// <summary>True if crypto trading is allowed.</summary>
    public bool AllowCrypto { get; init; }

    /// <summary>True if Financial Advisor (FA) features are allowed.</summary>
    public bool AllowFA { get; init; }

    /// <summary>True if Lite-under-Pro account configuration is allowed.</summary>
    public bool AllowLiteUnderPro { get; init; }

    /// <summary>Comma-separated list of allowed asset types (e.g., "STK,OPT,FUT,CASH").</summary>
    public string? AllowedAssetTypes { get; init; }

    /// <summary>True if subscription-based trade restrictions apply.</summary>
    public bool RestrictTradeSubscription { get; init; }

    /// <summary>True if UK-specific user labels are shown.</summary>
    public bool ShowUkUserLabels { get; init; }

    /// <summary>True if side-by-side layout is enabled.</summary>
    public bool SideBySide { get; init; }
}

/// <summary>Server identification carried by AccountStatusEvent. Distinct from any session-internal ServerInfo type.</summary>
[ExcludeFromCodeCoverage]
public sealed record StreamingServerInfo
{
    /// <summary>The server name reported by IBKR.</summary>
    public string ServerName { get; init; } = string.Empty;

    /// <summary>The server software version reported by IBKR.</summary>
    public string ServerVersion { get; init; } = string.Empty;
}
