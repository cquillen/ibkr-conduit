# Milestone 3b — Order Management + IIbkrClient Facade

**Date:** 2026-03-31
**Status:** Approved
**Goal:** Submit and cancel orders against the paper account, with conid resolution and a unified `IIbkrClient` facade as the single consumer entry point.

---

## Scope

M3b introduces order management (place, cancel, query) and contract resolution, wrapped in a unified `IIbkrClient` facade that replaces the previous pattern of consumers resolving individual Refit interfaces. After M3b:

1. Consumers resolve `IIbkrClient` from DI as the single entry point
2. `client.Portfolio.GetAccountsAsync()` — existing functionality, now through facade
3. `client.Contracts.SearchBySymbolAsync("AAPL")` — conid resolution
4. `client.Orders.PlaceOrderAsync(accountId, order)` — order submission with auto-confirm
5. `client.Orders.CancelOrderAsync(accountId, orderId)` — order cancellation
6. `client.Orders.GetLiveOrdersAsync()` — current session orders
7. `client.Orders.GetTradesAsync()` — trade history

### Deferred

- **Selective order question answers** — auto-confirm all for now, answer map in `docs/future-enhancements.md`
- **Order modification** — can be added later, same pattern as place order
- **What-if orders** — commission preview, not needed for M3b
- **Conid caching** — consumers cache conids themselves; library-level cache deferred

---

## Architecture

### IIbkrClient Facade

```
Consumer resolves IIbkrClient from DI
    │
    ├── client.Portfolio   (IPortfolioOperations)
    │       └── wraps IIbkrPortfolioApi (existing Refit)
    │
    ├── client.Contracts   (IContractOperations)
    │       └── wraps IIbkrContractApi (new Refit)
    │
    └── client.Orders      (IOrderOperations)
            └── wraps IIbkrOrderApi (new Refit)
            └── question/reply loop with auto-confirm
            └── per-account SemaphoreSlim serialization
```

### DI Registration

`AddIbkrClient(credentials, options?)` replaces `AddIbkrClient<TApi>(credentials, options?)`. Internally registers all Refit interfaces and the facade. Consumers resolve `IIbkrClient`.

### HTTP Pipeline (unchanged from M3a)

```
Refit → TokenRefreshHandler → ResilienceHandler → GlobalRateLimiting → EndpointRateLimiting → OAuthSigning → HttpClient
```

---

## Task 3b.1 — Contract Refit Interface + Models

### IIbkrContractApi (internal Refit interface)

```csharp
public interface IIbkrContractApi
{
    [Get("/v1/api/iserver/secdef/search")]
    Task<List<ContractSearchResult>> SearchBySymbolAsync([Query] string symbol);

    [Get("/v1/api/iserver/contract/{conid}/info")]
    Task<ContractDetails> GetContractDetailsAsync(string conid);
}
```

### Models (IIbkrContractApiModels.cs)

```csharp
public record ContractSearchResult(
    [property: JsonPropertyName("conid")] int Conid,
    [property: JsonPropertyName("companyHeader")] string CompanyHeader,
    [property: JsonPropertyName("companyName")] string CompanyName,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("conidEx")] string ConidEx,
    [property: JsonPropertyName("secType")] string SecurityType,
    [property: JsonPropertyName("listingExchange")] string ListingExchange,
    [property: JsonPropertyName("sections")] List<ContractSection>? Sections);

public record ContractSection(
    [property: JsonPropertyName("secType")] string SecurityType,
    [property: JsonPropertyName("months")] string? Months,
    [property: JsonPropertyName("symbol")] string? Symbol,
    [property: JsonPropertyName("exchange")] string? Exchange,
    [property: JsonPropertyName("conid")] int? Conid);

public record ContractDetails(
    [property: JsonPropertyName("con_id")] int Conid,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("company_name")] string CompanyName,
    [property: JsonPropertyName("exchange")] string Exchange,
    [property: JsonPropertyName("listing_exchange")] string ListingExchange,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("instrument_type")] string InstrumentType,
    [property: JsonPropertyName("valid_exchanges")] string ValidExchanges);
```

