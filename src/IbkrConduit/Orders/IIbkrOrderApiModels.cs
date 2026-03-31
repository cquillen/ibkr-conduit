using System.Text.Json.Serialization;

namespace IbkrConduit.Orders;

/// <summary>
/// Order request built by the consumer.
/// </summary>
public record OrderRequest
{
    /// <summary>IBKR contract ID for the instrument.</summary>
    public int Conid { get; init; }

    /// <summary>"BUY" or "SELL".</summary>
    public string Side { get; init; } = string.Empty;

    /// <summary>Order quantity.</summary>
    public decimal Quantity { get; init; }

    /// <summary>Order type: "MKT", "LMT", "STP", "STP_LMT", "MOC", "LOC", "TRAIL".</summary>
    public string OrderType { get; init; } = string.Empty;

    /// <summary>Limit price. Required for LMT, STP_LMT, LOC.</summary>
    public decimal? Price { get; init; }

    /// <summary>Aux/stop price. Required for STP, STP_LMT, TRAIL.</summary>
    public decimal? AuxPrice { get; init; }

    /// <summary>Time in force: "DAY", "GTC", "IOC".</summary>
    public string Tif { get; init; } = "DAY";

    /// <summary>
    /// Required for US Futures orders submitted by automated systems.
    /// Set to false for programmatic orders. CME Group Rule 536-B compliance.
    /// </summary>
    public bool? ManualIndicator { get; init; }
}

/// <summary>
/// Successful order placement result.
/// </summary>
/// <param name="OrderId">The IBKR order identifier.</param>
/// <param name="OrderStatus">The status of the placed order.</param>
public record OrderResult(string OrderId, string OrderStatus);

/// <summary>
/// Order cancellation result from the IBKR API.
/// </summary>
/// <param name="Message">The cancellation message.</param>
/// <param name="OrderId">The cancelled order identifier.</param>
/// <param name="Conid">The contract identifier of the cancelled order.</param>
public record CancelOrderResponse(
    [property: JsonPropertyName("msg")] string Message,
    [property: JsonPropertyName("order_id")] string OrderId,
    [property: JsonPropertyName("conid")] int Conid);

/// <summary>
/// Wrapper for the orders array sent to IBKR.
/// </summary>
/// <param name="Orders">The list of orders to submit.</param>
public record OrdersPayload(
    [property: JsonPropertyName("orders")] List<OrderWireModel> Orders);

/// <summary>
/// Wire format for a single order sent to IBKR.
/// </summary>
/// <param name="Conid">The IBKR contract identifier.</param>
/// <param name="Side">The order side: "BUY" or "SELL".</param>
/// <param name="Quantity">The order quantity.</param>
/// <param name="OrderType">The order type (e.g., "MKT", "LMT").</param>
/// <param name="Price">The limit price, if applicable.</param>
/// <param name="AuxPrice">The auxiliary/stop price, if applicable.</param>
/// <param name="Tif">Time in force (e.g., "DAY", "GTC").</param>
/// <param name="ManualIndicator">Manual indicator for CME compliance, if applicable.</param>
public record OrderWireModel(
    [property: JsonPropertyName("conid")] int Conid,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("quantity")] decimal Quantity,
    [property: JsonPropertyName("orderType")] string OrderType,
    [property: JsonPropertyName("price")] decimal? Price,
    [property: JsonPropertyName("auxPrice")] decimal? AuxPrice,
    [property: JsonPropertyName("tif")] string Tif,
    [property: JsonPropertyName("manualIndicator")] bool? ManualIndicator);

/// <summary>
/// Raw response from order submission or reply. Can be a question or a confirmation.
/// </summary>
/// <param name="Id">The reply/question identifier, present when IBKR asks a follow-up question.</param>
/// <param name="Message">Question messages from IBKR, present when confirmation is needed.</param>
/// <param name="IsSuppressed">Whether the question has been suppressed.</param>
/// <param name="MessageIds">Message IDs associated with the question.</param>
/// <param name="OrderId">The order identifier, present on successful placement.</param>
/// <param name="OrderStatus">The order status, present on successful placement.</param>
public record OrderSubmissionResponse(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("message")] List<string>? Message,
    [property: JsonPropertyName("isSuppressed")] bool? IsSuppressed,
    [property: JsonPropertyName("messageIds")] List<string>? MessageIds,
    [property: JsonPropertyName("order_id")] string? OrderId,
    [property: JsonPropertyName("order_status")] string? OrderStatus);

/// <summary>
/// Reply confirmation body sent to IBKR to confirm or reject an order question.
/// </summary>
/// <param name="Confirmed">Whether to confirm the question.</param>
public record ReplyRequest(
    [property: JsonPropertyName("confirmed")] bool Confirmed);

/// <summary>
/// Live orders response wrapper from the IBKR API.
/// </summary>
/// <param name="Orders">The list of live orders, if any.</param>
public record OrdersResponse(
    [property: JsonPropertyName("orders")] List<LiveOrder>? Orders);

/// <summary>
/// A live order in the current session.
/// </summary>
/// <param name="OrderId">The order identifier.</param>
/// <param name="Conid">The contract identifier.</param>
/// <param name="Symbol">The ticker symbol.</param>
/// <param name="Side">The order side.</param>
/// <param name="Quantity">The order quantity.</param>
/// <param name="OrderType">The order type.</param>
/// <param name="Price">The order price, if applicable.</param>
/// <param name="Status">The order status.</param>
/// <param name="FilledQuantity">The filled quantity.</param>
/// <param name="RemainingQuantity">The remaining quantity.</param>
public record LiveOrder(
    [property: JsonPropertyName("orderId")] string OrderId,
    [property: JsonPropertyName("conid")] int Conid,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("quantity")] decimal Quantity,
    [property: JsonPropertyName("orderType")] string OrderType,
    [property: JsonPropertyName("price")] decimal? Price,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("filledQuantity")] decimal FilledQuantity,
    [property: JsonPropertyName("remainingQuantity")] decimal RemainingQuantity);

/// <summary>
/// A completed trade from the IBKR API.
/// </summary>
/// <param name="ExecutionId">The execution identifier.</param>
/// <param name="Conid">The contract identifier.</param>
/// <param name="Symbol">The ticker symbol.</param>
/// <param name="Side">The trade side.</param>
/// <param name="Size">The trade size.</param>
/// <param name="Price">The execution price.</param>
/// <param name="OrderRef">The order reference.</param>
/// <param name="Submitter">The trade submitter.</param>
public record Trade(
    [property: JsonPropertyName("execution_id")] string ExecutionId,
    [property: JsonPropertyName("conid")] int Conid,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("size")] decimal Size,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("order_ref")] string OrderRef,
    [property: JsonPropertyName("submitter")] string Submitter);
