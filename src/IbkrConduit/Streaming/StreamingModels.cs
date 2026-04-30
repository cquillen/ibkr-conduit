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
