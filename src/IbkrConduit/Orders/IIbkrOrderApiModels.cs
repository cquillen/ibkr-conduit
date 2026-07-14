using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrConduit.Serialization;

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

    /// <summary>
    /// Order type. Documented core values: "LMT", "MKT", "STP", "STOP_LIMIT", "MIDPRICE", "TRAIL",
    /// "TRAILLMT". Also accepted in live probes (1 sample each): "LOC", "MOC", "MIT", "LIT", "REL" —
    /// not a closed set; IBKR documents no admissible enum for this field.
    /// A STOP_LIMIT order requires both <see cref="Price"/> and <see cref="AuxPrice"/>.
    /// </summary>
    public string OrderType { get; init; } = string.Empty;

    /// <summary>
    /// Limit price (the stop price for STP/TRAIL; the option price cap for MIDPRICE).
    /// Required for LMT or STOP_LIMIT.
    /// </summary>
    public decimal? Price { get; init; }

    /// <summary>
    /// Stop price for STOP_LIMIT and TRAILLMT orders. A STOP_LIMIT order requires both
    /// <see cref="Price"/> and <see cref="AuxPrice"/>.
    /// </summary>
    public decimal? AuxPrice { get; init; }

    /// <summary>Time in force: "DAY", "GTC", "IOC".</summary>
    public string Tif { get; init; } = "DAY";

    /// <summary>
    /// Trailing offset for a trailing order (IBKR <c>trailingAmt</c>), interpreted per
    /// <see cref="TrailingType"/>: an absolute price amount when <c>"amt"</c>, a percentage when
    /// <c>"%"</c>. Required — together with <see cref="TrailingType"/> — when <see cref="OrderType"/>
    /// is "TRAIL" or "TRAILLMT"; a placement or modification of such an order that omits either field
    /// fails fast with an <see cref="ArgumentException"/> before any wire activity. Omitted from the
    /// wire when null.
    /// </summary>
    public decimal? TrailingAmt { get; init; }

    /// <summary>
    /// Trailing type for a trailing order (IBKR <c>trailingType</c>). Valid values: <c>"amt"</c>
    /// (an absolute price offset) or <c>"%"</c> (a percentage offset). Required — together with
    /// <see cref="TrailingAmt"/> — when <see cref="OrderType"/> is "TRAIL" or "TRAILLMT"; a placement
    /// or modification of such an order that omits either field fails fast with an
    /// <see cref="ArgumentException"/> before any wire activity. Omitted from the wire when null.
    /// </summary>
    public string? TrailingType { get; init; }

    /// <summary>
    /// Required for US Futures orders submitted by automated systems.
    /// Set to false for programmatic orders. CME Group Rule 536-B compliance.
    /// </summary>
    public bool? ManualIndicator { get; init; }

    /// <summary>
    /// External operator identifying the submitting user in charge of the API operation. IBKR
    /// documents this field as required "when trading Futures and Futures Options contracts to remain
    /// in compliance with CME Group Rule 536-B" (companion to <see cref="ManualIndicator"/>). Optional
    /// pass-through: omitted from the wire when null and never validated client-side — the library
    /// forwards whatever the consumer supplies (enforcement is documented-not-verified).
    /// </summary>
    public string? ExtOperator { get; init; }

    /// <summary>
    /// Customer Order ID (IBKR <c>cOID</c>). An arbitrary string identifying the order; must be
    /// unique within a rolling 24-hour span. Set on a parent order to link children that reference
    /// it via <see cref="ParentId"/>. Do not set on child orders. Echoed back as <c>order_ref</c>
    /// on live orders and trades.
    /// </summary>
    public string? CustomerOrderId { get; init; }

    /// <summary>
    /// Parent linkage for bracket/OCA child orders (IBKR <c>parentId</c>). Set only on child orders;
    /// the value must equal the parent order's <see cref="CustomerOrderId"/>.
    /// </summary>
    public string? ParentId { get; init; }

    /// <summary>
    /// Marks the order as part of a one-cancels-all (OCA) group (IBKR <c>isSingleGroup</c>).
    /// Set to <c>true</c> on every order in the group.
    /// </summary>
    public bool? IsSingleGroup { get; init; }

    /// <summary>
    /// Allows the order to execute outside regular trading hours (IBKR <c>outsideRTH</c>).
    /// </summary>
    public bool? OutsideRth { get; init; }
}

