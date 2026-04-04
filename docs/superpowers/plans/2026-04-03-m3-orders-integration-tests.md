# M3: Orders Integration Tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add integration tests for all 8 order endpoints with fixture-based success tests, 401 recovery tests, and endpoint-specific edge cases — extending the existing `OrderTests.cs` which already has 4 tests.

**Architecture:** Each new test follows the established pattern: `TestHarness` + WireMock stubs + Shouldly assertions. Fixture JSON files provide deterministic response data. 401 recovery tests use WireMock scenarios to return 401 on first call, then succeed after re-auth. All new tests go into the existing `OrderTests.cs` file.

**Tech Stack:** xUnit v3, Shouldly, WireMock.Net, Refit, System.Text.Json

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `tests/IbkrConduit.Tests.Integration/Fixtures/Orders/GET-live-orders.json` | Create | Fixture: live orders with one filled SPY order |
| `tests/IbkrConduit.Tests.Integration/Fixtures/Orders/GET-live-orders-empty.json` | Create | Fixture: empty live orders response |
| `tests/IbkrConduit.Tests.Integration/Fixtures/Orders/GET-order-status.json` | Create | Fixture: detailed order status for a filled order |
| `tests/IbkrConduit.Tests.Integration/Fixtures/Orders/GET-trades-empty.json` | Create | Fixture: empty trades array |
| `tests/IbkrConduit.Tests.Integration/Fixtures/Orders/GET-trades.json` | Create | Fixture: trades with one completed trade |
| `tests/IbkrConduit.Tests.Integration/Fixtures/Orders/DELETE-cancel-order.json` | Create | Fixture: cancel order success response |
| `tests/IbkrConduit.Tests.Integration/Fixtures/Orders/POST-whatif-order.json` | Create | Fixture: what-if commission/margin preview |
| `tests/IbkrConduit.Tests.Integration/Fixtures/Orders/POST-modify-order-submitted.json` | Create | Fixture: modify order direct submission response |
| `src/IbkrConduit/Orders/IIbkrOrderApiModels.cs` | Modify | Update `LiveOrder` DTO to match wire format from recordings |
| `tests/IbkrConduit.Tests.Integration/Orders/OrderTests.cs` | Modify | Add 13 new tests |

---

### Task 1: Create Fixture Files

**Files:**
- Create: `tests/IbkrConduit.Tests.Integration/Fixtures/Orders/GET-live-orders.json`
- Create: `tests/IbkrConduit.Tests.Integration/Fixtures/Orders/GET-live-orders-empty.json`
- Create: `tests/IbkrConduit.Tests.Integration/Fixtures/Orders/GET-order-status.json`
- Create: `tests/IbkrConduit.Tests.Integration/Fixtures/Orders/GET-trades-empty.json`
- Create: `tests/IbkrConduit.Tests.Integration/Fixtures/Orders/GET-trades.json`
- Create: `tests/IbkrConduit.Tests.Integration/Fixtures/Orders/DELETE-cancel-order.json`
- Create: `tests/IbkrConduit.Tests.Integration/Fixtures/Orders/POST-whatif-order.json`
- Create: `tests/IbkrConduit.Tests.Integration/Fixtures/Orders/POST-modify-order-submitted.json`

All fixtures use the established format: `{ "Request": {...}, "Response": { "StatusCode": 200, "Headers": {...}, "Body": ... } }`. Account IDs use `U1234567`. Order IDs use test-friendly numeric values.

- [ ] **Step 1: Create `GET-live-orders.json`**

Based on the actual API recording shape, with account ID sanitized to `U1234567`:

```json
{
  "Request": { "Path": "/v1/api/iserver/account/orders", "Methods": ["GET"] },
  "Response": {
    "StatusCode": 200,
    "Headers": { "Content-Type": "application/json; charset=utf-8" },
    "Body": {
      "orders": [
        {
          "acct": "U1234567",
          "conidex": "756733",
          "conid": 756733,
          "account": "U1234567",
          "orderId": 473740665,
          "cashCcy": "USD",
          "sizeAndFills": "1",
          "orderDesc": "Bought 1 SPY Market, Day",
          "description1": "SPY",
          "ticker": "SPY",
          "secType": "STK",
          "listingExchange": "ARCA",
          "remainingQuantity": 0.0,
          "filledQuantity": 1.0,
          "totalSize": 1.0,
          "companyName": "SS SPDR S&P 500 ETF TRUST-US",
          "status": "Filled",
          "order_ccp_status": "Filled",
          "avgPrice": "647.09",
          "origOrderType": "MARKET",
          "supportsTaxOpt": "1",
          "lastExecutionTime": "260401234047",
          "orderType": "Market",
          "bgColor": "#FFFFFF",
          "fgColor": "#000000",
          "isEventTrading": "0",
          "price": "",
          "timeInForce": "CLOSE",
          "lastExecutionTime_r": 1775086847000,
          "side": "BUY"
        }
      ],
      "snapshot": false
    }
  }
}
```

