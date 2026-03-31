# Milestone 4 — Portfolio + Market Data

**Date:** 2026-03-31
**Status:** Draft
**Goal:** Expand the `IIbkrClient` facade with full portfolio data and market data, with transparent pre-flight handling for snapshots.

---

## Scope

M4 adds read-only query operations for portfolio data and market data to the `IIbkrClient` facade. After M4:

1. `client.Portfolio` — expanded: positions, summary, ledger, allocation, account info, performance, transaction history, cache invalidation
2. `client.MarketData` — new: snapshots (with transparent pre-flight retry), historical OHLCV bars
3. `MarketDataFields` — static constants for all 110 documented IBKR field IDs

### Deferred

- **Sub-accounts** — FA/IBroker tiered account structures. Added to `docs/future-enhancements.md`.
- **WebSocket streaming market data** — M5 scope.
- **Conid caching** — consumers cache conids themselves.
- **Regulatory snapshots** — incur per-request fees ($0.01 USD). Not included to avoid accidental charges.

### IbkrClientOptions Addition

```csharp
/// <summary>
/// How long a conid stays in the pre-flight cache before a fresh pre-flight is required.
/// Default is 5 minutes.
/// </summary>
public TimeSpan PreflightCacheDuration { get; init; } = TimeSpan.FromMinutes(5);
```

### NuGet Dependency

- `Microsoft.Extensions.Caching.Memory` — for `MemoryCache` used in pre-flight tracking

---

## Architecture

### Expanded Facade

```
client.Portfolio    — 11 methods (was 1)
client.MarketData   — 2 methods (new)
client.Contracts    — unchanged
client.Orders       — unchanged
```

### Pre-flight Handling

`MarketDataOperations.GetSnapshotAsync`:
1. Call snapshot endpoint
2. If response is empty/partial (no field data for requested conids): wait 500ms, retry once
3. Track pre-flighted conids in a `MemoryCache` with configurable expiration (`IbkrClientOptions.PreflightCacheDuration`, default 5 minutes) — skip retry on subsequent calls for the same conid within the cache window
4. Return whatever the second call returns

The cache duration balances two concerns: long enough to avoid redundant pre-flights during rapid polling, short enough that a request after a quiet period triggers a fresh pre-flight to re-establish the data stream.

Pre-flight state is per `MarketDataOperations` instance (scoped to the DI lifetime, which is singleton per tenant).

---

## Task 4.1 — Portfolio Refit Expansion + Models

### IIbkrPortfolioApi (expanded)

Add to existing interface:

```csharp
[Get("/v1/api/portfolio/{accountId}/positions/{page}")]
Task<List<Position>> GetPositionsAsync(
    string accountId, int page = 0,
    [Query] string? model = null, [Query] string? sort = null,
    [Query] string? direction = null, [Query] string? period = null,
    CancellationToken cancellationToken = default);

[Get("/v1/api/portfolio/{accountId}/summary")]
Task<Dictionary<string, AccountSummaryEntry>> GetAccountSummaryAsync(
    string accountId, CancellationToken cancellationToken = default);

[Get("/v1/api/portfolio/{accountId}/ledger")]
Task<Dictionary<string, LedgerEntry>> GetLedgerAsync(
    string accountId, CancellationToken cancellationToken = default);

[Get("/v1/api/portfolio/{accountId}/meta")]
Task<AccountInfo> GetAccountInfoAsync(
    string accountId, CancellationToken cancellationToken = default);

[Get("/v1/api/portfolio/{accountId}/allocation")]
Task<AccountAllocation> GetAccountAllocationAsync(
    string accountId, CancellationToken cancellationToken = default);

[Get("/v1/api/portfolio/{accountId}/position/{conid}")]
Task<List<Position>> GetPositionByConidAsync(
    string accountId, string conid, CancellationToken cancellationToken = default);

[Get("/v1/api/portfolio/positions/{conid}")]
Task<PositionContractInfo> GetPositionAndContractInfoAsync(
    string conid, CancellationToken cancellationToken = default);

[Post("/v1/api/portfolio/{accountId}/positions/invalidate")]
Task InvalidatePortfolioCacheAsync(
    string accountId, CancellationToken cancellationToken = default);

[Post("/v1/api/pa/performance")]
Task<AccountPerformance> GetAccountPerformanceAsync(
    [Body] PerformanceRequest request, CancellationToken cancellationToken = default);

[Post("/v1/api/pa/transactions")]
Task<TransactionHistory> GetTransactionHistoryAsync(
    [Body] TransactionHistoryRequest request, CancellationToken cancellationToken = default);
```

