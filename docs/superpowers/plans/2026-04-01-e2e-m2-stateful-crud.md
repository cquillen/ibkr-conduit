# E2E Test Suite M2: Stateful CRUD Scenarios — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix Reply deserialization bug and implement 3 stateful E2E scenarios covering 16 endpoints (Orders, Alerts, Watchlists) with cleanup.

**Architecture:** E2E tests inherit E2eScenarioBase, use full DI pipeline, create test entities with E2E-{guid} prefix, clean up in finally blocks. Reply endpoint fix uses flexible deserialization to handle IBKR's inconsistent response format.

**Tech Stack:** xUnit v3, Shouldly, Refit, System.Text.Json

---

## Branch

`feat/e2e-m2-stateful-crud`

---

## Task 0: Fix Reply Endpoint Deserialization Bug

**Problem:** The Refit interface `IIbkrOrderApi.ReplyAsync` declares `Task<List<OrderSubmissionResponse>>` as its return type, but the `/iserver/reply/{replyId}` endpoint sometimes returns a non-array JSON response (e.g., `{"order_id":"123","order_status":"Submitted"}` or `{"confirmed":true}`). When IBKR returns a bare JSON object instead of an array, Refit's default `System.Text.Json` deserialization throws a `JsonException` because it expects a JSON array `[...]`.

**Root cause:** `IIbkrOrderApi.ReplyAsync` at `/workspace/ibkr-conduit/src/IbkrConduit/Orders/IIbkrOrderApi.cs:21` returns `Task<List<OrderSubmissionResponse>>`. The consumer `OrderOperations.HandleQuestionReplyLoopAsync` at `/workspace/ibkr-conduit/src/IbkrConduit/Client/OrderOperations.cs:189` indexes into `responses[0]`.

**Approach:** Change the Refit return type for `ReplyAsync` to `Task<ApiResponse<string>>` (raw string body), then deserialize manually in `OrderOperations` using a helper that tries array-first, then wraps a single object in a list. This is the safest option because it avoids custom converters affecting other endpoints and handles any response shape IBKR sends.

Alternative considered: A custom `JsonConverter<List<OrderSubmissionResponse>>` that handles both array and non-array. This is cleaner at the Refit level but risks side effects on `PlaceOrderAsync` and `ModifyOrderAsync` which also return `List<OrderSubmissionResponse>`. We avoid this.

### Steps

- [ ] **0.1 — Write failing unit test for non-array Reply response**
  - File: `/workspace/ibkr-conduit/tests/IbkrConduit.Tests.Unit/Orders/OrderOperationsReplyTests.cs`
  - Test name: `PlaceOrderAsync_ReplyReturnsNonArray_DeserializesCorrectly`
  - Set up WireMock: POST `/iserver/account/{id}/orders` returns a question response (array with `id` and `message` fields). POST `/iserver/reply/{replyId}` returns a **bare JSON object** (NOT an array): `{"order_id":"67890","order_status":"Submitted"}`.
  - Assert that `PlaceOrderAsync` returns `OrderResult("67890", "Submitted")`.
  - Run test — confirm it **fails** with a `JsonException` or similar deserialization error.

- [ ] **0.2 — Change ReplyAsync return type to use raw response**
  - File: `/workspace/ibkr-conduit/src/IbkrConduit/Orders/IIbkrOrderApi.cs`
  - Change line 21 from `Task<List<OrderSubmissionResponse>> ReplyAsync(...)` to `Task<IApiResponse<string>> ReplyAsync(...)`
  - This tells Refit to return the raw string body without deserializing, wrapped in `IApiResponse` for status code access.
  - Note: This requires `using Refit;` (already present).