Save to: `tests/IbkrConduit.Tests.Integration/Fixtures/Orders/GET-live-orders.json`

- [ ] **Step 2: Create `GET-live-orders-empty.json`**

```json
{
  "Request": { "Path": "/v1/api/iserver/account/orders", "Methods": ["GET"] },
  "Response": {
    "StatusCode": 200,
    "Headers": { "Content-Type": "application/json; charset=utf-8" },
    "Body": {
      "orders": [],
      "snapshot": false
    }
  }
}
```

Save to: `tests/IbkrConduit.Tests.Integration/Fixtures/Orders/GET-live-orders-empty.json`

- [ ] **Step 3: Create `GET-order-status.json`**

Based on the recording with 40+ fields. Include the key fields that the `OrderStatus` DTO already maps:

```json
{
  "Request": { "Path": "/v1/api/iserver/account/order/status/473740665", "Methods": ["GET"] },
  "Response": {
    "StatusCode": 200,
    "Headers": { "Content-Type": "application/json; charset=utf-8" },
    "Body": {
      "sub_type": null,
      "request_id": "12345",
      "order_id": 473740665,
      "conidex": "756733",
      "conid": 756733,
      "symbol": "SPY",
      "side": "BUY",
      "contract_description_1": "SPY",
      "listing_exchange": "ARCA",
      "is_event_trading": "0",
      "order_desc": "Bought 1 SPY Market, Day",
      "order_status": "Filled",
      "order_type": "Market",
      "size": "1.0",
      "fill_price": "647.09",
      "filled_quantity": "1.0",
      "remaining_quantity": "0.0",
      "avg_fill_price": "647.09",
      "last_fill_price": "647.09",
      "total_size": "1.0",
      "total_cash_size": "0.0",
      "price": "0.0",
      "tif": "DAY",
      "bg_color": "#FFFFFF",
      "fg_color": "#000000",
      "order_not_editable": true,
      "editable_fields": "",
      "cannot_cancel_order": true,
      "outside_rth": false,
      "all_or_none": false,
      "deactivate_order": false,
      "use_price_mgmt_algo": false,
      "sec_type": "STK",
      "order_description": "Bought 1 SPY Market, Day",
      "order_description_with_contract": "Bought 1 SPY Market, Day",
      "clearing_id": "IB",
      "clearing_name": "IB",
      "alert_active": 0,
      "child_order_type": "0",
      "order_clearing_account": "U1234567",
      "size_and_fills": "1/1",
      "exit_strategy_display_price": "0.0",
      "exit_strategy_chart_description": "",
      "exit_strategy_tool_availability": "0",
      "allowed_duplicate_opposite": true,
      "order_time": "260401230000"
    }
  }
}
```

Save to: `tests/IbkrConduit.Tests.Integration/Fixtures/Orders/GET-order-status.json`

- [ ] **Step 4: Create `GET-trades-empty.json`**

```json
{
  "Request": { "Path": "/v1/api/iserver/account/trades", "Methods": ["GET"] },
  "Response": {
    "StatusCode": 200,
    "Headers": { "Content-Type": "application/json; charset=utf-8" },
    "Body": []
  }
}
```

Save to: `tests/IbkrConduit.Tests.Integration/Fixtures/Orders/GET-trades-empty.json`

- [ ] **Step 5: Create `GET-trades.json`**

```json
{
  "Request": { "Path": "/v1/api/iserver/account/trades", "Methods": ["GET"] },
  "Response": {
    "StatusCode": 200,
    "Headers": { "Content-Type": "application/json; charset=utf-8" },
    "Body": [
      {
        "execution_id": "00018fae.67890abc.01.01",
        "conid": 756733,
        "symbol": "SPY",
        "side": "BOT",
        "size": 1.0,
        "price": 647.09,
        "order_ref": "Order123",
        "submitter": "U1234567"
      }
    ]
  }
}
```

Save to: `tests/IbkrConduit.Tests.Integration/Fixtures/Orders/GET-trades.json`

- [ ] **Step 6: Create `DELETE-cancel-order.json`**

```json
{
  "Request": { "Path": "/v1/api/iserver/account/U1234567/order/602801486", "Methods": ["DELETE"] },
  "Response": {
    "StatusCode": 200,
    "Headers": { "Content-Type": "application/json; charset=utf-8" },
    "Body": {
      "msg": "Request was submitted",
      "order_id": 602801486,
      "conid": -1,
      "account": null
    }
  }
}
```

Save to: `tests/IbkrConduit.Tests.Integration/Fixtures/Orders/DELETE-cancel-order.json`

- [ ] **Step 7: Create `POST-whatif-order.json`**

