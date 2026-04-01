# Orders API Completion

**Date:** 2026-04-01
**Status:** Approved
**Goal:** Add the 3 missing order endpoints: modify order, what-if preview, and single order status.

---

## Scope

Complete the Orders section of the IBKR API coverage:

1. `POST /iserver/account/{accountId}/order/{orderId}` — modify an existing order
2. `POST /iserver/account/{accountId}/orders/whatif` — commission/margin preview without placing
3. `GET /iserver/account/order/status/{orderId}` — detailed status for a single order

---

## Task 1: Modify Order

### Refit Interface Addition (IIbkrOrderApi)

```csharp
[Post("/v1/api/iserver/account/{accountId}/order/{orderId}")]
Task<List<OrderSubmissionResponse>> ModifyOrderAsync(
    string accountId, string orderId, [Body] OrdersPayload orders,
    CancellationToken cancellationToken = default);
```

### Operations Interface Addition (IOrderOperations)

```csharp
Task<OrderResult> ModifyOrderAsync(
    string accountId, string orderId, OrderRequest order,
    CancellationToken cancellationToken = default);
```

### OrderOperations Implementation

Reuses the existing question/reply loop from `PlaceOrderAsync`:
1. Acquire per-account semaphore (same `_accountLocks` dictionary)
2. Convert `OrderRequest` to `OrderWireModel`, wrap in `OrdersPayload`
3. Call `_orderApi.ModifyOrderAsync(accountId, orderId, payload)`
4. Enter question/reply loop (max 20 iterations, auto-confirm with warning log)
5. Return `OrderResult`

The question/reply loop logic should be extracted into a private helper method shared by both `PlaceOrderAsync` and `ModifyOrderAsync` to avoid duplication.

---

## Task 2: What-If / Commission Preview

### Refit Interface Addition (IIbkrOrderApi)

```csharp
[Post("/v1/api/iserver/account/{accountId}/orders/whatif")]
Task<WhatIfResponse> WhatIfOrderAsync(
    string accountId, [Body] OrdersPayload orders,
    CancellationToken cancellationToken = default);
```

### Response Model (IIbkrOrderApiModels.cs)

```csharp
public record WhatIfResponse(
    [property: JsonPropertyName("amount")] WhatIfAmount? Amount,
    [property: JsonPropertyName("equity")] WhatIfEquity? Equity,
    [property: JsonPropertyName("initial")] WhatIfMargin? Initial,
    [property: JsonPropertyName("maintenance")] WhatIfMargin? Maintenance,
    [property: JsonPropertyName("warn")] string? Warning,
    [property: JsonPropertyName("error")] string? Error)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

public record WhatIfAmount(
    [property: JsonPropertyName("amount")] string? Amount,
    [property: JsonPropertyName("commission")] string? Commission,
    [property: JsonPropertyName("total")] string? Total)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

public record WhatIfEquity(
    [property: JsonPropertyName("current")] string? Current,
    [property: JsonPropertyName("change")] string? Change,
    [property: JsonPropertyName("after")] string? After)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

public record WhatIfMargin(
    [property: JsonPropertyName("current")] string? Current,
    [property: JsonPropertyName("change")] string? Change,
    [property: JsonPropertyName("after")] string? After)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
```

### Operations Interface Addition (IOrderOperations)

```csharp
Task<WhatIfResponse> WhatIfOrderAsync(
    string accountId, OrderRequest order,
    CancellationToken cancellationToken = default);
```

### OrderOperations Implementation

Simple pass-through — no question/reply flow, no semaphore needed:
1. Convert `OrderRequest` to `OrderWireModel`, wrap in `OrdersPayload`
2. Call `_orderApi.WhatIfOrderAsync(accountId, payload)`
3. Return `WhatIfResponse`

---

## Task 3: Single Order Status

### Refit Interface Addition (IIbkrOrderApi)

```csharp
[Get("/v1/api/iserver/account/order/status/{orderId}")]
Task<OrderStatus> GetOrderStatusAsync(
    string orderId, CancellationToken cancellationToken = default);
```

### Response Model (IIbkrOrderApiModels.cs)

```csharp
public record OrderStatus(
    [property: JsonPropertyName("sub_type")] string? SubType,
    [property: JsonPropertyName("request_id")] string? RequestId,
    [property: JsonPropertyName("order_id")] int OrderId,
    [property: JsonPropertyName("conidex")] string? ConidEx,
    [property: JsonPropertyName("conid")] int Conid,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("contract_description_1")] string? ContractDescription,
    [property: JsonPropertyName("listing_exchange")] string? ListingExchange,
    [property: JsonPropertyName("is_event_trading")] string? IsEventTrading,
    [property: JsonPropertyName("order_desc")] string? OrderDescription,
    [property: JsonPropertyName("order_status")] string Status,
    [property: JsonPropertyName("order_type")] string? OrderType,
    [property: JsonPropertyName("size")] decimal? Size,
    [property: JsonPropertyName("fill_price")] decimal? FillPrice,
    [property: JsonPropertyName("filled_quantity")] decimal? FilledQuantity,
    [property: JsonPropertyName("remaining_quantity")] decimal? RemainingQuantity,
    [property: JsonPropertyName("avg_fill_price")] decimal? AvgFillPrice,
    [property: JsonPropertyName("last_fill_price")] decimal? LastFillPrice,
    [property: JsonPropertyName("total_size")] decimal? TotalSize,
    [property: JsonPropertyName("total_cash_size")] decimal? TotalCashSize,
    [property: JsonPropertyName("price")] decimal? Price,
    [property: JsonPropertyName("tif")] string? Tif,
    [property: JsonPropertyName("bg_color")] string? BgColor,
    [property: JsonPropertyName("fg_color")] string? FgColor,
    [property: JsonPropertyName("order_not_editable")] bool? OrderNotEditable,
    [property: JsonPropertyName("editable_fields")] string? EditableFields,
    [property: JsonPropertyName("cannot_cancel_order")] bool? CannotCancelOrder)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
```

### Operations Interface Addition (IOrderOperations)

```csharp
Task<OrderStatus> GetOrderStatusAsync(
    string orderId, CancellationToken cancellationToken = default);
```

### OrderOperations Implementation

Simple pass-through:
1. Call `_orderApi.GetOrderStatusAsync(orderId)`
2. Return `OrderStatus`

---

## Refactoring: Extract Question/Reply Loop

The question/reply loop currently lives inline in `PlaceOrderAsync`. Extract it to a private helper:

```csharp
private async Task<OrderResult> HandleQuestionReplyLoopAsync(
    List<OrderSubmissionResponse> responses, CancellationToken cancellationToken)
```

Both `PlaceOrderAsync` and `ModifyOrderAsync` call this after the initial POST.

---

## Files Modified

```
src/IbkrConduit/Orders/IIbkrOrderApi.cs — 3 new methods
src/IbkrConduit/Orders/IIbkrOrderApiModels.cs — WhatIfResponse, OrderStatus records
src/IbkrConduit/Client/IOrderOperations.cs — 3 new methods
src/IbkrConduit/Client/OrderOperations.cs — implementations + extracted helper
```

## New Test Files

```
tests/IbkrConduit.Tests.Unit/Orders/OrderOperationsModifyTests.cs
tests/IbkrConduit.Tests.Unit/Orders/OrderOperationsWhatIfTests.cs
tests/IbkrConduit.Tests.Unit/Orders/OrderOperationsStatusTests.cs
```