### Response Models (IIbkrPortfolioApiModels.cs expanded)

All models use immutable positional records with `JsonExtensionData` for undocumented fields.

**Position:**
```csharp
public record Position(
    [property: JsonPropertyName("acctId")] string AccountId,
    [property: JsonPropertyName("conid")] int Conid,
    [property: JsonPropertyName("contractDesc")] string ContractDescription,
    [property: JsonPropertyName("position")] decimal Quantity,
    [property: JsonPropertyName("mktPrice")] decimal MarketPrice,
    [property: JsonPropertyName("mktValue")] decimal MarketValue,
    [property: JsonPropertyName("avgCost")] decimal AverageCost,
    [property: JsonPropertyName("avgPrice")] decimal AveragePrice,
    [property: JsonPropertyName("realizedPnl")] decimal RealizedPnl,
    [property: JsonPropertyName("unrealizedPnl")] decimal UnrealizedPnl,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("assetClass")] string AssetClass,
    [property: JsonPropertyName("sector")] string? Sector,
    [property: JsonPropertyName("ticker")] string Ticker,
    [property: JsonPropertyName("multiplier")] decimal? Multiplier,
    [property: JsonPropertyName("isUS")] bool? IsUs)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
```

**AccountSummaryEntry:**
```csharp
public record AccountSummaryEntry(
    [property: JsonPropertyName("amount")] decimal? Amount,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("isNull")] bool IsNull,
    [property: JsonPropertyName("timestamp")] long? Timestamp,
    [property: JsonPropertyName("value")] string? Value)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
```

The summary endpoint returns `Dictionary<string, AccountSummaryEntry>` where keys are dynamic field names like `"netliquidationvalue"`, `"totalcashvalue"`, `"netliquidationvalue-c"`, `"netliquidationvalue-s"`, etc.

**LedgerEntry:**
```csharp
public record LedgerEntry(
    [property: JsonPropertyName("cashbalance")] decimal CashBalance,
    [property: JsonPropertyName("netliquidationvalue")] decimal NetLiquidationValue,
    [property: JsonPropertyName("settledcash")] decimal SettledCash,
    [property: JsonPropertyName("exchangerate")] decimal ExchangeRate,
    [property: JsonPropertyName("stockmarketvalue")] decimal StockMarketValue,
    [property: JsonPropertyName("corporatebondsmarketvalue")] decimal CorporateBondsMarketValue,
    [property: JsonPropertyName("warrantsmarketvalue")] decimal WarrantsMarketValue,
    [property: JsonPropertyName("futuremarketvalue")] decimal FutureMarketValue,
    [property: JsonPropertyName("commoditymarketvalue")] decimal CommodityMarketValue)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
```

The ledger endpoint returns `Dictionary<string, LedgerEntry>` where keys are currency codes like `"USD"`, `"EUR"`, `"BASE"`.

**AccountInfo, AccountAllocation, PositionContractInfo, AccountPerformance, TransactionHistory** — defined with the most common fields from the IBKR docs, plus `JsonExtensionData`. Exact fields will be refined during implementation based on actual API responses.

**Request models:**
```csharp
public record PerformanceRequest(
    [property: JsonPropertyName("acctIds")] List<string> AccountIds,
    [property: JsonPropertyName("period")] string Period);

public record TransactionHistoryRequest(
    [property: JsonPropertyName("acctIds")] List<string> AccountIds,
    [property: JsonPropertyName("conids")] List<string> Conids,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("days")] int? Days);
```