```json
{
  "Request": { "Path": "/v1/api/iserver/account/U1234567/orders/whatif", "Methods": ["POST"] },
  "Response": {
    "StatusCode": 200,
    "Headers": { "Content-Type": "application/json; charset=utf-8" },
    "Body": {
      "amount": {
        "amount": "1 USD (1 Shares)",
        "commission": "0.01 USD",
        "total": "1.01 USD"
      },
      "equity": {
        "current": "1,006,413",
        "change": "163",
        "after": "1,006,576"
      },
      "initial": {
        "current": "8,637",
        "change": "164",
        "after": "8,801"
      },
      "maintenance": {
        "current": "8,637",
        "change": "164",
        "after": "8,801"
      },
      "warn": "The following order \"BUY 1 SPY LMT 1.00\" price exceeds...",
      "error": null
    }
  }
}
```

Save to: `tests/IbkrConduit.Tests.Integration/Fixtures/Orders/POST-whatif-order.json`

- [ ] **Step 8: Create `POST-modify-order-submitted.json`**

Uses the same response shape as place order. Distinct order ID to differentiate in tests:

```json
{
  "Request": { "Path": "/v1/api/iserver/account/U1234567/order/473740665", "Methods": ["POST"] },
  "Response": {
    "StatusCode": 200,
    "Headers": { "Content-Type": "application/json; charset=utf-8" },
    "Body": [
      {
        "order_id": "555666777",
        "order_status": "PreSubmitted",
        "encrypt_message": "1"
      }
    ]
  }
}
```

Save to: `tests/IbkrConduit.Tests.Integration/Fixtures/Orders/POST-modify-order-submitted.json`

- [ ] **Step 9: Verify fixtures load correctly**

Run: `dotnet build --configuration Release /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj`

Expected: Build succeeds. (Fixtures are copied to output via `CopyToOutputDirectory`.)

- [ ] **Step 10: Commit**

```bash
git add tests/IbkrConduit.Tests.Integration/Fixtures/Orders/GET-live-orders.json
git add tests/IbkrConduit.Tests.Integration/Fixtures/Orders/GET-live-orders-empty.json
git add tests/IbkrConduit.Tests.Integration/Fixtures/Orders/GET-order-status.json
git add tests/IbkrConduit.Tests.Integration/Fixtures/Orders/GET-trades-empty.json
git add tests/IbkrConduit.Tests.Integration/Fixtures/Orders/GET-trades.json
git add tests/IbkrConduit.Tests.Integration/Fixtures/Orders/DELETE-cancel-order.json
git add tests/IbkrConduit.Tests.Integration/Fixtures/Orders/POST-whatif-order.json
git add tests/IbkrConduit.Tests.Integration/Fixtures/Orders/POST-modify-order-submitted.json
git commit -m "test: add fixture files for M3 orders integration tests"
```

---

### Task 2: Update LiveOrder DTO to Match Wire Format

The current `LiveOrder` DTO has minimal fields. The recording shows many more fields that tests need to assert. The DTO needs additional fields from the recording and a `[JsonExtensionData]` property for unmapped fields.

**Important:** The recording shows `orderId` as a numeric value (`473740665`), but the current DTO maps it as `string`. The IBKR API sends it as a JSON number here (unlike place-order which sends it as a string). We need `JsonNumberHandling` to handle both.

**Files:**
- Modify: `src/IbkrConduit/Orders/IIbkrOrderApiModels.cs` (lines 134-145, the `LiveOrder` record)

- [ ] **Step 1: Write the failing test**

Add to `tests/IbkrConduit.Tests.Integration/Orders/OrderTests.cs` (append before the `DisposeAsync` method):

```csharp
[Fact]
public async Task GetLiveOrders_ReturnsAllFields()
{
    _harness.StubAuthenticatedGet(
        "/v1/api/iserver/account/orders",
        FixtureLoader.LoadBody("Orders", "GET-live-orders"));

    var orders = await _harness.Client.Orders.GetLiveOrdersAsync(TestContext.Current.CancellationToken);

    orders.ShouldNotBeEmpty();
    var order = orders[0];
    order.Account.ShouldBe("U1234567");
    order.Conid.ShouldBe(756733);
    order.ConidEx.ShouldBe("756733");
    order.OrderId.ShouldBe(473740665);
    order.Ticker.ShouldBe("SPY");
    order.SecType.ShouldBe("STK");
    order.ListingExchange.ShouldBe("ARCA");
    order.Side.ShouldBe("BUY");
    order.Status.ShouldBe("Filled");
    order.OrderType.ShouldBe("Market");
    order.FilledQuantity.ShouldBe(1.0m);
    order.RemainingQuantity.ShouldBe(0.0m);
    order.TotalSize.ShouldBe(1.0m);
    order.CompanyName.ShouldBe("SS SPDR S&P 500 ETF TRUST-US");
    order.AvgPrice.ShouldBe("647.09");
    order.TimeInForce.ShouldBe("CLOSE");
    order.OrderDescription.ShouldBe("Bought 1 SPY Market, Day");

    _harness.VerifyHandshakeOccurred();
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --configuration Release --filter "GetLiveOrders_ReturnsAllFields" /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj`