- [ ] **0.3 — Add DeserializeReplyResponse helper in OrderOperations**
  - File: `/workspace/ibkr-conduit/src/IbkrConduit/Client/OrderOperations.cs`
  - Add a `private static List<OrderSubmissionResponse> DeserializeReplyResponse(string content)` method that:
    1. Trims the content and checks if it starts with `[`
    2. If array: deserialize as `List<OrderSubmissionResponse>` using `JsonSerializer.Deserialize`
    3. If object: deserialize as single `OrderSubmissionResponse`, wrap in a `new List<>` with one element
    4. Throw `InvalidOperationException` with the raw content if neither works
  - Uses `System.Text.Json.JsonSerializer` with `PropertyNameCaseInsensitive = true` (or rely on `JsonPropertyName` attributes already on the record).

- [ ] **0.4 — Update HandleQuestionReplyLoopAsync to use the helper**
  - File: `/workspace/ibkr-conduit/src/IbkrConduit/Client/OrderOperations.cs`
  - In `HandleQuestionReplyLoopAsync` at line 207-209, change:
    ```csharp
    var replyResponses = await _orderApi.ReplyAsync(
        response.Id, new ReplyRequest(true), cancellationToken);
    response = replyResponses[0];
    ```
    To:
    ```csharp
    var replyApiResponse = await _orderApi.ReplyAsync(
        response.Id, new ReplyRequest(true), cancellationToken);
    replyApiResponse.EnsureSuccessStatusCode();
    var replyResponses = DeserializeReplyResponse(replyApiResponse.Content!);
    response = replyResponses[0];
    ```
  - Add a log line for the raw reply content at Debug level for diagnostics.

- [ ] **0.5 — Update existing unit tests that mock ReplyAsync**
  - Files:
    - `/workspace/ibkr-conduit/tests/IbkrConduit.Tests.Unit/Orders/OrderOperationsTests.cs`
    - Any other test file that sets up `_orderApi.ReplyAsync` expectations
  - Update mock return values to match the new `IApiResponse<string>` return type. Mocks should now return a serialized JSON string wrapped in an `ApiResponse<string>`.

- [ ] **0.6 — Run all tests — verify green**
  - Run: `dotnet test --configuration Release` from repo root
  - All existing tests should pass, plus the new test from step 0.1.

- [ ] **0.7 — Run lint check**
  - Run: `dotnet format --verify-no-changes`
  - Fix any formatting issues.

---

## Task 1: Scenario 4 — Full Order Lifecycle (8 endpoints + 6 error cases)

**File:** `/workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/E2E/Scenario04_OrderLifecycleTests.cs`

**Endpoints exercised:**
1. `iserver/secdef/search` — search SPY conid (via `client.Contracts.SearchBySymbolAsync`)
2. `iserver/account/{id}/orders/whatif` — preview order (via `client.Orders.WhatIfOrderAsync`)
3. `iserver/account/{id}/orders` POST — place order (via `client.Orders.PlaceOrderAsync`)
4. `iserver/reply/{replyId}` — auto-confirm (handled internally by `OrderOperations`)
5. `iserver/account/order/status/{orderId}` — get status (via `client.Orders.GetOrderStatusAsync`)
6. `iserver/account/orders` GET — list live orders (via `client.Orders.GetLiveOrdersAsync`)
7. `iserver/account/{id}/order/{orderId}` POST — modify order (via `client.Orders.ModifyOrderAsync`)
8. `iserver/account/{id}/order/{orderId}` DELETE — cancel order (via `client.Orders.CancelOrderAsync`)
9. `iserver/account/trades` — get trades (via `client.Orders.GetTradesAsync`)

**Key model constructors (from `/workspace/ibkr-conduit/src/IbkrConduit/Orders/IIbkrOrderApiModels.cs`):**
```csharp
// OrderRequest uses init properties, not constructor params:
new OrderRequest
{
    Conid = spyConid,
    Side = "BUY",
    Quantity = 1,
    OrderType = "LMT",
    Price = 1.00m,
    Tif = "GTC",
}
```

### Steps