---

## Task 4.2 — MarketData Refit Interface + Models

### IIbkrMarketDataApi

```csharp
public interface IIbkrMarketDataApi
{
    [Get("/v1/api/iserver/marketdata/snapshot")]
    Task<List<MarketDataSnapshotRaw>> GetSnapshotAsync(
        [Query] string conids, [Query] string fields,
        CancellationToken cancellationToken = default);

    [Get("/v1/api/iserver/marketdata/history")]
    Task<HistoricalDataResponse> GetHistoryAsync(
        [Query] string conid, [Query] string period,
        [Query] string bar, [Query] bool? outsideRth = null,
        CancellationToken cancellationToken = default);
}
```

### MarketDataSnapshotRaw (wire model)

The raw IBKR response uses numeric string keys:
```json
{"conid": 265598, "31": "193.18", "84": "193.06", "86": "193.14", "_updated": 1702334859712}
```

```csharp
public record MarketDataSnapshotRaw(
    [property: JsonPropertyName("conid")] int Conid,
    [property: JsonPropertyName("conidEx")] string? ConidEx,
    [property: JsonPropertyName("_updated")] long? Updated,
    [property: JsonPropertyName("server_id")] string? ServerId,
    [property: JsonPropertyName("6509")] string? MarketDataAvailability)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Fields { get; init; }
}
```

### MarketDataSnapshot (consumer-facing model)

Mapped from `MarketDataSnapshotRaw` by `MarketDataOperations`. The 20 most common fields as typed properties:

```csharp
public record MarketDataSnapshot
{
    public int Conid { get; init; }
    public long? Updated { get; init; }
    public string? MarketDataAvailability { get; init; }
    public string? LastPrice { get; init; }
    public string? BidPrice { get; init; }
    public string? AskPrice { get; init; }
    public string? BidSize { get; init; }
    public string? AskSize { get; init; }
    public string? LastSize { get; init; }
    public string? High { get; init; }
    public string? Low { get; init; }
    public string? Open { get; init; }
    public string? Close { get; init; }
    public string? PriorClose { get; init; }
    public string? Volume { get; init; }
    public string? VolumeLong { get; init; }
    public string? Change { get; init; }
    public string? ChangePercent { get; init; }
    public string? MarketValue { get; init; }
    public string? AvgPrice { get; init; }
    public string? UnrealizedPnl { get; init; }
    public string? RealizedPnl { get; init; }
    public string? DailyPnl { get; init; }
    public string? ImpliedVolatility { get; init; }

    /// <summary>
    /// All fields from the raw response keyed by field ID string.
    /// Use <see cref="MarketDataFields"/> constants for field lookup.
    /// </summary>
    public IReadOnlyDictionary<string, string>? AllFields { get; init; }
}
```

Note: IBKR returns all values as strings (including numeric values). The typed properties use `string?` to match the wire format. Consumers parse to decimal/int as needed.

### MarketDataFields (all 110 constants)

```csharp
public static class MarketDataFields
{
    public const string LastPrice = "31";
    public const string Symbol = "55";
    public const string Text = "58";
    public const string High = "70";
    public const string Low = "71";
    public const string MarketValue = "73";
    public const string AvgPrice = "74";
    public const string UnrealizedPnl = "75";
    // ... all 110 fields
    public const string BidPrice = "84";
    public const string AskSize = "85";
    public const string AskPrice = "86";
    public const string Volume = "87";
    public const string BidSize = "88";
    // etc.
}
```

### HistoricalDataResponse

