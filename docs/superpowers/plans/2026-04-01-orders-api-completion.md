# Orders API Completion — Implementation Plan

**Date:** 2026-04-01
**Goal:** Add the 3 missing order endpoints (modify order, what-if preview, single order status) and extract the question/reply loop into a shared helper.
**Architecture:** Refit interfaces + thin `OrderOperations` orchestration layer with per-account semaphore serialization.
**Tech Stack:** .NET 9, Refit, xUnit v3, Shouldly, System.Text.Json

---

## Step 0: Commit spec and audit docs

```bash
git add docs/endpoint-coverage-audit.md docs/superpowers/specs/2026-04-01-orders-api-completion-design.md
git commit -m "docs: add orders API completion spec and endpoint coverage audit"
```

---

## Step 1: Refactor — Extract `HandleQuestionReplyLoopAsync`

### 1a. RED — Write failing test verifying extraction works

No new tests needed. The existing `PlaceOrderAsync` tests cover the question/reply loop. We refactor the implementation and confirm all existing tests still pass.

### 1b. GREEN — Extract the helper and verify existing tests pass

**File:** `src/IbkrConduit/Client/OrderOperations.cs`

Extract lines 73–106 from `PlaceOrderAsync` into:

```csharp
private async Task<OrderResult> HandleQuestionReplyLoopAsync(
    List<OrderSubmissionResponse> responses, CancellationToken cancellationToken)
{
    var response = responses[0];
    for (var i = 0; i < _maxReplyIterations; i++)
    {
        if (response.OrderId is not null)
        {
            return new OrderResult(response.OrderId, response.OrderStatus ?? string.Empty);
        }

        if (response.Message is not null && response.Id is not null)
        {
            _questionCount.Add(1);
            var messageText = string.Join("; ", response.Message);
            LogOrderQuestionAutoConfirmed(messageText);

            var replyResponses = await _orderApi.ReplyAsync(
                response.Id, new ReplyRequest(true), cancellationToken);
            response = replyResponses[0];
        }
        else
        {
            throw new InvalidOperationException(
                "Unexpected order submission response: no order ID and no question message.");
        }
    }

    throw new InvalidOperationException(
        $"Order question/reply loop exceeded maximum of {_maxReplyIterations} iterations.");
}
```

`PlaceOrderAsync` calls `HandleQuestionReplyLoopAsync` after the initial POST, records metrics, and returns the result.

```bash
dotnet test --configuration Release
```

### 1c. COMMIT

```
refactor: extract HandleQuestionReplyLoopAsync from PlaceOrderAsync
```

---

## Step 2: Add Refit methods and response models

### 2a. Add response models to `IIbkrOrderApiModels.cs`

**WhatIfResponse**, **WhatIfAmount**, **WhatIfEquity**, **WhatIfMargin**, **OrderStatus** — all immutable records with `[ExcludeFromCodeCoverage]` and `[JsonExtensionData]`.

### 2b. Add Refit methods to `IIbkrOrderApi.cs`

```csharp
[Post("/v1/api/iserver/account/{accountId}/order/{orderId}")]
Task<List<OrderSubmissionResponse>> ModifyOrderAsync(
    string accountId, string orderId, [Body] OrdersPayload orders,
    CancellationToken cancellationToken = default);

[Post("/v1/api/iserver/account/{accountId}/orders/whatif")]
Task<WhatIfResponse> WhatIfOrderAsync(
    string accountId, [Body] OrdersPayload orders,
    CancellationToken cancellationToken = default);

[Get("/v1/api/iserver/account/order/status/{orderId}")]
Task<OrderStatus> GetOrderStatusAsync(
    string orderId, CancellationToken cancellationToken = default);
```

### 2c. Add interface methods to `IOrderOperations.cs`

```csharp
Task<OrderResult> ModifyOrderAsync(
    string accountId, string orderId, OrderRequest order,
    CancellationToken cancellationToken = default);

Task<WhatIfResponse> WhatIfOrderAsync(
    string accountId, OrderRequest order,
    CancellationToken cancellationToken = default);

Task<OrderStatus> GetOrderStatusAsync(
    string orderId, CancellationToken cancellationToken = default);
```

### 2d. Update FakeOrderApi in tests to implement new interface methods (throw NotImplementedException initially)

```bash
dotnet build --configuration Release
```

### 2e. COMMIT

```
feat: add Refit methods and response models for modify, what-if, and order status
```

---

## Step 3: ModifyOrder — TDD

### 3a. RED — Write failing tests

**File:** `tests/IbkrConduit.Tests.Unit/Orders/OrderOperationsModifyTests.cs`