- [ ] **1.1 — Create the test file with class scaffolding**
  - File: `/workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/E2E/Scenario04_OrderLifecycleTests.cs`
  - Class: `Scenario04_OrderLifecycleTests` inheriting `E2eScenarioBase`
  - Attributes: `[Collection("IBKR E2E")]`, `[ExcludeFromCodeCoverage]`
  - Using directives: `IbkrConduit.Client`, `IbkrConduit.Orders`, `Refit`, `Shouldly`

- [ ] **1.2 — Implement happy-path test: OrderLifecycle_FullWorkflow**
  - Attribute: `[EnvironmentFact("IBKR_CONSUMER_KEY")]`
  - Setup: `CreateClient()`, `StartRecording("Scenario04_OrderLifecycle")`
  - Step 1: Get accounts via `client.Portfolio.GetAccountsAsync(CT)` to get `accountId`
  - Step 2: Search SPY via `client.Contracts.SearchBySymbolAsync("SPY", CT)`, capture `spyConid`
  - Step 3: What-if preview — `client.Orders.WhatIfOrderAsync(accountId, orderRequest, CT)` where order is 1 share SPY LMT $1.00 GTC. Assert response is not null (may have `Warning` but not `Error`).
  - Step 4: Place order — `client.Orders.PlaceOrderAsync(accountId, orderRequest, CT)`. Assert `result.OrderId` is not null/empty.
  - Step 5: Get order status — `client.Orders.GetOrderStatusAsync(result.OrderId, CT)`. Assert `status.Symbol` is "SPY", `status.Status` is not null.
  - Step 6: Get live orders — `client.Orders.GetLiveOrdersAsync(CT)`. Assert the list contains an order with our `OrderId`.
  - Step 7: Modify order — change price to $1.01 — `client.Orders.ModifyOrderAsync(accountId, orderId, modifiedOrder, CT)`. Assert `modifyResult.OrderId` is not null.
  - Step 8: Get order status again — verify price changed (or at least order still exists).
  - Step 9: Cancel order — `client.Orders.CancelOrderAsync(accountId, orderId, CT)`. Assert `cancelResult.Message` contains relevant text.
  - Step 10: Get live orders — verify our order no longer appears (or has status "Cancelled").
  - Step 11: Get trades — `client.Orders.GetTradesAsync(CT)`. Assert response is not null (may be empty if order never filled).
  - Finally block: `StopRecording()`, cleanup (see 1.3), `DisposeAsync()`.

- [ ] **1.3 — Implement cleanup logic in finally block**
  - In the finally block, before `DisposeAsync()`:
    ```
    try {
        var liveOrders = await client.Orders.GetLiveOrdersAsync(CT);
        foreach (var order in liveOrders where order.Symbol == "SPY" && order.Price <= 1.01m)
            await client.Orders.CancelOrderAsync(accountId, order.OrderId, CT);
    } catch { /* cleanup best-effort */ }
    ```
  - This catches any orders left open if the test fails mid-scenario.

- [ ] **1.4 — Implement error case: GetOrderStatus_NonExistentId**
  - Test name: `GetOrderStatus_NonExistentOrderId_ThrowsOrReturnsError`
  - Attribute: `[EnvironmentFact("IBKR_CONSUMER_KEY")]`
  - Call `client.Orders.GetOrderStatusAsync("000000000", CT)`
  - Wrap in try/catch for `ApiException`. Document the actual IBKR behavior per quirks process.
  - Follow same pattern as `Scenario01_AccountDiscoveryTests.GetAccountInfo_InvalidId_ThrowsApiException`.

- [ ] **1.5 — Implement error case: CancelOrder_NonExistentId**
  - Test name: `CancelOrder_NonExistentOrderId_ThrowsOrReturnsError`
  - Call `client.Orders.CancelOrderAsync(accountId, "000000000", CT)`
  - Handle `ApiException`. Document actual behavior.

- [ ] **1.6 — Implement error case: ModifyOrder_NonExistentId**
  - Test name: `ModifyOrder_NonExistentOrderId_ThrowsOrReturnsError`
  - Call `client.Orders.ModifyOrderAsync(accountId, "000000000", modifyRequest, CT)`
  - Handle `ApiException`. Document actual behavior.