```csharp
public record HistoricalDataResponse(
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("priceFactor")] int? PriceFactor,
    [property: JsonPropertyName("startTime")] string? StartTime,
    [property: JsonPropertyName("high")] string? HighStr,
    [property: JsonPropertyName("low")] string? LowStr,
    [property: JsonPropertyName("timePeriod")] string? TimePeriod,
    [property: JsonPropertyName("barLength")] int? BarLength,
    [property: JsonPropertyName("mdAvailability")] string? MdAvailability,
    [property: JsonPropertyName("mktDataDelay")] int? MktDataDelay,
    [property: JsonPropertyName("outsideRth")] bool? OutsideRth,
    [property: JsonPropertyName("volumeFactor")] int? VolumeFactor,
    [property: JsonPropertyName("priceDisplayRule")] int? PriceDisplayRule,
    [property: JsonPropertyName("priceDisplayValue")] string? PriceDisplayValue,
    [property: JsonPropertyName("negativeCapable")] bool? NegativeCapable,
    [property: JsonPropertyName("messageVersion")] int? MessageVersion,
    [property: JsonPropertyName("data")] List<HistoricalBar>? Data,
    [property: JsonPropertyName("points")] int? Points,
    [property: JsonPropertyName("travelTime")] int? TravelTime);

public record HistoricalBar(
    [property: JsonPropertyName("o")] decimal Open,
    [property: JsonPropertyName("c")] decimal Close,
    [property: JsonPropertyName("h")] decimal High,
    [property: JsonPropertyName("l")] decimal Low,
    [property: JsonPropertyName("v")] decimal Volume,
    [property: JsonPropertyName("t")] long Timestamp);
```

---

## Task 4.3 — IPortfolioOperations Expansion

Expand `IPortfolioOperations` and `PortfolioOperations` with all new methods:

```csharp
public interface IPortfolioOperations
{
    // Existing
    Task<List<Account>> GetAccountsAsync(CancellationToken cancellationToken = default);

    // New
    Task<List<Position>> GetPositionsAsync(string accountId, int page = 0,
        CancellationToken cancellationToken = default);
    Task<Dictionary<string, AccountSummaryEntry>> GetAccountSummaryAsync(string accountId,
        CancellationToken cancellationToken = default);
    Task<Dictionary<string, LedgerEntry>> GetLedgerAsync(string accountId,
        CancellationToken cancellationToken = default);
    Task<AccountInfo> GetAccountInfoAsync(string accountId,
        CancellationToken cancellationToken = default);
    Task<AccountAllocation> GetAccountAllocationAsync(string accountId,
        CancellationToken cancellationToken = default);
    Task<List<Position>> GetPositionByConidAsync(string accountId, string conid,
        CancellationToken cancellationToken = default);
    Task<PositionContractInfo> GetPositionAndContractInfoAsync(string conid,
        CancellationToken cancellationToken = default);
    Task InvalidatePortfolioCacheAsync(string accountId,
        CancellationToken cancellationToken = default);
    Task<AccountPerformance> GetAccountPerformanceAsync(List<string> accountIds, string period,
        CancellationToken cancellationToken = default);
    Task<TransactionHistory> GetTransactionHistoryAsync(List<string> accountIds,
        List<string> conids, string currency, int? days = null,
        CancellationToken cancellationToken = default);
}
```

`PortfolioOperations` — simple pass-through to Refit, constructing request objects where needed (performance, transactions).

---

## Task 4.4 — IMarketDataOperations + Pre-flight Handling

```csharp
public interface IMarketDataOperations
{
    Task<List<MarketDataSnapshot>> GetSnapshotAsync(int[] conids, string[] fields,
        CancellationToken cancellationToken = default);
    Task<HistoricalDataResponse> GetHistoryAsync(int conid, string period, string bar,
        bool? outsideRth = null, CancellationToken cancellationToken = default);
}
```

### MarketDataOperations

**Constructor:** Takes `IIbkrMarketDataApi`, `IbkrClientOptions`, `ILogger<MarketDataOperations>`

**Fields:**
- `_preflightCache`: `MemoryCache` — tracks conids that have completed pre-flight, entries expire after `IbkrClientOptions.PreflightCacheDuration`

**`GetSnapshotAsync`:**
1. Build comma-separated conids and fields strings
2. Call `_api.GetSnapshotAsync(conids, fields)`
3. Check if any requested conids have no field data in the response (pre-flight needed)
4. If pre-flight needed AND those conids are not in `_preflightCache`:
   - Add them to `_preflightCache` with absolute expiration of `_options.PreflightCacheDuration`
   - Wait 500ms
   - Retry the call
