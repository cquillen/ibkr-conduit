using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace IbkrConduit.Orders;

/// <summary>
/// Request body for POST /iserver/notification to dismiss a server prompt
/// received via the WebSocket <c>ntf</c> (notification) topic.
/// </summary>
/// <param name="OrderId">IB-assigned order identifier from the <c>ntf</c> WebSocket message.</param>
/// <param name="ReqId">IB-assigned request identifier from the <c>ntf</c> WebSocket message.</param>
/// <param name="Text">The selected value from the prompt's <c>options</c> array (e.g., "Yes", "No").</param>
[ExcludeFromCodeCoverage]
public record DismissNotificationRequest(
    [property: JsonPropertyName("orderId")] int OrderId,
    [property: JsonPropertyName("reqId")] string ReqId,
    [property: JsonPropertyName("text")] string Text);