Expected: FAIL — `LiveOrder` does not have `Account`, `ConidEx`, `OrderId` (as int), `Ticker`, `SecType`, etc.

- [ ] **Step 3: Update the LiveOrder DTO**

Replace the `LiveOrder` record in `src/IbkrConduit/Orders/IIbkrOrderApiModels.cs` (lines 121-145).

Replace the existing `LiveOrder` record and its doc comments (starting at `/// <summary>` before `LiveOrder` through the closing parenthesis `);\n`) with:

```csharp
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
/// <param name="FilledQuantity">The filled quantity.</param>
/// <param name="RemainingQuantity">The remaining quantity.</param>
/// <param name="TotalSize">The total order size.</param>
/// <param name="CompanyName">The company name.</param>
/// <param name="AvgPrice">The average fill price as a string.</param>
/// <param name="TimeInForce">The time in force.</param>
/// <param name="OrderDescription">The order description.</param>
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
    [property: JsonPropertyName("filledQuantity")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    decimal FilledQuantity,
    [property: JsonPropertyName("remainingQuantity")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    decimal RemainingQuantity,
    [property: JsonPropertyName("totalSize")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    decimal TotalSize,
    [property: JsonPropertyName("companyName")] string? CompanyName,
    [property: JsonPropertyName("avgPrice")] string? AvgPrice,
    [property: JsonPropertyName("timeInForce")] string? TimeInForce,
    [property: JsonPropertyName("orderDesc")] string? OrderDescription)
{
    /// <summary>Additional unmapped properties from the API response.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --configuration Release --filter "GetLiveOrders_ReturnsAllFields" /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj`

Expected: PASS

- [ ] **Step 5: Run all existing tests to verify nothing is broken**

Run: `dotnet test --configuration Release /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj`

Expected: All tests pass (existing 4 order tests + 1 new + all portfolio/session/etc).

- [ ] **Step 6: Commit**

```bash
git add src/IbkrConduit/Orders/IIbkrOrderApiModels.cs
git add tests/IbkrConduit.Tests.Integration/Orders/OrderTests.cs
git commit -m "feat: update LiveOrder DTO to match wire format + add GetLiveOrders success test"
```

---

### Task 3: Live Orders — Empty + 401 Recovery Tests

**Files:**
- Modify: `tests/IbkrConduit.Tests.Integration/Orders/OrderTests.cs`

- [ ] **Step 1: Write the empty orders test**

Add to `OrderTests.cs` (before `DisposeAsync`):

```csharp
[Fact]
public async Task GetLiveOrders_EmptyOrders_ReturnsEmptyList()
{
    _harness.StubAuthenticatedGet(
        "/v1/api/iserver/account/orders",
        FixtureLoader.LoadBody("Orders", "GET-live-orders-empty"));

    var orders = await _harness.Client.Orders.GetLiveOrdersAsync(TestContext.Current.CancellationToken);

    orders.ShouldBeEmpty();
}
```

- [ ] **Step 2: Run test to verify it passes**

Run: `dotnet test --configuration Release --filter "GetLiveOrders_EmptyOrders_ReturnsEmptyList" /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj`

Expected: PASS (the `OrderOperations.GetLiveOrdersAsync` already returns `response.Orders ?? []`)

- [ ] **Step 3: Write the 401 recovery test**

Add to `OrderTests.cs` (before `DisposeAsync`):

```csharp
[Fact]
public async Task GetLiveOrders_401Recovery_ReauthenticatesAndRetries()
{
    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/iserver/account/orders")
            .UsingGet())
        .InScenario("live-orders-401")
        .WillSetStateTo("token-expired")
        .RespondWith(
            Response.Create()
                .WithStatusCode(401)
                .WithBody("Unauthorized"));

    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/iserver/account/orders")
            .UsingGet())
        .InScenario("live-orders-401")
        .WhenStateIs("token-expired")
        .RespondWith(
            Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(FixtureLoader.LoadBody("Orders", "GET-live-orders")));

    var orders = await _harness.Client.Orders.GetLiveOrdersAsync(TestContext.Current.CancellationToken);

    orders.ShouldNotBeEmpty();
    orders[0].OrderId.ShouldBe(473740665);

    _harness.VerifyReauthenticationOccurred();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --configuration Release --filter "GetLiveOrders_401Recovery_ReauthenticatesAndRetries" /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add tests/IbkrConduit.Tests.Integration/Orders/OrderTests.cs
git commit -m "test: add live orders empty response and 401 recovery tests"
```

---

### Task 4: Order Status Tests — Success + 401 Recovery

