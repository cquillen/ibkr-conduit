using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrConduit.Orders;

/// <summary>
/// Order request built by the consumer.
/// </summary>
[ExcludeFromCodeCoverage]
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
[ExcludeFromCodeCoverage]
public record OrderResult(string OrderId, string OrderStatus);

/// <summary>
/// Order cancellation result from the IBKR API.
/// </summary>
/// <param name="Message">The cancellation message.</param>
/// <param name="OrderId">The cancelled order identifier.</param>
/// <param name="Conid">The contract identifier of the cancelled order.</param>
[ExcludeFromCodeCoverage]
public record CancelOrderResponse(
    [property: JsonPropertyName("msg")] string Message,
    [property: JsonPropertyName("order_id")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    int OrderId,
    [property: JsonPropertyName("conid")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    int Conid);

/// <summary>
/// Wrapper for the orders array sent to IBKR.
/// </summary>
/// <param name="Orders">The list of orders to submit.</param>
[ExcludeFromCodeCoverage]
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
[ExcludeFromCodeCoverage]
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
[ExcludeFromCodeCoverage]
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
[ExcludeFromCodeCoverage]
public record ReplyRequest(
    [property: JsonPropertyName("confirmed")] bool Confirmed);

/// <summary>
/// Live orders response wrapper from the IBKR API.
/// </summary>
/// <param name="Orders">The list of live orders, if any.</param>
[ExcludeFromCodeCoverage]
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
[ExcludeFromCodeCoverage]
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
[ExcludeFromCodeCoverage]
public record Trade(
    [property: JsonPropertyName("execution_id")] string ExecutionId,
    [property: JsonPropertyName("conid")] int Conid,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("size")] decimal Size,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("order_ref")] string OrderRef,
    [property: JsonPropertyName("submitter")] string Submitter);