Note: The exact JSON property names will be verified against real IBKR responses during implementation. The models above are based on the ibind reference and IBKR documentation.

---

## Task 3b.2 — Order Refit Interface + Models

### IIbkrOrderApi (internal Refit interface)

```csharp
public interface IIbkrOrderApi
{
    [Post("/v1/api/iserver/account/{accountId}/orders")]
    Task<List<OrderSubmissionResponse>> PlaceOrderAsync(
        string accountId, [Body] OrdersPayload orders);

    [Post("/v1/api/iserver/reply/{replyId}")]
    Task<List<OrderSubmissionResponse>> ReplyAsync(
        string replyId, [Body] ReplyRequest request);

    [Delete("/v1/api/iserver/account/{accountId}/order/{orderId}")]
    Task<CancelOrderResponse> CancelOrderAsync(string accountId, string orderId);

    [Get("/v1/api/iserver/account/orders")]
    Task<OrdersResponse> GetLiveOrdersAsync();

    [Get("/v1/api/iserver/account/trades")]
    Task<List<Trade>> GetTradesAsync();
}
```

### Models (IIbkrOrderApiModels.cs)

**Consumer-facing models:**

```csharp
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

/// <summary>Successful order placement result.</summary>
public record OrderResult(string OrderId, string OrderStatus);

/// <summary>Order cancellation result.</summary>
public record CancelOrderResponse(
    [property: JsonPropertyName("msg")] string Message,
    [property: JsonPropertyName("order_id")] string OrderId,
    [property: JsonPropertyName("conid")] int Conid);
```

**Internal wire models:**

```csharp
/// <summary>Wrapper for the orders array sent to IBKR.</summary>
public record OrdersPayload(
    [property: JsonPropertyName("orders")] List<OrderWireModel> Orders);

/// <summary>Wire format for a single order sent to IBKR.</summary>
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
public record OrderSubmissionResponse(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("message")] List<string>? Message,
    [property: JsonPropertyName("isSuppressed")] bool? IsSuppressed,
    [property: JsonPropertyName("messageIds")] List<string>? MessageIds,
    [property: JsonPropertyName("order_id")] string? OrderId,
    [property: JsonPropertyName("order_status")] string? OrderStatus);

/// <summary>Reply confirmation body.</summary>
public record ReplyRequest(
    [property: JsonPropertyName("confirmed")] bool Confirmed);

/// <summary>Live orders response wrapper.</summary>
public record OrdersResponse(
    [property: JsonPropertyName("orders")] List<LiveOrder>? Orders);

/// <summary>A live order in the current session.</summary>
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

/// <summary>A completed trade.</summary>
public record Trade(
    [property: JsonPropertyName("execution_id")] string ExecutionId,
    [property: JsonPropertyName("conid")] int Conid,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("size")] decimal Size,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("order_ref")] string OrderRef,
    [property: JsonPropertyName("submitter")] string Submitter);
```

---

## Task 3b.3 — Operations Interfaces

```csharp
/// <summary>Portfolio operations on the IBKR API.</summary>
public interface IPortfolioOperations
{
    Task<List<Account>> GetAccountsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Contract lookup operations on the IBKR API.</summary>
public interface IContractOperations
{
    Task<List<ContractSearchResult>> SearchBySymbolAsync(
        string symbol, CancellationToken cancellationToken = default);

    Task<ContractDetails> GetContractDetailsAsync(
        string conid, CancellationToken cancellationToken = default);
}

/// <summary>Order management operations on the IBKR API.</summary>
public interface IOrderOperations
{
    Task<OrderResult> PlaceOrderAsync(
        string accountId, OrderRequest order, CancellationToken cancellationToken = default);

    Task<CancelOrderResponse> CancelOrderAsync(
        string accountId, string orderId, CancellationToken cancellationToken = default);

    Task<List<LiveOrder>> GetLiveOrdersAsync(CancellationToken cancellationToken = default);

    Task<List<Trade>> GetTradesAsync(CancellationToken cancellationToken = default);
}
```