**Files:**
- Modify: `tests/IbkrConduit.Tests.Integration/Orders/OrderTests.cs`

- [ ] **Step 1: Write the success test**

Add to `OrderTests.cs` (before `DisposeAsync`):

```csharp
[Fact]
public async Task GetOrderStatus_ReturnsAllFields()
{
    _harness.StubAuthenticatedGet(
        "/v1/api/iserver/account/order/status/473740665",
        FixtureLoader.LoadBody("Orders", "GET-order-status"));

    var status = await _harness.Client.Orders.GetOrderStatusAsync(
        "473740665", TestContext.Current.CancellationToken);

    status.OrderId.ShouldBe(473740665);
    status.Conid.ShouldBe(756733);
    status.ConidEx.ShouldBe("756733");
    status.Symbol.ShouldBe("SPY");
    status.Side.ShouldBe("BUY");
    status.Status.ShouldBe("Filled");
    status.OrderType.ShouldBe("Market");
    status.Size.ShouldBe(1.0m);
    status.FillPrice.ShouldBe(647.09m);
    status.FilledQuantity.ShouldBe(1.0m);
    status.RemainingQuantity.ShouldBe(0.0m);
    status.AvgFillPrice.ShouldBe(647.09m);
    status.LastFillPrice.ShouldBe(647.09m);
    status.TotalSize.ShouldBe(1.0m);
    status.Tif.ShouldBe("DAY");
    status.ContractDescription.ShouldBe("SPY");
    status.ListingExchange.ShouldBe("ARCA");
    status.OrderNotEditable.ShouldBe(true);
    status.CannotCancelOrder.ShouldBe(true);
    status.OrderDescription.ShouldBe("Bought 1 SPY Market, Day");
    status.BgColor.ShouldBe("#FFFFFF");
    status.FgColor.ShouldBe("#000000");

    _harness.VerifyHandshakeOccurred();
}
```

- [ ] **Step 2: Run test to verify it passes**

Run: `dotnet test --configuration Release --filter "GetOrderStatus_ReturnsAllFields" /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj`

Expected: PASS

- [ ] **Step 3: Write the 401 recovery test**

Add to `OrderTests.cs` (before `DisposeAsync`):

```csharp
[Fact]
public async Task GetOrderStatus_401Recovery_ReauthenticatesAndRetries()
{
    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/iserver/account/order/status/473740665")
            .UsingGet())
        .InScenario("order-status-401")
        .WillSetStateTo("token-expired")
        .RespondWith(
            Response.Create()
                .WithStatusCode(401)
                .WithBody("Unauthorized"));

    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/iserver/account/order/status/473740665")
            .UsingGet())
        .InScenario("order-status-401")
        .WhenStateIs("token-expired")
        .RespondWith(
            Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(FixtureLoader.LoadBody("Orders", "GET-order-status")));

    var status = await _harness.Client.Orders.GetOrderStatusAsync(
        "473740665", TestContext.Current.CancellationToken);

    status.OrderId.ShouldBe(473740665);
    status.Symbol.ShouldBe("SPY");

    _harness.VerifyReauthenticationOccurred();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --configuration Release --filter "GetOrderStatus_401Recovery_ReauthenticatesAndRetries" /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add tests/IbkrConduit.Tests.Integration/Orders/OrderTests.cs
git commit -m "test: add order status success and 401 recovery tests"
```

---

### Task 5: Cancel Order Tests — Success + 401 Recovery

**Files:**
- Modify: `tests/IbkrConduit.Tests.Integration/Orders/OrderTests.cs`

- [ ] **Step 1: Write the success test**

Add to `OrderTests.cs` (before `DisposeAsync`):

```csharp
[Fact]
public async Task CancelOrder_ReturnsAllFields()
{
    _harness.StubAuthenticated(
        HttpMethod.Delete,
        "/v1/api/iserver/account/U1234567/order/602801486",
        FixtureLoader.LoadBody("Orders", "DELETE-cancel-order"));

    var result = await _harness.Client.Orders.CancelOrderAsync(
        "U1234567", "602801486", TestContext.Current.CancellationToken);

    result.Message.ShouldBe("Request was submitted");
    result.OrderId.ShouldBe(602801486);
    result.Conid.ShouldBe(-1);

    _harness.VerifyHandshakeOccurred();
}
```

Note: This test requires `using System.Net.Http;` at the top of the file (for `HttpMethod.Delete`). The existing file does not have this import. Add it if missing.

- [ ] **Step 2: Run test to verify it passes**

Run: `dotnet test --configuration Release --filter "CancelOrder_ReturnsAllFields" /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj`

Expected: PASS

- [ ] **Step 3: Write the 401 recovery test**

Add to `OrderTests.cs` (before `DisposeAsync`):