- [ ] **1.7 — Implement error case: CancelOrder_AlreadyCancelled**
  - Test name: `CancelOrder_SameOrderTwice_ThrowsOrReturnsError`
  - Place a GTC limit order at $1.00, cancel it, then cancel again.
  - Handle `ApiException` on second cancel. Document actual behavior.
  - Finally block cleans up any leftover orders.

- [ ] **1.8 — Implement error case: WhatIf_InvalidConid**
  - Test name: `WhatIfOrder_InvalidConid_ThrowsOrReturnsError`
  - Call `client.Orders.WhatIfOrderAsync(accountId, orderWithConid0, CT)` where `Conid = 0`
  - Handle `ApiException` or check `WhatIfResponse.Error` for non-null. Document actual behavior.

- [ ] **1.9 — Implement error case: PlaceOrder_QuantityZero**
  - Test name: `PlaceOrder_QuantityZero_ThrowsOrReturnsError`
  - Call `client.Orders.PlaceOrderAsync(accountId, orderWithQty0, CT)` where `Quantity = 0`
  - Handle `ApiException`. Document actual behavior.

- [ ] **1.10 — Build and verify**
  - Run: `dotnet build --configuration Release`
  - Run: `dotnet test --configuration Release` (unit/integration tests without E2E)
  - Run: `dotnet format --verify-no-changes`

---

## Task 2: Scenario 6 — Alert Management (4 endpoints + 3 error cases)

**File:** `/workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/E2E/Scenario06_AlertManagementTests.cs`

**Endpoints exercised:**
1. `iserver/account/{id}/alert` POST — create alert (via `client.Alerts.CreateOrModifyAlertAsync`)
2. `iserver/account/mta` GET — list alerts (via `client.Alerts.GetAlertsAsync`)
3. `iserver/account/alert/{id}` GET — get detail (via `client.Alerts.GetAlertDetailAsync`)
4. `iserver/account/{id}/alert/{id}` DELETE — delete alert (via `client.Alerts.DeleteAlertAsync`)

**Key model constructors (from `/workspace/ibkr-conduit/src/IbkrConduit/Alerts/IIbkrAlertApiModels.cs`):**
```csharp
// CreateAlertRequest — positional record:
new CreateAlertRequest(
    OrderId: 0,                            // 0 = new alert
    AlertName: $"E2E-{guid}",
    AlertMessage: "E2E test alert",
    AlertRepeatable: 0,                    // 0 = no repeat
    OutsideRth: 1,                         // 1 = active outside RTH
    Conditions: [
        new AlertCondition(
            Type: 1,                       // 1 = price condition
            Conidex: spyConid.ToString(),
            Operator: ">=",
            TriggerMethod: "0",
            Value: "9999.00")
    ]);

// AlertSummary: OrderId is int, AlertName is string
// AlertDetail: OrderId is int, AlertName is string, Conditions is List<AlertCondition>
// DeleteAlertResponse: OrderId is int, Msg is string?
```

### Steps

- [ ] **2.1 — Create the test file with class scaffolding**
  - File: `/workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/E2E/Scenario06_AlertManagementTests.cs`
  - Class: `Scenario06_AlertManagementTests` inheriting `E2eScenarioBase`
  - Attributes: `[Collection("IBKR E2E")]`, `[ExcludeFromCodeCoverage]`
  - Using directives: `IbkrConduit.Client`, `IbkrConduit.Alerts`, `Refit`, `Shouldly`