Tests:
1. `ModifyOrderAsync_DirectConfirmation_ReturnsOrderResult` — modify returns immediately with order ID
2. `ModifyOrderAsync_WithQuestion_AutoConfirmsAndReturnsOrderResult` — modify triggers question/reply
3. `ModifyOrderAsync_ConvertsOrderRequestToWireModel` — verify wire model conversion
4. `ModifyOrderAsync_SerializesPerAccount` — verify per-account semaphore

```bash
dotnet test --configuration Release  # EXPECT FAILURES (not implemented)
```

### 3b. GREEN — Implement `ModifyOrderAsync` in `OrderOperations.cs`

```csharp
public async Task<OrderResult> ModifyOrderAsync(
    string accountId, string orderId, OrderRequest order,
    CancellationToken cancellationToken = default)
{
    using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.Modify");
    activity?.SetTag(LogFields.AccountId, accountId);
    activity?.SetTag(LogFields.OrderId, orderId);
    activity?.SetTag(LogFields.Conid, order.Conid);
    activity?.SetTag(LogFields.Side, order.Side);
    activity?.SetTag(LogFields.OrderType, order.OrderType);

    var semaphore = _accountLocks.GetOrAdd(accountId, _ => new SemaphoreSlim(1, 1));
    await semaphore.WaitAsync(cancellationToken);
    var sw = Stopwatch.StartNew();
    try
    {
        var wireModel = new OrderWireModel(
            order.Conid, order.Side, order.Quantity, order.OrderType,
            order.Price, order.AuxPrice, order.Tif, order.ManualIndicator);

        var payload = new OrdersPayload([wireModel]);
        var responses = await _orderApi.ModifyOrderAsync(accountId, orderId, payload, cancellationToken);
        var result = await HandleQuestionReplyLoopAsync(responses, cancellationToken);

        _submissionDuration.Record(sw.Elapsed.TotalMilliseconds);
        _submissionCount.Add(1,
            new KeyValuePair<string, object?>(LogFields.Side, order.Side),
            new KeyValuePair<string, object?>(LogFields.OrderType, order.OrderType));
        return result;
    }
    finally
    {
        semaphore.Release();
    }
}
```

```bash
dotnet test --configuration Release  # ALL PASS
```

### 3c. COMMIT

```
feat: implement ModifyOrderAsync with question/reply loop
```

---

## Step 4: WhatIf — TDD

### 4a. RED — Write failing tests

**File:** `tests/IbkrConduit.Tests.Unit/Orders/OrderOperationsWhatIfTests.cs`

Tests:
1. `WhatIfOrderAsync_ReturnsWhatIfResponse` — pass-through returns response
2. `WhatIfOrderAsync_ConvertsOrderRequestToWireModel` — verify conversion

```bash
dotnet test --configuration Release  # EXPECT FAILURES
```

### 4b. GREEN — Implement `WhatIfOrderAsync` in `OrderOperations.cs`

```csharp
public async Task<WhatIfResponse> WhatIfOrderAsync(
    string accountId, OrderRequest order,
    CancellationToken cancellationToken = default)
{
    using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.WhatIf");
    activity?.SetTag(LogFields.AccountId, accountId);
    activity?.SetTag(LogFields.Conid, order.Conid);

    var wireModel = new OrderWireModel(
        order.Conid, order.Side, order.Quantity, order.OrderType,
        order.Price, order.AuxPrice, order.Tif, order.ManualIndicator);

    var payload = new OrdersPayload([wireModel]);
    return await _orderApi.WhatIfOrderAsync(accountId, payload, cancellationToken);
}
```

```bash
dotnet test --configuration Release  # ALL PASS
```

### 4c. COMMIT

```
feat: implement WhatIfOrderAsync pass-through
```

---

## Step 5: OrderStatus — TDD

### 5a. RED — Write failing tests

**File:** `tests/IbkrConduit.Tests.Unit/Orders/OrderOperationsStatusTests.cs`

Tests:
1. `GetOrderStatusAsync_ReturnsOrderStatus` — pass-through returns response

```bash
dotnet test --configuration Release  # EXPECT FAILURES
```

### 5b. GREEN — Implement `GetOrderStatusAsync` in `OrderOperations.cs`

```csharp
public async Task<OrderStatus> GetOrderStatusAsync(
    string orderId, CancellationToken cancellationToken = default)
{
    using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.GetStatus");
    activity?.SetTag(LogFields.OrderId, orderId);
    return await _orderApi.GetOrderStatusAsync(orderId, cancellationToken);
}
```

```bash
dotnet test --configuration Release  # ALL PASS
```

### 5c. COMMIT

```
feat: implement GetOrderStatusAsync pass-through
```

---

## Step 6: Final verification

```bash
dotnet build --configuration Release   # zero warnings
dotnet test --configuration Release    # all pass
dotnet format --verify-no-changes      # clean
```