```csharp
[Fact]
public async Task CancelOrder_401Recovery_ReauthenticatesAndRetries()
{
    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/iserver/account/U1234567/order/602801486")
            .UsingDelete())
        .InScenario("cancel-401")
        .WillSetStateTo("token-expired")
        .RespondWith(
            Response.Create()
                .WithStatusCode(401)
                .WithBody("Unauthorized"));

    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/iserver/account/U1234567/order/602801486")
            .UsingDelete())
        .InScenario("cancel-401")
        .WhenStateIs("token-expired")
        .RespondWith(
            Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(FixtureLoader.LoadBody("Orders", "DELETE-cancel-order")));

    var result = await _harness.Client.Orders.CancelOrderAsync(
        "U1234567", "602801486", TestContext.Current.CancellationToken);

    result.Message.ShouldBe("Request was submitted");
    result.OrderId.ShouldBe(602801486);

    _harness.VerifyReauthenticationOccurred();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --configuration Release --filter "CancelOrder_401Recovery_ReauthenticatesAndRetries" /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add tests/IbkrConduit.Tests.Integration/Orders/OrderTests.cs
git commit -m "test: add cancel order success and 401 recovery tests"
```

---

### Task 6: Trades Tests — Success + Empty + 401 Recovery

**Files:**
- Modify: `tests/IbkrConduit.Tests.Integration/Orders/OrderTests.cs`

- [ ] **Step 1: Write the success test with populated trades**

Add to `OrderTests.cs` (before `DisposeAsync`):

```csharp
[Fact]
public async Task GetTrades_ReturnsAllFields()
{
    _harness.StubAuthenticatedGet(
        "/v1/api/iserver/account/trades",
        FixtureLoader.LoadBody("Orders", "GET-trades"));

    var trades = await _harness.Client.Orders.GetTradesAsync(TestContext.Current.CancellationToken);

    trades.ShouldNotBeEmpty();
    var trade = trades[0];
    trade.ExecutionId.ShouldBe("00018fae.67890abc.01.01");
    trade.Conid.ShouldBe(756733);
    trade.Symbol.ShouldBe("SPY");
    trade.Side.ShouldBe("BOT");
    trade.Size.ShouldBe(1.0m);
    trade.Price.ShouldBe(647.09m);
    trade.OrderRef.ShouldBe("Order123");
    trade.Submitter.ShouldBe("U1234567");

    _harness.VerifyHandshakeOccurred();
}
```

- [ ] **Step 2: Run test to verify it passes**

Run: `dotnet test --configuration Release --filter "GetTrades_ReturnsAllFields" /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj`

Expected: PASS

- [ ] **Step 3: Write the empty trades test**

Add to `OrderTests.cs` (before `DisposeAsync`):

```csharp
[Fact]
public async Task GetTrades_EmptyResponse_ReturnsEmptyList()
{
    _harness.StubAuthenticatedGet(
        "/v1/api/iserver/account/trades",
        FixtureLoader.LoadBody("Orders", "GET-trades-empty"));

    var trades = await _harness.Client.Orders.GetTradesAsync(TestContext.Current.CancellationToken);

    trades.ShouldBeEmpty();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --configuration Release --filter "GetTrades_EmptyResponse_ReturnsEmptyList" /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj`

Expected: PASS

- [ ] **Step 5: Write the 401 recovery test**

Add to `OrderTests.cs` (before `DisposeAsync`):

