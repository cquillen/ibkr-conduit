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
/// <param name="Tif">Time in force: "GTC" (good till cancelled) or "GTD" (good till date).</param>
/// <param name="Conditions">List of alert conditions.</param>
/// <param name="Email">Optional email address for alert notifications.</param>
/// <param name="ExpireTime">Optional expiration time for GTD alerts.</param>
/// <param name="ITWSOrdersOnly">Optional flag to restrict to iTWS orders only.</param>
/// <param name="SendMessage">Optional flag to send a message on trigger.</param>
/// <param name="ShowPopup">Optional flag to show a popup on trigger.</param>
[ExcludeFromCodeCoverage]
public record CreateAlertRequest(
    [property: JsonPropertyName("orderId")] int OrderId,
    [property: JsonPropertyName("alertName")] string AlertName,
    [property: JsonPropertyName("alertMessage")] string AlertMessage,
    [property: JsonPropertyName("alertRepeatable")] int AlertRepeatable,
    [property: JsonPropertyName("outsideRth")] int OutsideRth,
    [property: JsonPropertyName("tif")] string Tif,
    [property: JsonPropertyName("conditions")] List<AlertCondition> Conditions,
    [property: JsonPropertyName("email")] string? Email = null,
    [property: JsonPropertyName("expireTime")] string? ExpireTime = null,
    [property: JsonPropertyName("iTWSOrdersOnly")] int? ITWSOrdersOnly = null,
    [property: JsonPropertyName("sendMessage")] int? SendMessage = null,
    [property: JsonPropertyName("showPopup")] int? ShowPopup = null);

/// <summary>
/// A condition within an alert creation/modification request.
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
/// A condition within an alert detail response. Uses different field names than the request.
/// </summary>
/// <param name="ConditionType">Condition type (1 = price, 3 = time, etc.).</param>
/// <param name="Conidex">Contract identifier expression.</param>
/// <param name="ContractDescription1">Human-readable contract description.</param>
/// <param name="ConditionOperator">Comparison operator.</param>
/// <param name="ConditionTriggerMethod">Trigger method.</param>
/// <param name="ConditionValue">Threshold value for the condition.</param>
/// <param name="ConditionLogicBind">Logic binding to next condition ("a" = AND, "o" = OR).</param>
/// <param name="ConditionTimeZone">Time zone for time-based conditions.</param>
[ExcludeFromCodeCoverage]
public record AlertConditionDetail(
    [property: JsonPropertyName("condition_type")] int ConditionType,
    [property: JsonPropertyName("conidex")] string Conidex,
    [property: JsonPropertyName("contract_description_1")] string? ContractDescription1,
    [property: JsonPropertyName("condition_operator")] string ConditionOperator,
    [property: JsonPropertyName("condition_trigger_method")] string ConditionTriggerMethod,
    [property: JsonPropertyName("condition_value")] string ConditionValue,
    [property: JsonPropertyName("condition_logic_bind")] string ConditionLogicBind,
    [property: JsonPropertyName("condition_time_zone")] string? ConditionTimeZone)
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
/// <param name="Success">Whether the alert was created successfully.</param>
/// <param name="Text">Status text message.</param>
/// <param name="OrderStatus">Status of the alert order.</param>
/// <param name="WarningMessage">Warning message, if any.</param>
[ExcludeFromCodeCoverage]
public record CreateAlertResponse(
    [property: JsonPropertyName("request_id")] int RequestId,
    [property: JsonPropertyName("order_id")] int OrderId,
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("order_status")] string? OrderStatus = null,
    [property: JsonPropertyName("warning_message")] string? WarningMessage = null)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Summary of an alert from GET /iserver/account/{accountId}/alerts or GET /iserver/account/mta.