---

## Task 3b.4 — OrderOperations (Question/Reply Loop)

### OrderOperations : IOrderOperations

**Constructor:** Takes `IIbkrOrderApi`, `ILogger<OrderOperations>`

**Fields:**
- `_orderApi`: `IIbkrOrderApi`
- `_logger`: `ILogger<OrderOperations>`
- `_accountLocks`: `ConcurrentDictionary<string, SemaphoreSlim>` — per-account serialization

**`PlaceOrderAsync`:**
1. Get or create `SemaphoreSlim(1,1)` for the account ID
2. Acquire semaphore
3. Convert `OrderRequest` to `OrderWireModel` and wrap in `OrdersPayload`
4. POST to order endpoint
5. Enter question/reply loop (max 20 iterations):
   - If response has `Message` → it's a question
   - Log question text at Warning level: `"IBKR order question auto-confirmed: {message}"`
   - POST reply with `confirmed: true`
   - If response has `OrderId` → return `OrderResult`
6. If max iterations exceeded → throw `InvalidOperationException`
7. Release semaphore in `finally` block

**Other methods:** `CancelOrderAsync`, `GetLiveOrdersAsync`, `GetTradesAsync` — simple pass-through to the Refit interface, no special logic.

---

## Task 3b.5 — IIbkrClient Facade

### IIbkrClient

```csharp
/// <summary>
/// Unified client for the IBKR API. The single entry point for consumers.
/// </summary>
public interface IIbkrClient : IAsyncDisposable
{
    IPortfolioOperations Portfolio { get; }
    IContractOperations Contracts { get; }
    IOrderOperations Orders { get; }
}
```

### IbkrClient

```csharp
public class IbkrClient : IIbkrClient
{
    private readonly ISessionManager _sessionManager;

    public IbkrClient(
        IPortfolioOperations portfolio,
        IContractOperations contracts,
        IOrderOperations orders,
        ISessionManager sessionManager)
    {
        Portfolio = portfolio;
        Contracts = contracts;
        Orders = orders;
        _sessionManager = sessionManager;
    }

    public IPortfolioOperations Portfolio { get; }
    public IContractOperations Contracts { get; }
    public IOrderOperations Orders { get; }

    public async ValueTask DisposeAsync()
    {
        await _sessionManager.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
```

### PortfolioOperations

Simple wrapper over `IIbkrPortfolioApi`:
```csharp
public class PortfolioOperations : IPortfolioOperations
{
    private readonly IIbkrPortfolioApi _api;

    public PortfolioOperations(IIbkrPortfolioApi api) => _api = api;

    public Task<List<Account>> GetAccountsAsync(CancellationToken cancellationToken)
        => _api.GetAccountsAsync();
}
```

### ContractOperations

Simple wrapper over `IIbkrContractApi`:
```csharp
public class ContractOperations : IContractOperations
{
    private readonly IIbkrContractApi _api;

    public ContractOperations(IIbkrContractApi api) => _api = api;

    public Task<List<ContractSearchResult>> SearchBySymbolAsync(
        string symbol, CancellationToken cancellationToken)
        => _api.SearchBySymbolAsync(symbol);

    public Task<ContractDetails> GetContractDetailsAsync(
        string conid, CancellationToken cancellationToken)
        => _api.GetContractDetailsAsync(conid);
}
```

---

## Task 3b.6 — DI Wiring Update

### ServiceCollectionExtensions Changes

Replace `AddIbkrClient<TApi>` with:

```csharp
public static IServiceCollection AddIbkrClient(
    this IServiceCollection services,
    IbkrOAuthCredentials credentials,
    IbkrClientOptions? options = null)
```