- [ ] **2.2 — Implement happy-path test: AlertManagement_FullWorkflow**
  - Attribute: `[EnvironmentFact("IBKR_CONSUMER_KEY")]`
  - Setup: `CreateClient()`, `StartRecording("Scenario06_AlertManagement")`
  - Generate a unique identifier: `var testId = $"E2E-{Guid.NewGuid():N}"[..20]` (alert names may have length limits)
  - Step 1: Get accounts to get `accountId`
  - Step 2: Search SPY to get `spyConid` (via `client.Contracts.SearchBySymbolAsync("SPY", CT)`)
  - Step 3: Create alert — `client.Alerts.CreateOrModifyAlertAsync(accountId, createRequest, CT)`. Assert `response.OrderId > 0`. Capture `alertId = response.OrderId.ToString()`.
  - Step 4: List alerts — `client.Alerts.GetAlertsAsync(CT)`. Assert the list contains an alert with `AlertName` matching our `testId`.
  - Step 5: Get alert detail — `client.Alerts.GetAlertDetailAsync(alertId, CT)`. Assert `detail.AlertName` equals our `testId`, `detail.Conditions` is not empty.
  - Step 6: Delete alert — `client.Alerts.DeleteAlertAsync(accountId, alertId, CT)`. Assert response is not null.
  - Step 7: List alerts again — `client.Alerts.GetAlertsAsync(CT)`. Assert no alert with our `testId` in the list.
  - Finally block: cleanup (see 2.3), `StopRecording()`, `DisposeAsync()`.

- [ ] **2.3 — Implement cleanup logic in finally block**
  - In the finally block:
    ```
    try {
        var alerts = await client.Alerts.GetAlertsAsync(CT);
        foreach (var alert in alerts where alert.AlertName.StartsWith("E2E-"))
            await client.Alerts.DeleteAlertAsync(accountId, alert.OrderId.ToString(), CT);
    } catch { /* cleanup best-effort */ }
    ```

- [ ] **2.4 — Implement error case: GetAlertDetail_NonExistentId**
  - Test name: `GetAlertDetail_NonExistentAlertId_ThrowsOrReturnsError`
  - Call `client.Alerts.GetAlertDetailAsync("0", CT)`
  - Handle `ApiException`. Document actual behavior per quirks process.

- [ ] **2.5 — Implement error case: DeleteAlert_NonExistentId**
  - Test name: `DeleteAlert_NonExistentAlertId_ThrowsOrReturnsError`
  - Call `client.Alerts.DeleteAlertAsync(accountId, "0", CT)`
  - Handle `ApiException`. Document actual behavior.

- [ ] **2.6 — Implement error case: DeleteAlert_SameAlertTwice**
  - Test name: `DeleteAlert_SameAlertTwice_ThrowsOrReturnsError`
  - Create an alert, delete it, then delete again.
  - Handle `ApiException` on second delete. Document actual behavior.
  - Finally block cleans up any leftover E2E- alerts.

- [ ] **2.7 — Build and verify**
  - Run: `dotnet build --configuration Release`
  - Run: `dotnet test --configuration Release`
  - Run: `dotnet format --verify-no-changes`

---

## Task 3: Scenario 7 — Watchlist Management (4 endpoints + 3 error cases)

**File:** `/workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/E2E/Scenario07_WatchlistManagementTests.cs`

**Endpoints exercised:**
1. `iserver/watchlist` POST — create watchlist (via `client.Watchlists.CreateWatchlistAsync`)
2. `iserver/watchlists` GET — list watchlists (via `client.Watchlists.GetWatchlistsAsync`)
3. `iserver/watchlist` GET — get watchlist (via `client.Watchlists.GetWatchlistAsync`)
4. `iserver/watchlist` DELETE — delete watchlist (via `client.Watchlists.DeleteWatchlistAsync`)

**Key model constructors (from `/workspace/ibkr-conduit/src/IbkrConduit/Watchlists/IIbkrWatchlistApiModels.cs`):**
```csharp
// CreateWatchlistRequest — positional record:
new CreateWatchlistRequest(
    Id: $"E2E-{guid}",
    Rows: [
        new WatchlistRow(C: spyConid, H: "SPY"),
        new WatchlistRow(C: aaplConid, H: "AAPL"),
    ]);

// CreateWatchlistResponse: Id is string
// WatchlistSummary: Id is string, Name is string, Modified is long, Instruments is int
// WatchlistDetail: Id is string, Name is string, Rows is List<WatchlistDetailRow>
// WatchlistDetailRow: C is int, H is string, Sym is string?
// DeleteWatchlistResponse: Deleted is bool, Id is string
```

