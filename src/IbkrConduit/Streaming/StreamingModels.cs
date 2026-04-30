using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

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
/// A real-time order update from the WebSocket sor topic.
/// </summary>
[ExcludeFromCodeCoverage]
public record OrderUpdate
{
    /// <summary>Order identifier.</summary>
    [JsonPropertyName("orderId")]
    public string OrderId { get; init; } = string.Empty;

    /// <summary>Contract identifier.</summary>
    [JsonPropertyName("conid")]
    public int Conid { get; init; }

    /// <summary>Ticker symbol.</summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Order side (BUY/SELL).</summary>
    [JsonPropertyName("side")]
    public string Side { get; init; } = string.Empty;

    /// <summary>Order size.</summary>
    [JsonPropertyName("size")]
    public decimal Size { get; init; }

    /// <summary>Order type (e.g., MKT, LMT).</summary>
    [JsonPropertyName("orderType")]
    public string OrderType { get; init; } = string.Empty;

    /// <summary>Limit price, if applicable.</summary>
    [JsonPropertyName("price")]
    public decimal? Price { get; init; }

    /// <summary>Order status.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>Filled quantity.</summary>
    [JsonPropertyName("filledQuantity")]
    public decimal FilledQuantity { get; init; }

    /// <summary>Remaining quantity.</summary>
    [JsonPropertyName("remainingQuantity")]
    public decimal RemainingQuantity { get; init; }

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

    /// <summary>Daily profit and loss.</summary>
    [JsonPropertyName("dpl")]
    public decimal DailyPnl { get; init; }

    /// <summary>Unrealized profit and loss.</summary>
    [JsonPropertyName("upl")]
    public decimal UnrealizedPnl { get; init; }

    /// <summary>Realized profit and loss.</summary>
    [JsonPropertyName("rpl")]
    public decimal RealizedPnl { get; init; }

    /// <summary>Net liquidation value.</summary>
    [JsonPropertyName("nl")]
    public decimal NetLiquidation { get; init; }

    /// <summary>Additional data not mapped to known properties.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// A real-time account summary update from the WebSocket ssd topic.
/// </summary>
[ExcludeFromCodeCoverage]
public record AccountSummaryUpdate
{
    /// <summary>Account identifier.</summary>
    [JsonPropertyName("acctId")]
    public string AccountId { get; init; } = string.Empty;

    /// <summary>Key-value pairs of account summary fields.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Data { get; init; }
}

/// <summary>
/// A real-time account ledger update from the WebSocket sld topic.
/// </summary>
[ExcludeFromCodeCoverage]
public record AccountLedgerUpdate
{
    /// <summary>Account identifier.</summary>
    [JsonPropertyName("acctId")]
    public string AccountId { get; init; } = string.Empty;

    /// <summary>Currency-keyed ledger data.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Data { get; init; }
}

/// <summary>Pushed when the brokerage authentication state changes (e.g., competing session, server-side timeout).</summary>
[ExcludeFromCodeCoverage]
public sealed record SessionStatusEvent
{
    /// <summary>True if the brokerage session is currently authenticated.</summary>
    public bool Authenticated { get; init; }
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
    /// Field name is assumed to be "hb" per IBKR convention; the IBKR doc text references the heartbeat but does not name the field. To be verified against real traffic post-merge.
    /// </summary>
    public long? HeartbeatMs { get; init; }
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

    /// <summary>True if the account supports fractional trading.</summary>
    public bool IsFT { get; init; }

    /// <summary>True if the active account is a paper-trading account.</summary>
    public bool IsPaper { get; init; }
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

    /// <summary>Comma-separated list of allowed asset types (e.g., "STK,OPT,FUT,CASH").</summary>
    public string? AllowedAssetTypes { get; init; }
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
