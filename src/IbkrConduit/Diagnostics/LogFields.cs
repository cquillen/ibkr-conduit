using System.Diagnostics.CodeAnalysis;

namespace IbkrConduit.Diagnostics;

/// <summary>
/// Standard structured log field names used across all IbkrConduit components.
/// </summary>
[ExcludeFromCodeCoverage]
public static class LogFields
{
    /// <summary>Tenant identifier for multi-tenant sessions.</summary>
    public const string TenantId = "ibkr.tenant_id";

    /// <summary>IBKR account identifier.</summary>
    public const string AccountId = "ibkr.account_id";

    /// <summary>IBKR contract identifier.</summary>
    public const string Conid = "ibkr.conid";

    /// <summary>Order identifier.</summary>
    public const string OrderId = "ibkr.order_id";

    /// <summary>Instrument symbol.</summary>
    public const string Symbol = "ibkr.symbol";

    /// <summary>API endpoint path.</summary>
    public const string Endpoint = "ibkr.endpoint";

    /// <summary>HTTP method.</summary>
    public const string Method = "ibkr.method";

    /// <summary>HTTP status code.</summary>
    public const string StatusCode = "ibkr.status_code";

    /// <summary>Duration in milliseconds.</summary>
    public const string DurationMs = "ibkr.duration_ms";

    /// <summary>Number of question/reply iterations in an order submission.</summary>
    public const string QuestionCount = "ibkr.question_count";

    /// <summary>Number of poll attempts in a Flex query.</summary>
    public const string PollCount = "ibkr.poll_count";

    /// <summary>Event trigger (e.g., tickle failure, token expiry).</summary>
    public const string Trigger = "ibkr.trigger";

    /// <summary>WebSocket or streaming topic.</summary>
    public const string Topic = "ibkr.topic";

    /// <summary>Flex query template identifier.</summary>
    public const string QueryId = "ibkr.query_id";

    /// <summary>Retry attempt number.</summary>
    public const string Attempt = "ibkr.attempt";

    /// <summary>Whether the value was served from cache.</summary>
    public const string Cached = "ibkr.cached";

    /// <summary>Whether a market data pre-flight request was needed.</summary>
    public const string PreflightNeeded = "ibkr.preflight_needed";

    /// <summary>Order side (BUY or SELL).</summary>
    public const string Side = "ibkr.side";

    /// <summary>Order type (LMT, MKT, STP, etc.).</summary>
    public const string OrderType = "ibkr.order_type";

    /// <summary>Error code from IBKR or Flex service.</summary>
    public const string ErrorCode = "ibkr.error_code";
}