### Steps

- [ ] **3.1 — Create the test file with class scaffolding**
  - File: `/workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/E2E/Scenario07_WatchlistManagementTests.cs`
  - Class: `Scenario07_WatchlistManagementTests` inheriting `E2eScenarioBase`
  - Attributes: `[Collection("IBKR E2E")]`, `[ExcludeFromCodeCoverage]`
  - Using directives: `IbkrConduit.Client`, `IbkrConduit.Watchlists`, `Refit`, `Shouldly`

- [ ] **3.2 — Implement happy-path test: WatchlistManagement_FullWorkflow**
  - Attribute: `[EnvironmentFact("IBKR_CONSUMER_KEY")]`
  - Setup: `CreateClient()`, `StartRecording("Scenario07_WatchlistManagement")`
  - Generate a unique identifier: `var testId = $"E2E-{Guid.NewGuid():N}"[..20]`
  - Step 1: Search SPY to get `spyConid`, search AAPL to get `aaplConid`
  - Step 2: Create watchlist — `client.Watchlists.CreateWatchlistAsync(createRequest, CT)`. Assert `response.Id` is not null/empty. Capture `watchlistId = response.Id`.
  - Step 3: List watchlists — `client.Watchlists.GetWatchlistsAsync(CT)`. Assert the list contains a watchlist with `Name` matching our `testId` (note: IBKR may use `Id` as the name).
  - Step 4: Get watchlist — `client.Watchlists.GetWatchlistAsync(watchlistId, CT)`. Assert `detail.Rows` has 2 entries. Assert one row has `C == spyConid` and another has `C == aaplConid`.
  - Step 5: Delete watchlist — `client.Watchlists.DeleteWatchlistAsync(watchlistId, CT)`. Assert `response.Deleted` is true.
  - Step 6: List watchlists again — `client.Watchlists.GetWatchlistsAsync(CT)`. Assert no watchlist with our `testId`.
  - Finally block: cleanup (see 3.3), `StopRecording()`, `DisposeAsync()`.

- [ ] **3.3 — Implement cleanup logic in finally block**
  - In the finally block:
    ```
    try {
        var watchlists = await client.Watchlists.GetWatchlistsAsync(CT);
        foreach (var wl in watchlists where wl.Name.StartsWith("E2E-"))
            await client.Watchlists.DeleteWatchlistAsync(wl.Id, CT);
    } catch { /* cleanup best-effort */ }
    ```

- [ ] **3.4 — Implement error case: GetWatchlist_NonExistentId**
  - Test name: `GetWatchlist_NonExistentId_ThrowsOrReturnsError`
  - Call `client.Watchlists.GetWatchlistAsync("FAKE-ID-99999", CT)`
  - Handle `ApiException`. Document actual behavior per quirks process.

- [ ] **3.5 — Implement error case: DeleteWatchlist_NonExistentId**
  - Test name: `DeleteWatchlist_NonExistentId_ThrowsOrReturnsError`
  - Call `client.Watchlists.DeleteWatchlistAsync("FAKE-ID-99999", CT)`
  - Handle `ApiException`. Document actual behavior.

- [ ] **3.6 — Implement error case: DeleteWatchlist_SameWatchlistTwice**
  - Test name: `DeleteWatchlist_SameWatchlistTwice_ThrowsOrReturnsError`
  - Create a watchlist, delete it, then delete again.
  - Handle `ApiException` on second delete. Document actual behavior.
  - Finally block cleans up any leftover E2E- watchlists.

- [ ] **3.7 — Build and verify**
  - Run: `dotnet build --configuration Release`
  - Run: `dotnet test --configuration Release`
  - Run: `dotnet format --verify-no-changes`

---

## Task 4: Final Verification

- [ ] **4.1 — Full build**
  - Run: `dotnet build --configuration Release`
  - Verify zero warnings (TreatWarningsAsErrors is enabled).