Registers:
- All existing infrastructure (LST client, token provider, session manager, tickle timer, rate limiters, resilience)
- `IIbkrPortfolioApi` — Refit client through consumer pipeline
- `IIbkrContractApi` — Refit client through consumer pipeline
- `IIbkrOrderApi` — Refit client through consumer pipeline
- `IPortfolioOperations` → `PortfolioOperations`
- `IContractOperations` → `ContractOperations`
- `IOrderOperations` → `OrderOperations`
- `IIbkrClient` → `IbkrClient`

All consumer-facing Refit interfaces go through the full pipeline (TokenRefreshHandler → ResilienceHandler → rate limiting → signing).

The old generic `AddIbkrClient<TApi>` method is removed — `IIbkrClient` replaces it as the consumer entry point.

---

## Task 3b.7 — Integration Tests

### WireMock Tests

**Test 1: Order submission — direct confirmation (no questions)**
- Mock `/iserver/account/{id}/orders` → 200 with order confirmation
- Assert: `PlaceOrderAsync` returns `OrderResult` with order ID

**Test 2: Order submission — question/reply auto-confirm**
- First POST → 200 with question response
- Mock `/iserver/reply/{id}` → 200 with order confirmation
- Assert: `PlaceOrderAsync` returns `OrderResult`, question was auto-confirmed

**Test 3: Order cancellation**
- Mock `DELETE /iserver/account/{id}/order/{orderId}` → 200
- Assert: `CancelOrderAsync` returns result

**Test 4: Contract search**
- Mock `/iserver/secdef/search?symbol=AAPL` → 200 with results
- Assert: `SearchBySymbolAsync` returns contract list

**Test 5: IIbkrClient facade integration**
- Wire up full pipeline with WireMock
- Use `client.Portfolio.GetAccountsAsync()` → works
- Use `client.Contracts.SearchBySymbolAsync()` → works
- Assert: facade correctly delegates to underlying Refit interfaces

### Paper Account E2E Test (conditional on IBKR_CONSUMER_KEY)

- Load credentials, wire full pipeline
- `client.Portfolio.GetAccountsAsync()` → verify account
- `client.Contracts.SearchBySymbolAsync("AAPL")` → verify conid returned

Note: We do NOT place a real order in the E2E test — contract search is sufficient to prove the facade and pipeline work end-to-end. Order placement against the paper account can be tested manually.

---

## Project Structure (New/Modified Files)

### New Files

```
src/IbkrConduit/
  Client/
    IIbkrClient.cs
    IbkrClient.cs
    IPortfolioOperations.cs
    PortfolioOperations.cs
    IContractOperations.cs
    ContractOperations.cs
    IOrderOperations.cs
    OrderOperations.cs
  Contracts/
    IIbkrContractApi.cs
    IIbkrContractApiModels.cs
  Orders/
    IIbkrOrderApi.cs
    IIbkrOrderApiModels.cs

tests/IbkrConduit.Tests.Unit/
  Orders/
    OrderOperationsTests.cs
  Client/
    IbkrClientTests.cs

tests/IbkrConduit.Tests.Integration/
  Orders/
    OrderManagementTests.cs
```

### Modified Files

```
src/IbkrConduit/Http/ServiceCollectionExtensions.cs — new registration, remove generic TApi
docs/future-enhancements.md — already created
```

---

## Dependency Graph

```
Task 3b.1 (contracts)    Task 3b.2 (orders)    Task 3b.3 (operations interfaces)
         │                       │                        │
         └───────────────────────┴────────────────────────┘
                                 │
                                 ▼
                        Task 3b.4 (OrderOperations)
                                 │
                                 ▼
                        Task 3b.5 (IIbkrClient facade)
                                 │
                                 ▼
                        Task 3b.6 (DI wiring)
                                 │
                                 ▼
                        Task 3b.7 (integration tests)
```

**Parallel opportunities:** Tasks 3b.1, 3b.2, and 3b.3 are independent.