```csharp
[Fact]
public async Task GetTrades_401Recovery_ReauthenticatesAndRetries()
{
    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/iserver/account/trades")
            .UsingGet())
        .InScenario("trades-401")
        .WillSetStateTo("token-expired")
        .RespondWith(
            Response.Create()
                .WithStatusCode(401)
                .WithBody("Unauthorized"));

    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/iserver/account/trades")
            .UsingGet())
        .InScenario("trades-401")
        .WhenStateIs("token-expired")
        .RespondWith(
            Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(FixtureLoader.LoadBody("Orders", "GET-trades")));

    var trades = await _harness.Client.Orders.GetTradesAsync(TestContext.Current.CancellationToken);

    trades.ShouldNotBeEmpty();
    trades[0].Symbol.ShouldBe("SPY");

    _harness.VerifyReauthenticationOccurred();
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test --configuration Release --filter "GetTrades_401Recovery_ReauthenticatesAndRetries" /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj`

Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add tests/IbkrConduit.Tests.Integration/Orders/OrderTests.cs
git commit -m "test: add trades success, empty, and 401 recovery tests"
```

---

### Task 7: WhatIf Order Tests — Success + 401 Recovery

**Files:**
- Modify: `tests/IbkrConduit.Tests.Integration/Orders/OrderTests.cs`

- [ ] **Step 1: Write the success test**

Add to `OrderTests.cs` (before `DisposeAsync`):

```csharp
[Fact]
public async Task WhatIfOrder_ReturnsAllFields()
{
    _harness.StubAuthenticatedPost(
        "/v1/api/iserver/account/U1234567/orders/whatif",
        FixtureLoader.LoadBody("Orders", "POST-whatif-order"));

    var order = new OrderRequest
    {
        Conid = 756733,
        Side = "BUY",
        Quantity = 1,
        OrderType = "LMT",
        Price = 1.00m,
        Tif = "DAY",
    };

    var result = await _harness.Client.Orders.WhatIfOrderAsync(
        "U1234567", order, TestContext.Current.CancellationToken);

    result.Amount.ShouldNotBeNull();
    result.Amount!.Amount.ShouldBe("1 USD (1 Shares)");
    result.Amount!.Commission.ShouldBe("0.01 USD");
    result.Amount!.Total.ShouldBe("1.01 USD");

    result.Equity.ShouldNotBeNull();
    result.Equity!.Current.ShouldBe("1,006,413");
    result.Equity!.Change.ShouldBe("163");
    result.Equity!.After.ShouldBe("1,006,576");

    result.Initial.ShouldNotBeNull();
    result.Initial!.Current.ShouldBe("8,637");
    result.Initial!.Change.ShouldBe("164");
    result.Initial!.After.ShouldBe("8,801");

    result.Maintenance.ShouldNotBeNull();
    result.Maintenance!.Current.ShouldBe("8,637");
    result.Maintenance!.Change.ShouldBe("164");
    result.Maintenance!.After.ShouldBe("8,801");

    result.Warning.ShouldNotBeNull();
    result.Error.ShouldBeNull();

    _harness.VerifyHandshakeOccurred();
}
```

- [ ] **Step 2: Run test to verify it passes**

Run: `dotnet test --configuration Release --filter "WhatIfOrder_ReturnsAllFields" /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj`

Expected: PASS

- [ ] **Step 3: Write the 401 recovery test**

Add to `OrderTests.cs` (before `DisposeAsync`):

```csharp
[Fact]
public async Task WhatIfOrder_401Recovery_ReauthenticatesAndRetries()
{
    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/iserver/account/U1234567/orders/whatif")
            .UsingPost())
        .InScenario("whatif-401")
        .WillSetStateTo("token-expired")
        .RespondWith(
            Response.Create()
                .WithStatusCode(401)
                .WithBody("Unauthorized"));

    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/iserver/account/U1234567/orders/whatif")
            .UsingPost())
        .InScenario("whatif-401")
        .WhenStateIs("token-expired")
        .RespondWith(
            Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(FixtureLoader.LoadBody("Orders", "POST-whatif-order")));

    var order = new OrderRequest
    {
        Conid = 756733,
        Side = "BUY",
        Quantity = 1,
        OrderType = "LMT",
        Price = 1.00m,
        Tif = "DAY",
    };

    var result = await _harness.Client.Orders.WhatIfOrderAsync(
        "U1234567", order, TestContext.Current.CancellationToken);

    result.Amount.ShouldNotBeNull();
    result.Amount!.Commission.ShouldBe("0.01 USD");

    _harness.VerifyReauthenticationOccurred();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --configuration Release --filter "WhatIfOrder_401Recovery_ReauthenticatesAndRetries" /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add tests/IbkrConduit.Tests.Integration/Orders/OrderTests.cs
git commit -m "test: add whatif order success and 401 recovery tests"
```

---

### Task 8: Modify Order Tests — Success + 401 Recovery

**Files:**
- Modify: `tests/IbkrConduit.Tests.Integration/Orders/OrderTests.cs`

- [ ] **Step 1: Write the success test**

Add to `OrderTests.cs` (before `DisposeAsync`):

```csharp
[Fact]
public async Task ModifyOrder_DirectSubmission_ReturnsOrderSubmitted()
{
    _harness.StubAuthenticatedPost(
        "/v1/api/iserver/account/U1234567/order/473740665",
        FixtureLoader.LoadBody("Orders", "POST-modify-order-submitted"));

    var order = new OrderRequest
    {
        Conid = 756733,
        Side = "BUY",
        Quantity = 2,
        OrderType = "LMT",
        Price = 650.00m,
        Tif = "GTC",
    };

    var result = await _harness.Client.Orders.ModifyOrderAsync(
        "U1234567", "473740665", order, TestContext.Current.CancellationToken);

    result.IsT0.ShouldBeTrue("Expected OrderSubmitted but got OrderConfirmationRequired");
    var submitted = result.AsT0;
    submitted.OrderId.ShouldBe("555666777");
    submitted.OrderStatus.ShouldBe("PreSubmitted");

    _harness.VerifyHandshakeOccurred();
}
```

- [ ] **Step 2: Run test to verify it passes**

Run: `dotnet test --configuration Release --filter "ModifyOrder_DirectSubmission_ReturnsOrderSubmitted" /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj`

Expected: PASS

- [ ] **Step 3: Write the 401 recovery test**

Add to `OrderTests.cs` (before `DisposeAsync`):

```csharp
[Fact]
public async Task ModifyOrder_401Recovery_ReauthenticatesAndRetries()
{
    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/iserver/account/U1234567/order/473740665")
            .UsingPost())
        .InScenario("modify-401")
        .WillSetStateTo("token-expired")
        .RespondWith(
            Response.Create()
                .WithStatusCode(401)
                .WithBody("Unauthorized"));

    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/iserver/account/U1234567/order/473740665")
            .UsingPost())
        .InScenario("modify-401")
        .WhenStateIs("token-expired")
        .RespondWith(
            Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(FixtureLoader.LoadBody("Orders", "POST-modify-order-submitted")));

    var order = new OrderRequest
    {
        Conid = 756733,
        Side = "BUY",
        Quantity = 2,
        OrderType = "LMT",
        Price = 650.00m,
        Tif = "GTC",
    };

    var result = await _harness.Client.Orders.ModifyOrderAsync(
        "U1234567", "473740665", order, TestContext.Current.CancellationToken);

    result.IsT0.ShouldBeTrue("Expected OrderSubmitted after 401 recovery");
    result.AsT0.OrderId.ShouldBe("555666777");

    _harness.VerifyReauthenticationOccurred();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --configuration Release --filter "ModifyOrder_401Recovery_ReauthenticatesAndRetries" /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add tests/IbkrConduit.Tests.Integration/Orders/OrderTests.cs