/// <summary>
/// Order cancellation result from the IBKR API.
/// </summary>
/// <param name="Message">The cancellation message.</param>
/// <param name="OrderId">The cancelled order identifier.</param>
/// <param name="Conid">
/// The contract identifier of the cancelled order. IBKR sends <c>-1</c>, paired with a
/// <c>null</c> <see cref="Account"/>, as a documented sentinel meaning the order was
/// immediately cancelled on request (see <see cref="Account"/>).
/// </param>
[ExcludeFromCodeCoverage]
public record CancelOrderResponse(
    [property: JsonPropertyName("msg")] string Message,
    [property: JsonPropertyName("order_id")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    int OrderId,
    [property: JsonPropertyName("conid")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    int Conid)
{
    /// <summary>
    /// The account the cancelled order was originally set to clear on (IBKR <c>account</c>).
    /// <c>null</c>, paired with <see cref="Conid"/> == <c>-1</c>, is a documented sentinel
    /// (DOC-01, DOC-03) meaning the order was immediately cancelled on request — not a
    /// generically-absent field.
    /// </summary>
    [JsonPropertyName("account")]
    public string? Account { get; init; }

    /// <summary>Additional unmapped properties from the API response.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

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
/// <param name="OrderType">The order type. Documented core values: "LMT", "MKT", "STP", "STOP_LIMIT", "MIDPRICE", "TRAIL", "TRAILLMT". Also accepted in live probes (1 sample each): "LOC", "MOC", "MIT", "LIT", "REL" — not a closed set; IBKR documents no admissible enum for this field. A STOP_LIMIT order requires both Price and AuxPrice.</param>
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
    [property: JsonPropertyName("manualIndicator")] bool? ManualIndicator)
{
    /// <summary>Customer Order ID (<c>cOID</c>); omitted from the wire payload when null.</summary>
    [JsonPropertyName("cOID")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CustomerOrderId { get; init; }

    /// <summary>Parent linkage for child orders (<c>parentId</c>); omitted when null.</summary>
    [JsonPropertyName("parentId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ParentId { get; init; }

    /// <summary>OCA group flag (<c>isSingleGroup</c>); omitted when null.</summary>
    [JsonPropertyName("isSingleGroup")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsSingleGroup { get; init; }

    /// <summary>Outside-regular-trading-hours flag (<c>outsideRTH</c>); omitted when null.</summary>
    [JsonPropertyName("outsideRTH")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? OutsideRth { get; init; }

    /// <summary>
    /// External operator for futures/futures-options CME Group Rule 536-B compliance
    /// (<c>extOperator</c>); omitted when null. Pure pass-through — not validated client-side.
    /// </summary>
    [JsonPropertyName("extOperator")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExtOperator { get; init; }

    /// <summary>Trailing offset for TRAIL/TRAILLMT orders (<c>trailingAmt</c>); omitted when null.</summary>
    [JsonPropertyName("trailingAmt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? TrailingAmt { get; init; }

    /// <summary>Trailing type for TRAIL/TRAILLMT orders (<c>trailingType</c>: "amt" or "%"); omitted when null.</summary>
    [JsonPropertyName("trailingType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TrailingType { get; init; }
}

/// <summary>
/// Raw response from order submission or reply. Can be a question or a confirmation.
/// </summary>
/// <param name="Id">The reply/question identifier, present when IBKR asks a follow-up question.</param>
/// <param name="Message">Question messages from IBKR, present when confirmation is needed.</param>
/// <param name="IsSuppressed">Whether the question has been suppressed.</param>
/// <param name="MessageIds">Message IDs associated with the question.</param>
/// <param name="OrderId">The order identifier, present on successful placement.</param>
/// <param name="OrderStatus">The order status, present on successful placement.</param>
/// <param name="LocalOrderId">The customer order ID (<c>cOID</c>) echoed back on the parent of a bracket/OCA group.</param>
/// <param name="OcaGroupId">The OCA group identifier assigned by IBKR for a one-cancels-all group.</param>
/// <param name="EncryptMessage">IBKR returns "1" to indicate the acknowledgement message was encrypted. Informational only.</param>
[ExcludeFromCodeCoverage]
public record OrderSubmissionResponse(
    // WIR-4: IBKR unquotes numeric identifiers on its other order surfaces (trades REST, sor/str
    // frames); normalize a numeric id/order_id to a string so a transmitted order never surfaces as a
    // deserialization failure indistinguishable from a refusal.
    [property: JsonPropertyName("id")]
    [property: JsonConverter(typeof(FlexibleStringJsonConverter))]
    string? Id,
    [property: JsonPropertyName("message")] List<string>? Message,
    [property: JsonPropertyName("isSuppressed")] bool? IsSuppressed,
    [property: JsonPropertyName("messageIds")] List<string>? MessageIds,
    [property: JsonPropertyName("order_id")]
    [property: JsonConverter(typeof(FlexibleStringJsonConverter))]
    string? OrderId,
    [property: JsonPropertyName("order_status")] string? OrderStatus,
    [property: JsonPropertyName("local_order_id")] string? LocalOrderId = null,
    [property: JsonPropertyName("oca_group_id")] string? OcaGroupId = null,
    [property: JsonPropertyName("encrypt_message")] string? EncryptMessage = null)
{
    /// <summary>
    /// Reject reason present when IBKR returns a 200 error element in place of an order id or a
    /// question — e.g. an array-wrapped <c>[{"error":"…"}]</c> reject that bypasses the bare-object
    /// hidden-error detection (AMB-4). Null on the success and question shapes.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }

    /// <summary>
    /// Rejection detail on an <c>advancedOrderReject</c>-style row (RPD-04). Documented + observed
    /// in-context by IBKR (DOC-01); when present on a leg <see cref="Client.OrderOperations.ClassifyGroupResponses"/>
    /// classifies as <see cref="IbkrConduit.Errors.IbkrOrderRejectedError"/> via the terminal-status path,
    /// it enriches that error's message instead of the generic fallback. Null when absent.
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>
    /// Warning/rejection detail observed on the wire in the orders context (RPD-04) — undocumented there;
    /// the same field name is documented elsewhere (DOC-01, DOC-03) on an unrelated FYI/alert-creation
    /// endpoint as always returning null, a distinct context this field must not be confused with. Null
    /// when absent.
    /// </summary>
    [JsonPropertyName("warning_message")]
    public string? WarningMessage { get; init; }

    /// <summary>
    /// The selectable answer options for a confirmation question (RPD-04), e.g. <c>["Yes", "No"]</c> — the
    /// values <see cref="Client.IOrderOperations.DismissNotificationAsync"/>'s <c>text</c> parameter
    /// accepts. Documented only in a DOC-03 worked example, absent from DOC-01's formal schema. Null when
    /// absent.
    /// </summary>
    [JsonPropertyName("messageOptions")]
    public List<string>? MessageOptions { get; init; }

    /// <summary>
    /// Parent-order linkage observed on a child leg's submission-response row (RPD-04, ADR-0008) — links
    /// back to the parent's <c>order_id</c>. Undocumented anywhere; distinct from both the request-side
    /// <see cref="OrderRequest.ParentId"/>/<c>cOID</c> string and from <see cref="LiveOrder.ParentId"/>
    /// (RPD-02's response-side integer linkage on the live-orders endpoint) — a third, separate field
    /// carrying similar-but-not-identical linkage information at a different point in the flow. Kept as a
    /// tolerant string (WIR-4) since IBKR's order identifiers are inconsistently quoted across surfaces.
    /// Null when absent.
    /// </summary>
    [JsonPropertyName("parent_order_id")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? ParentOrderId { get; init; }
}

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
/// <param name="Snapshot">
/// IBKR's priming indicator for <c>/iserver/account/orders</c>: the first call returns
/// <c>false</c> and must be repeated to obtain the full set. Null when absent.
/// </param>
[ExcludeFromCodeCoverage]
public record OrdersResponse(
    [property: JsonPropertyName("orders")] List<LiveOrder>? Orders,
    [property: JsonPropertyName("snapshot")] bool? Snapshot = null);

/// <summary>
/// The public result of <see cref="Client.IOrderOperations.GetLiveOrdersAsync"/>: the live orders
/// together with IBKR's priming indicator (design doc §10.6, findings GAP1-1/GAP1-2).
/// </summary>
/// <param name="Orders">
/// The live orders returned by this call. Empty means "this response carried no orders" — which is
/// only an authoritative "no live orders" fact when <paramref name="IsSnapshot"/> is <c>true</c>.
/// </param>
/// <param name="IsSnapshot">
/// IBKR's <c>snapshot</c> flag for <c>/iserver/account/orders</c>. <c>true</c> means the order cache
/// is primed and the set is authoritative. <c>false</c> means the cache is NOT yet primed — an empty
/// <paramref name="Orders"/> is an unprimed artifact, NOT evidence that no orders exist. A consumer
/// that must know whether orders exist (e.g. before an absence-driven repair) must treat
/// <c>IsSnapshot == false</c> as "unknown — call again", never as "no orders". Absent on the wire
/// maps to <c>false</c> (unprimed) — never a fabricated <c>true</c>.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record LiveOrdersSnapshot(IReadOnlyList<LiveOrder> Orders, bool IsSnapshot);

#pragma warning disable CA1711 // ConidEx is the IBKR API field name — suffix is not a .NET type convention issue
/// <summary>
/// A live order in the current session.
/// </summary>
/// <param name="Account">The account identifier.</param>
/// <param name="Conid">The contract identifier.</param>
/// <param name="ConidEx">The extended contract identifier string.</param>
/// <param name="OrderId">The order identifier.</param>
/// <param name="Ticker">The ticker symbol.</param>
/// <param name="SecType">The security type (e.g., "STK", "OPT").</param>
/// <param name="ListingExchange">The listing exchange.</param>
/// <param name="Side">The order side.</param>
/// <param name="Status">The order status.</param>
/// <param name="OrderCcpStatus">The CCP order status.</param>
/// <param name="OrderType">The order type.</param>
/// <param name="FilledQuantity">The filled quantity. Null when IBKR omits it or sends an empty value — never a fabricated 0.</param>
/// <param name="RemainingQuantity">The remaining quantity. Null when IBKR omits it or sends an empty value — never a fabricated 0.</param>
/// <param name="TotalSize">The total order size. Null when IBKR omits it or sends an empty value — never a fabricated 0.</param>
/// <param name="CompanyName">The company name.</param>
/// <param name="AvgPrice">The average fill price as a string.</param>
/// <param name="TimeInForce">The time in force.</param>
/// <param name="OrderDescription">The order description.</param>
/// <param name="Price">The working limit price; null for market orders (IBKR sends an empty string).</param>
[ExcludeFromCodeCoverage]
public record LiveOrder(
    [property: JsonPropertyName("account")] string? Account,
    [property: JsonPropertyName("conid")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    int Conid,
    [property: JsonPropertyName("conidex")] string? ConidEx,
    [property: JsonPropertyName("orderId")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    int OrderId,
    [property: JsonPropertyName("ticker")] string? Ticker,
    [property: JsonPropertyName("secType")] string? SecType,
    [property: JsonPropertyName("listingExchange")] string? ListingExchange,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("order_ccp_status")] string? OrderCcpStatus,
    [property: JsonPropertyName("orderType")] string? OrderType,
    // No [JsonNumberHandling(AllowReadingFromString)] on the quantity fields: they are nullable and
    // wire-optional, and IBKR can send an empty string; the shared empty-tolerant number converters
    // map ""/omitted -> null (AllowReadingFromString would instead throw on the empty string).
    [property: JsonPropertyName("filledQuantity")] decimal? FilledQuantity,
    [property: JsonPropertyName("remainingQuantity")] decimal? RemainingQuantity,
    [property: JsonPropertyName("totalSize")] decimal? TotalSize,
    [property: JsonPropertyName("companyName")] string? CompanyName,
    [property: JsonPropertyName("avgPrice")] string? AvgPrice,
    [property: JsonPropertyName("timeInForce")] string? TimeInForce,
    [property: JsonPropertyName("orderDesc")] string? OrderDescription,
    // No [JsonNumberHandling(AllowReadingFromString)] here: market orders send price="",
    // which the shared empty-tolerant number converters map to null (AllowReadingFromString
    // would throw on the empty string).
    [property: JsonPropertyName("price")] decimal? Price = null)
{
    /// <summary>
    /// User-defined order reference echoing the <c>cOID</c> supplied at placement
    /// (IBKR <c>order_ref</c>). Present only when a <see cref="OrderRequest.CustomerOrderId"/>
    /// was set on the order; otherwise null.
    /// </summary>
    [JsonPropertyName("order_ref")]
    public string? OrderRef { get; init; }

    /// <summary>
    /// The reason the order was cancelled or rejected by the system (IBKR
    /// <c>order_cancellation_by_system_reason</c>). Documented by IBKR (DOC-01): "Only present
    /// for Cancelled orders. Provides the reason for order to have been cancelled or rejected
    /// by the system." Null for every other status.
    /// </summary>
    [JsonPropertyName("order_cancellation_by_system_reason")]
    public string? OrderCancellationBySystemReason { get; init; }

    /// <summary>
    /// Parent-order linkage for a bracket/OCA <em>child</em> leg (IBKR <c>parentId</c>). Present only
    /// on child legs; the parent order itself carries no <c>parentId</c> (null here). This is the
    /// <em>response</em>-side linkage value — the parent's server-assigned <see cref="OrderId"/> (an
    /// integer) — NOT the request-side <see cref="OrderRequest.ParentId"/>/<c>cOID</c> string the child
    /// was submitted with: a deliberate request/response type asymmetry. Resolve child→parent by
    /// matching this value against another order's <see cref="OrderId"/>, keying on the response-side
    /// integer (never the request-side cOID). Typed <c>int?</c> and tolerant of the integer-valued
    /// string form IBKR uses on its other order surfaces (observed on the wire as an unquoted JSON
    /// integer). Null when absent (a non-child order). Documented only as a request-side field by every
    /// IBKR source (DOC-01/03/05); its presence on the live-orders response is verified by recording +
    /// live probe (RPD-02), not by any doc.
    /// </summary>
    [JsonPropertyName("parentId")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? ParentId { get; init; }

    /// <summary>
    /// One-cancels-all group identifier (IBKR <c>ocaGroupId</c>) linking legs that cancel together.
    /// Kept as the raw string exactly as IBKR sends it. It appears in TWO observed shapes that grouping
    /// logic MUST both handle — never assume the <c>oco-</c> prefix, and never strip or reinterpret it:
    /// <list type="bullet">
    /// <item>an explicit OCA group (legs submitted with <see cref="OrderRequest.IsSingleGroup"/>) carries
    /// a prefixed <c>"oco-&lt;orderId&gt;"</c> string;</item>
    /// <item>a bracket's own take-profit/stop exit legs carry a <em>bare</em> integer-valued string equal
    /// to the parent's <see cref="OrderId"/> — no <c>oco-</c> prefix.</item>
    /// </list>
    /// Null when the order belongs to no OCA group. Documented by <em>no</em> IBKR source for the CP Web
    /// API (DOC-08's TWS-API OCA uses a differently-named <c>ocaGroup</c> on a different surface); its
    /// presence and both shapes are verified by recording + live probe (RPD-02).
    /// </summary>
    [JsonPropertyName("ocaGroupId")]
    public string? OcaGroupId { get; init; }

    /// <summary>Additional unmapped properties from the API response.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// A completed trade from the IBKR API (<c>/iserver/account/trades</c>).
/// </summary>
/// <param name="ExecutionId">The execution identifier.</param>
/// <param name="Conid">The contract identifier.</param>
/// <param name="Symbol">The ticker symbol.</param>
/// <param name="Side">The trade side (Buy or Sell).</param>
/// <param name="Size">The trade size (quantity). Null when IBKR omits it or sends an empty value — never a fabricated 0.</param>
/// <param name="Price">The execution price. Null when IBKR omits it or sends an empty value — never a fabricated 0.</param>
/// <param name="OrderRef">The user-defined order reference (cOID). Null when absent — e.g. a manual/TWS trade placed without a cOID.</param>
/// <param name="Submitter">The username that submitted the order. Null when IBKR omits it.</param>
[ExcludeFromCodeCoverage]
public record Trade(
    [property: JsonPropertyName("execution_id")] string ExecutionId,
    [property: JsonPropertyName("conid")] int Conid,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("size")] decimal? Size,
    [property: JsonPropertyName("price")] decimal? Price,
    [property: JsonPropertyName("order_ref")] string? OrderRef,
    [property: JsonPropertyName("submitter")] string? Submitter)
{
    /// <summary>Whether tax optimizer is supported for the order. IBKR sends "1"/"0"; parsed to bool.</summary>
    [JsonPropertyName("supports_tax_opt")]
    [JsonConverter(typeof(FlexibleBoolJsonConverter))]
    public bool? SupportsTaxOpt { get; init; }

    /// <summary>Full order description (side, size, symbol, order type, price, TIF).</summary>
    [JsonPropertyName("order_description")]
    public string? OrderDescription { get; init; }

    /// <summary>Trade time in UTC, formatted "YYYYMMDD-HH:mm:ss". Kept raw.</summary>
    [JsonPropertyName("trade_time")]
    public string? TradeTime { get; init; }

    /// <summary>Trade time in epoch milliseconds.</summary>
    [JsonPropertyName("trade_time_r")]
    public long? TradeTimeR { get; init; }

    /// <summary>The exchange the order was executed on.</summary>
    [JsonPropertyName("exchange")]
    public string? Exchange { get; init; }

    /// <summary>Commission cost for the trade. IBKR sends a quoted value; parsed to decimal. Null when omitted or empty.</summary>
    [JsonPropertyName("commission")]
    public decimal? Commission { get; init; }

    /// <summary>Total net cost of the order. Null when omitted or empty.</summary>
    [JsonPropertyName("net_amount")]
    public decimal? NetAmount { get; init; }

    /// <summary>The account identifier.</summary>
    [JsonPropertyName("account")]
    public string? Account { get; init; }

    /// <summary>The account code.</summary>
    [JsonPropertyName("accountCode")]
    public string? AccountCode { get; init; }

    /// <summary>The long name of the contract's company.</summary>
    [JsonPropertyName("company_name")]
    public string? CompanyName { get; init; }

    /// <summary>The local symbol of the order/contract.</summary>
    [JsonPropertyName("contract_description_1")]
    public string? ContractDescription1 { get; init; }

    /// <summary>The security type of the contract (e.g., STK, OPT).</summary>
    [JsonPropertyName("sec_type")]
    public string? SecType { get; init; }

    /// <summary>The primary listing exchange of the contract (distinct from the execution exchange).</summary>
    [JsonPropertyName("listing_exchange")]
    public string? ListingExchange { get; init; }

    // CA1711 (the "Ex" suffix on ConidEx) is already disabled file-wide above; no local pragma needed.
    /// <summary>The contract identifier of the order (extended form).</summary>
    [JsonPropertyName("conidEx")]
    public string? ConidEx { get; init; }

    /// <summary>The clearing firm identifier (e.g., "IB").</summary>
    [JsonPropertyName("clearing_id")]
    public string? ClearingId { get; init; }

    /// <summary>The clearing firm name.</summary>
    [JsonPropertyName("clearing_name")]
    public string? ClearingName { get; init; }

    /// <summary>Whether the order was part of an account liquidation. IBKR sends "1"/"0"; parsed to bool.</summary>
    [JsonPropertyName("liquidation_trade")]
    [JsonConverter(typeof(FlexibleBoolJsonConverter))]
    public bool? LiquidationTrade { get; init; }

    /// <summary>Whether the order was part of event trading. IBKR sends "1"/"0"; parsed to bool.</summary>
    [JsonPropertyName("is_event_trading")]
    [JsonConverter(typeof(FlexibleBoolJsonConverter))]
    public bool? IsEventTrading { get; init; }

    /// <summary>
    /// The IBKR order id this trade belongs to. Returned by the endpoint though absent from the
    /// documented schema; IBKR sends it as a JSON number, normalized to a string via
    /// <see cref="FlexibleStringJsonConverter"/> so it correlates with the order streams.
    /// </summary>
    [JsonPropertyName("order_id")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? OrderId { get; init; }

    /// <summary>Resulting total position size in the contract after this trade. Null when omitted or empty.</summary>
    [JsonPropertyName("position")]
    public decimal? Position { get; init; }

    /// <summary>Allocation account name for the trade.</summary>
    [JsonPropertyName("account_allocation_name")]
    public string? AccountAllocationName { get; init; }

    /// <summary>Additional data not mapped to known properties.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

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
/// <param name="OrderType">
/// The order type. Observed submission-to-read-side name mapping (1-sample live probe, 2026-07-07):
/// "LOC"→"LIMITONCLOSE", "MOC"→"MARKETONCLOSE", "REL"→"RELATIVE"; "MIT" and "LIT" unchanged.
/// </param>
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