/// </summary>
/// <param name="Account">The account that owns the alert.</param>
/// <param name="OrderId">The alert order identifier.</param>
/// <param name="AlertName">Name of the alert.</param>
/// <param name="AlertActive">Whether the alert is currently active (0 or 1).</param>
/// <param name="AlertRepeatable">Whether the alert can repeat (0 or 1).</param>
/// <param name="OrderTime">Time the alert order was placed.</param>
/// <param name="AlertTriggered">Whether the alert has been triggered.</param>
[ExcludeFromCodeCoverage]
public record AlertSummary(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("order_id")] int OrderId,
    [property: JsonPropertyName("alert_name")] string AlertName,
    [property: JsonPropertyName("alert_active")] int AlertActive,
    [property: JsonPropertyName("alert_repeatable")] int AlertRepeatable,
    [property: JsonPropertyName("order_time")] string? OrderTime = null,
    [property: JsonPropertyName("alert_triggered")] bool AlertTriggered = false)
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
/// <param name="Account">The account that owns the alert.</param>
/// <param name="OrderId">The alert order identifier.</param>
/// <param name="AlertName">Name of the alert.</param>
/// <param name="AlertMessage">Message displayed when the alert triggers.</param>
/// <param name="AlertActive">Whether the alert is currently active (0 or 1).</param>
/// <param name="AlertRepeatable">Whether the alert repeats (0 or 1).</param>
/// <param name="Conditions">List of conditions for this alert.</param>
/// <param name="Tif">Time in force (GTC or GTD).</param>
/// <param name="ExpireTime">Expiration time for GTD alerts.</param>
/// <param name="AlertEmail">Email address for alert notifications.</param>
/// <param name="AlertSendMessage">Whether to send a message on trigger.</param>
/// <param name="AlertShowPopup">Whether to show a popup on trigger.</param>
/// <param name="AlertPlayAudio">Whether to play audio on trigger.</param>
/// <param name="OrderStatus">Current status of the alert order.</param>
/// <param name="AlertTriggered">Whether the alert has been triggered.</param>
/// <param name="FgColor">Foreground color for display.</param>
/// <param name="BgColor">Background color for display.</param>
/// <param name="OrderNotEditable">Whether the order is not editable.</param>
/// <param name="ItwsOrdersOnly">Whether restricted to iTWS orders only.</param>
/// <param name="AlertMtaCurrency">MTA currency setting.</param>
/// <param name="AlertMtaDefaults">MTA defaults setting.</param>
/// <param name="ToolId">Tool identifier.</param>
/// <param name="TimeZone">Time zone for the alert.</param>
/// <param name="AlertDefaultType">Default alert type.</param>
/// <param name="ConditionSize">Number of conditions.</param>
/// <param name="ConditionOutsideRth">Whether conditions apply outside regular trading hours.</param>
[ExcludeFromCodeCoverage]
public record AlertDetail(
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("order_id")] int OrderId,
    [property: JsonPropertyName("alert_name")] string AlertName,
    [property: JsonPropertyName("alert_message")] string AlertMessage,
    [property: JsonPropertyName("alert_active")] int AlertActive,
    [property: JsonPropertyName("alert_repeatable")] int AlertRepeatable,
    [property: JsonPropertyName("conditions")] List<AlertConditionDetail> Conditions,
    [property: JsonPropertyName("tif")] string? Tif = null,
    [property: JsonPropertyName("expire_time")] string? ExpireTime = null,
    [property: JsonPropertyName("alert_email")] string? AlertEmail = null,
    [property: JsonPropertyName("alert_send_message")] int? AlertSendMessage = null,
    [property: JsonPropertyName("alert_show_popup")] int? AlertShowPopup = null,
    [property: JsonPropertyName("alert_play_audio")] int? AlertPlayAudio = null,
    [property: JsonPropertyName("order_status")] string? OrderStatus = null,
    [property: JsonPropertyName("alert_triggered")] bool AlertTriggered = false,
    [property: JsonPropertyName("fg_color")] string? FgColor = null,
    [property: JsonPropertyName("bg_color")] string? BgColor = null,
    [property: JsonPropertyName("order_not_editable")] bool OrderNotEditable = false,
    [property: JsonPropertyName("itws_orders_only")] int ItwsOrdersOnly = 0,
    [property: JsonPropertyName("alert_mta_currency")] string? AlertMtaCurrency = null,
    [property: JsonPropertyName("alert_mta_defaults")] string? AlertMtaDefaults = null,
    [property: JsonPropertyName("tool_id")] long? ToolId = null,
    [property: JsonPropertyName("time_zone")] string? TimeZone = null,
    [property: JsonPropertyName("alert_default_type")] int? AlertDefaultType = null,
    [property: JsonPropertyName("condition_size")] int ConditionSize = 0,
    [property: JsonPropertyName("condition_outside_rth")] int ConditionOutsideRth = 0)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Request body for POST /iserver/account/{accountId}/alert/activate.
/// </summary>
/// <param name="AlertId">The alert identifier to activate or deactivate.</param>
/// <param name="AlertActive">Whether to activate (1) or deactivate (0) the alert.</param>
[ExcludeFromCodeCoverage]
public record AlertActivationRequest(
    [property: JsonPropertyName("alertId")] int AlertId,
    [property: JsonPropertyName("alertActive")] int AlertActive);

/// <summary>
/// Response from POST /iserver/account/{accountId}/alert/activate.
/// </summary>
/// <param name="RequestId">The request identifier.</param>
/// <param name="OrderId">The alert order identifier.</param>
/// <param name="Success">Whether the activation was successful.</param>
/// <param name="Text">Status text message.</param>
/// <param name="FailureList">Comma-separated list of failures, if any.</param>
[ExcludeFromCodeCoverage]
public record AlertActivationResponse(
    [property: JsonPropertyName("request_id")] int? RequestId,
    [property: JsonPropertyName("order_id")] int OrderId,
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("failure_list")] string? FailureList = null)
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
/// <param name="Success">Whether the deletion was successful.</param>
/// <param name="Text">Status text message.</param>
/// <param name="FailureList">Comma-separated list of failures, if any.</param>
[ExcludeFromCodeCoverage]
public record DeleteAlertResponse(
    [property: JsonPropertyName("request_id")] int RequestId,
    [property: JsonPropertyName("order_id")] int OrderId,
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("failure_list")] string? FailureList = null)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