- [ ] **4.2 — Full test suite**
  - Run: `dotnet test --configuration Release`
  - All unit tests and WireMock integration tests must pass.
  - E2E tests will be skipped (no IBKR_CONSUMER_KEY in CI) but must compile.

- [ ] **4.3 — Lint check**
  - Run: `dotnet format --verify-no-changes`
  - Fix any formatting issues.

- [ ] **4.4 — Run E2E tests against paper account (if env vars available)**
  - With IBKR credentials set, run E2E tests to validate against real IBKR:
    ```
    dotnet test --configuration Release --filter "Collection=IBKR E2E"
    ```
  - Document any quirks discovered per the verification process in the spec.

- [ ] **4.5 — Commit and create PR**
  - Branch: `feat/e2e-m2-stateful-crud`
  - Commit message: `feat: fix Reply deserialization bug and add E2E scenarios 4, 6, 7`
  - PR title: `feat: E2E M2 — stateful CRUD scenarios (orders, alerts, watchlists)`

---

## Conventions Checklist

All test files must follow these conventions:

- [ ] `[Collection("IBKR E2E")]` attribute on every test class
- [ ] `[EnvironmentFact("IBKR_CONSUMER_KEY")]` on every E2E test method
- [ ] `[ExcludeFromCodeCoverage]` on every test class
- [ ] Inherit `E2eScenarioBase`
- [ ] Use `CreateClient()` to get `(provider, client)` tuple
- [ ] Use `StartRecording("ScenarioName")` / `StopRecording()` around test logic
- [ ] Use `CT` property (from base class) as the cancellation token everywhere
- [ ] `try/finally` block wrapping the act phase, cleanup in `finally`
- [ ] `await DisposeAsync()` in the `finally` block
- [ ] Use Shouldly assertions exclusively (`ShouldBe`, `ShouldNotBeNull`, `ShouldNotBeEmpty`, etc.)
- [ ] No `Assert.*` calls — Shouldly only
- [ ] File-scoped namespaces
- [ ] XML doc comments on the test class
- [ ] Error case tests follow the try/catch `ApiException` pattern from Scenario01 (allow for IBKR returning unexpected status codes)
- [ ] Quirks documented inline per the Handling API Quirks process in the spec

---

## Dependency Graph

```
Task 0 (Reply fix) ──> Task 1 (Orders E2E, depends on Reply fix)
                   ╲
                    ╲─> Task 2 (Alerts E2E, independent)
                     ╲
                      ╲> Task 3 (Watchlists E2E, independent)

Task 1, 2, 3 ──> Task 4 (Final verification)
```

Tasks 2 and 3 are independent of each other and of Task 1 (they don't use orders). They could be parallelized. Task 1 depends on Task 0 because the order E2E test exercises the Reply endpoint. Task 4 depends on all others.

---

## Files Created/Modified Summary

| Action | Path |
|--------|------|
| Modified | `src/IbkrConduit/Orders/IIbkrOrderApi.cs` — change `ReplyAsync` return type |
| Modified | `src/IbkrConduit/Client/OrderOperations.cs` — add `DeserializeReplyResponse`, update `HandleQuestionReplyLoopAsync` |
| Modified | `tests/IbkrConduit.Tests.Unit/Orders/OrderOperationsTests.cs` — update mock for new `ReplyAsync` signature |
| Created | `tests/IbkrConduit.Tests.Unit/Orders/OrderOperationsReplyTests.cs` — unit tests for Reply deserialization fix |
| Created | `tests/IbkrConduit.Tests.Integration/E2E/Scenario04_OrderLifecycleTests.cs` — order lifecycle E2E |
| Created | `tests/IbkrConduit.Tests.Integration/E2E/Scenario06_AlertManagementTests.cs` — alert management E2E |
| Created | `tests/IbkrConduit.Tests.Integration/E2E/Scenario07_WatchlistManagementTests.cs` — watchlist management E2E |