/// <summary>
/// What-if (commission/margin preview) response from the IBKR API.
/// </summary>
/// <param name="Amount">The order amount details.</param>
/// <param name="Equity">The equity impact details.</param>
/// <param name="Initial">The initial margin requirement.</param>
/// <param name="Maintenance">The maintenance margin requirement.</param>
/// <param name="Warning">Warning message, if any.</param>
/// <param name="Error">Error message, if any.</param>
[ExcludeFromCodeCoverage]
public record WhatIfResponse(
    [property: JsonPropertyName("amount")] WhatIfAmount? Amount,
    [property: JsonPropertyName("equity")] WhatIfEquity? Equity,
    [property: JsonPropertyName("initial")] WhatIfMargin? Initial,
    [property: JsonPropertyName("maintenance")] WhatIfMargin? Maintenance,
    [property: JsonPropertyName("warn")] string? Warning,
    [property: JsonPropertyName("error")] string? Error)
{
    /// <summary>Additional unmapped properties from the API response.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Amount details in a what-if response.
/// </summary>
/// <param name="Amount">The order amount.</param>
/// <param name="Commission">The estimated commission.</param>
/// <param name="Total">The total including commission.</param>
[ExcludeFromCodeCoverage]
public record WhatIfAmount(
    [property: JsonPropertyName("amount")] string? Amount,
    [property: JsonPropertyName("commission")] string? Commission,
    [property: JsonPropertyName("total")] string? Total)
{
    /// <summary>Additional unmapped properties from the API response.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Equity impact details in a what-if response.
/// </summary>
/// <param name="Current">The current equity.</param>
/// <param name="Change">The equity change.</param>
/// <param name="After">The equity after the order.</param>
[ExcludeFromCodeCoverage]
public record WhatIfEquity(
    [property: JsonPropertyName("current")] string? Current,
    [property: JsonPropertyName("change")] string? Change,
    [property: JsonPropertyName("after")] string? After)
{
    /// <summary>Additional unmapped properties from the API response.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Margin details in a what-if response (used for both initial and maintenance).
/// </summary>
/// <param name="Current">The current margin.</param>
/// <param name="Change">The margin change.</param>
/// <param name="After">The margin after the order.</param>
[ExcludeFromCodeCoverage]
public record WhatIfMargin(
    [property: JsonPropertyName("current")] string? Current,
    [property: JsonPropertyName("change")] string? Change,
    [property: JsonPropertyName("after")] string? After)
{
    /// <summary>Additional unmapped properties from the API response.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

#pragma warning disable CA1711 // ConidEx is the IBKR API field name — suffix is not a .NET type convention issue
/// <summary>
/// Detailed status for a single order from the IBKR API.
/// </summary>
/// <param name="SubType">The order sub-type.</param>
/// <param name="RequestId">The request identifier.</param>
/// <param name="OrderId">The order identifier.</param>
/// <param name="ConidEx">The extended contract identifier.</param>
/// <param name="Conid">The contract identifier.</param>
/// <param name="Symbol">The ticker symbol.</param>
/// <param name="Side">The order side.</param>
/// <param name="ContractDescription">The contract description.</param>
/// <param name="ListingExchange">The listing exchange.</param>
/// <param name="IsEventTrading">Whether this is event trading.</param>
/// <param name="OrderDescription">The order description.</param>
/// <param name="Status">The order status.</param>
/// <param name="OrderType">The order type.</param>
/// <param name="Size">The order size.</param>
/// <param name="FillPrice">The fill price.</param>
/// <param name="FilledQuantity">The filled quantity.</param>
/// <param name="RemainingQuantity">The remaining quantity.</param>
/// <param name="AvgFillPrice">The average fill price.</param>
/// <param name="LastFillPrice">The last fill price.</param>
/// <param name="TotalSize">The total size.</param>
/// <param name="TotalCashSize">The total cash size.</param>
/// <param name="Price">The order price.</param>
/// <param name="Tif">The time in force.</param>
/// <param name="BgColor">The background color hint.</param>
/// <param name="FgColor">The foreground color hint.</param>
/// <param name="OrderNotEditable">Whether the order is not editable.</param>
/// <param name="EditableFields">The editable fields.</param>
/// <param name="CannotCancelOrder">Whether the order cannot be cancelled.</param>
[ExcludeFromCodeCoverage]
public record OrderStatus(
    [property: JsonPropertyName("sub_type")] string? SubType,
    [property: JsonPropertyName("request_id")] string? RequestId,
    [property: JsonPropertyName("order_id")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    int OrderId,
    [property: JsonPropertyName("conidex")] string? ConidEx,
    [property: JsonPropertyName("conid")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    int Conid,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("contract_description_1")] string? ContractDescription,
    [property: JsonPropertyName("listing_exchange")] string? ListingExchange,
    [property: JsonPropertyName("is_event_trading")] string? IsEventTrading,
    [property: JsonPropertyName("order_desc")] string? OrderDescription,
    [property: JsonPropertyName("order_status")] string Status,
    [property: JsonPropertyName("order_type")] string? OrderType,
    [property: JsonPropertyName("size")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    decimal? Size,
    [property: JsonPropertyName("fill_price")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    decimal? FillPrice,
    [property: JsonPropertyName("filled_quantity")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    decimal? FilledQuantity,
    [property: JsonPropertyName("remaining_quantity")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    decimal? RemainingQuantity,
    [property: JsonPropertyName("avg_fill_price")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    decimal? AvgFillPrice,
    [property: JsonPropertyName("last_fill_price")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    decimal? LastFillPrice,
    [property: JsonPropertyName("total_size")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    decimal? TotalSize,
    [property: JsonPropertyName("total_cash_size")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    decimal? TotalCashSize,
    [property: JsonPropertyName("price")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    decimal? Price,
    [property: JsonPropertyName("tif")] string? Tif,
    [property: JsonPropertyName("bg_color")] string? BgColor,
    [property: JsonPropertyName("fg_color")] string? FgColor,
    [property: JsonPropertyName("order_not_editable")] bool? OrderNotEditable,
    [property: JsonPropertyName("editable_fields")] string? EditableFields,
    [property: JsonPropertyName("cannot_cancel_order")] bool? CannotCancelOrder)
{
    /// <summary>Additional unmapped properties from the API response.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
#pragma warning restore CA1711
