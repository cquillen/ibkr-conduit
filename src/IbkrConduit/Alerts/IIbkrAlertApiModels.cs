using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrConduit.Alerts;

/// <summary>
/// Request body for POST /iserver/account/{accountId}/alert to create or modify an alert.
/// </summary>
/// <param name="OrderId">Order ID (0 for new alerts, existing ID for modifications).</param>
/// <param name="AlertName">Name of the alert.</param>
/// <param name="AlertMessage">Message to display when triggered.</param>
/// <param name="AlertRepeatable">Whether the alert can repeat (0 = no, 1 = yes).</param>
/// <param name="OutsideRth">Whether the alert is active outside regular trading hours (0 = no, 1 = yes).</param>
/// <param name="Conditions">List of alert conditions.</param>
[ExcludeFromCodeCoverage]
public record CreateAlertRequest(
    [property: JsonPropertyName("orderId")] int OrderId,
    [property: JsonPropertyName("alertName")] string AlertName,
    [property: JsonPropertyName("alertMessage")] string AlertMessage,
    [property: JsonPropertyName("alertRepeatable")] int AlertRepeatable,
    [property: JsonPropertyName("outsideRth")] int OutsideRth,
    [property: JsonPropertyName("conditions")] List<AlertCondition> Conditions);

/// <summary>
/// A condition within an alert definition.
/// </summary>
/// <param name="Type">Condition type (1 = price, 3 = time, etc.).</param>
/// <param name="Conidex">Contract identifier expression (e.g., "265598").</param>
/// <param name="Operator">Comparison operator ("&gt;=" or "&lt;=").</param>
/// <param name="TriggerMethod">Trigger method ("0" = default).</param>
/// <param name="Value">Threshold value for the condition.</param>
[ExcludeFromCodeCoverage]
public record AlertCondition(
    [property: JsonPropertyName("type")] int Type,
    [property: JsonPropertyName("conidex")] string Conidex,
    [property: JsonPropertyName("operator")] string Operator,
    [property: JsonPropertyName("triggerMethod")] string TriggerMethod,
    [property: JsonPropertyName("value")] string Value)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Response from POST /iserver/account/{accountId}/alert.
/// </summary>
/// <param name="RequestId">The request identifier.</param>
/// <param name="OrderId">The order/alert identifier.</param>
/// <param name="OrderStatus">Status of the alert order.</param>
/// <param name="Text">Status text message.</param>
[ExcludeFromCodeCoverage]
public record CreateAlertResponse(
    [property: JsonPropertyName("request_id")] int RequestId,
    [property: JsonPropertyName("order_id")] int OrderId,
    [property: JsonPropertyName("order_status")] string OrderStatus,
    [property: JsonPropertyName("text")] string? Text)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Summary of an alert from GET /iserver/account/mta.
/// </summary>
/// <param name="AccountId">The account that owns the alert.</param>
/// <param name="OrderId">The alert order identifier.</param>
/// <param name="AlertName">Name of the alert.</param>
/// <param name="AlertActive">Whether the alert is currently active (0 or 1).</param>
/// <param name="OrderStatus">Current status of the alert.</param>
[ExcludeFromCodeCoverage]
public record AlertSummary(
    [property: JsonPropertyName("account")] string AccountId,
    [property: JsonPropertyName("order_id")] int OrderId,
    [property: JsonPropertyName("alert_name")] string AlertName,
    [property: JsonPropertyName("alert_active")] int AlertActive,
    [property: JsonPropertyName("order_status")] string OrderStatus)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Detailed alert information from GET /iserver/account/alert/{alertId}.
/// </summary>
/// <param name="AccountId">The account that owns the alert.</param>
/// <param name="OrderId">The alert order identifier.</param>
/// <param name="AlertName">Name of the alert.</param>
/// <param name="AlertMessage">Message displayed when the alert triggers.</param>
/// <param name="AlertActive">Whether the alert is currently active (0 or 1).</param>
/// <param name="AlertRepeatable">Whether the alert repeats (0 or 1).</param>
/// <param name="Conditions">List of conditions for this alert.</param>
[ExcludeFromCodeCoverage]
public record AlertDetail(
    [property: JsonPropertyName("account")] string AccountId,
    [property: JsonPropertyName("order_id")] int OrderId,
    [property: JsonPropertyName("alert_name")] string AlertName,
    [property: JsonPropertyName("alert_message")] string AlertMessage,
    [property: JsonPropertyName("alert_active")] int AlertActive,
    [property: JsonPropertyName("alert_repeatable")] int AlertRepeatable,
    [property: JsonPropertyName("conditions")] List<AlertCondition> Conditions)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Response from DELETE /iserver/account/{accountId}/alert/{alertId}.
/// </summary>
/// <param name="RequestId">The request identifier.</param>
/// <param name="OrderId">The alert order identifier.</param>
/// <param name="Msg">Status message.</param>
/// <param name="Text">Additional text.</param>
[ExcludeFromCodeCoverage]
public record DeleteAlertResponse(
    [property: JsonPropertyName("request_id")] int RequestId,
    [property: JsonPropertyName("order_id")] int OrderId,
    [property: JsonPropertyName("msg")] string? Msg,
    [property: JsonPropertyName("text")] string? Text)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