5. Map `MarketDataSnapshotRaw` → `MarketDataSnapshot` (map numeric field IDs to named properties)
6. Return mapped results

**`GetHistoryAsync`:** Simple pass-through, no special handling.

---

## Task 4.5 — IIbkrClient Facade Update

Add `IMarketDataOperations MarketData { get; }` to `IIbkrClient` and `IbkrClient`.

Update `ServiceCollectionExtensions.AddIbkrClient`:
- Register `IIbkrMarketDataApi` Refit client through consumer pipeline
- Register `IMarketDataOperations` → `MarketDataOperations` (singleton — for pre-flight conid tracking)
- Register expanded `IPortfolioOperations` → `PortfolioOperations`
- Update `IbkrClient` constructor

---

## Task 4.6 — Integration Tests

### WireMock Tests

**Test 1: Get positions**
- Mock `/portfolio/{id}/positions/0` → 200 with position array
- Assert: returns typed `Position` with correct fields

**Test 2: Get account summary**
- Mock `/portfolio/{id}/summary` → 200 with key-value object
- Assert: returns `Dictionary<string, AccountSummaryEntry>`

**Test 3: Snapshot with pre-flight**
- First call → 200 with empty conid response (no field data)
- Second call → 200 with full snapshot data
- Assert: consumer gets full data, pre-flight handled transparently

**Test 4: Historical bars**
- Mock `/iserver/marketdata/history` → 200 with OHLCV data
- Assert: returns `HistoricalDataResponse` with bars

### Paper Account E2E Test

Using `[EnvironmentFact("IBKR_CONSUMER_KEY")]`:
- `client.Portfolio.GetAccountsAsync()` → get account ID
- `client.Portfolio.GetPositionsAsync(accountId)` → verify SPY position from M3b order
- `client.Portfolio.GetAccountSummaryAsync(accountId)` → verify summary data
- `client.Portfolio.GetLedgerAsync(accountId)` → verify ledger data
- `client.MarketData.GetSnapshotAsync([spyConid], [MarketDataFields.LastPrice, ...])` → verify snapshot returns price data

---

## Project Structure (New/Modified Files)

### New Files

```
src/IbkrConduit/
  MarketData/
    IIbkrMarketDataApi.cs
    IIbkrMarketDataApiModels.cs
    MarketDataFields.cs
  Client/
    IMarketDataOperations.cs
    MarketDataOperations.cs

tests/IbkrConduit.Tests.Unit/
  MarketData/
    MarketDataOperationsTests.cs
  Portfolio/
    PortfolioOperationsTests.cs

tests/IbkrConduit.Tests.Integration/
  MarketData/
    MarketDataTests.cs
```

### Modified Files

```
src/IbkrConduit/Portfolio/IIbkrPortfolioApi.cs — add 10 new endpoints
src/IbkrConduit/Portfolio/IIbkrPortfolioApiModels.cs — add Position, AccountSummaryEntry, LedgerEntry, etc.
src/IbkrConduit/Client/IPortfolioOperations.cs — add 10 new methods
src/IbkrConduit/Client/PortfolioOperations.cs — implement 10 new methods
src/IbkrConduit/Client/IIbkrClient.cs — add MarketData property
src/IbkrConduit/Client/IbkrClient.cs — accept and expose MarketData
src/IbkrConduit/Http/ServiceCollectionExtensions.cs — register market data components
```

---

## Dependency Graph

```
Task 4.1 (portfolio expansion)    Task 4.2 (market data interface)
         │                                │
         ▼                                ▼
Task 4.3 (portfolio operations)   Task 4.4 (market data operations)
         │                                │
         └────────────┬───────────────────┘
                      ▼
              Task 4.5 (facade update)
                      │
                      ▼
              Task 4.6 (integration tests)
```

**Parallel opportunities:** Tasks 4.1/4.2 are independent. Tasks 4.3/4.4 are independent (each depends on its own Refit task). Task 4.5 depends on both. Task 4.6 is last.