git commit -m "test: add modify order success and 401 recovery tests"
```

---

### Task 9: Add Missing `using` Import + Final Verification

**Files:**
- Modify: `tests/IbkrConduit.Tests.Integration/Orders/OrderTests.cs`

- [ ] **Step 1: Add `System.Net.Http` using if not already present**

The `CancelOrder_ReturnsAllFields` test uses `HttpMethod.Delete`. Check if `using System.Net.Http;` is already in the file. If not, add it after `using System.Threading.Tasks;`:

```csharp
using System.Net.Http;
```

- [ ] **Step 2: Run the full test suite**

Run: `dotnet test --configuration Release /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj`

Expected: All 17 order tests pass (4 existing + 13 new), plus all other integration tests.

- [ ] **Step 3: Run the full build + lint check**

Run: `dotnet build --configuration Release /workspace/ibkr-conduit`

Run: `dotnet format --verify-no-changes /workspace/ibkr-conduit`

Expected: Both pass with zero warnings.

- [ ] **Step 4: Run all tests across the entire solution**

Run: `dotnet test --configuration Release /workspace/ibkr-conduit`

Expected: All tests pass.

- [ ] **Step 5: Final commit if any formatting fixes were needed**

```bash
git add -u
git commit -m "chore: formatting fixes for M3 orders integration tests"
```

---

## Test Summary

| # | Test Name | Endpoint | Type |
|---|---|---|---|
| 1 | `PlaceOrder_DirectSubmission_ReturnsOrderSubmitted` | POST place | Success (existing) |
| 2 | `PlaceOrder_ConfirmationRequired_ReturnsConfirmationThenSubmits` | POST place + reply | Success (existing) |
| 3 | `PlaceOrder_401Recovery_ReauthenticatesAndRetries` | POST place | 401 (existing) |
| 4 | `Reply_401Recovery_ReauthenticatesAndRetries` | POST reply | 401 (existing) |
| 5 | `GetLiveOrders_ReturnsAllFields` | GET orders | Success |
| 6 | `GetLiveOrders_EmptyOrders_ReturnsEmptyList` | GET orders | Edge case |
| 7 | `GetLiveOrders_401Recovery_ReauthenticatesAndRetries` | GET orders | 401 |
| 8 | `GetOrderStatus_ReturnsAllFields` | GET status | Success |
| 9 | `GetOrderStatus_401Recovery_ReauthenticatesAndRetries` | GET status | 401 |
| 10 | `CancelOrder_ReturnsAllFields` | DELETE cancel | Success |
| 11 | `CancelOrder_401Recovery_ReauthenticatesAndRetries` | DELETE cancel | 401 |
| 12 | `GetTrades_ReturnsAllFields` | GET trades | Success |
| 13 | `GetTrades_EmptyResponse_ReturnsEmptyList` | GET trades | Edge case |
| 14 | `GetTrades_401Recovery_ReauthenticatesAndRetries` | GET trades | 401 |
| 15 | `WhatIfOrder_ReturnsAllFields` | POST whatif | Success |
| 16 | `WhatIfOrder_401Recovery_ReauthenticatesAndRetries` | POST whatif | 401 |
| 17 | `ModifyOrder_DirectSubmission_ReturnsOrderSubmitted` | POST modify | Success |
| 18 | `ModifyOrder_401Recovery_ReauthenticatesAndRetries` | POST modify | 401 |

**Total: 18 tests** (4 existing + 14 new)
