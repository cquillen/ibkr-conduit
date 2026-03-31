# Observability Milestone — Tracing, Metrics, and Structured Logging

**Date:** 2026-03-31
**Status:** Draft
**Goal:** Add production-grade observability with distributed tracing (38 spans), metrics (32 instruments), and consistent structured logging across all components — using zero external dependencies.

---

## Scope

After this milestone:

1. Consumers can subscribe to traces via `ActivitySource` named `"IbkrConduit"`
2. Consumers can subscribe to metrics via `Meter` named `"IbkrConduit"`
3. All log entries use structured fields via `LogFields` constants
4. Zero new NuGet dependencies — everything uses in-box `System.Diagnostics`

### Consumer Setup

```csharp
// Consumer adds OpenTelemetry packages to THEIR project:
services.AddOpenTelemetry()
    .WithTracing(b => b.AddSource("IbkrConduit"))
    .WithMetrics(b => b.AddMeter("IbkrConduit"));

// Logging — consumer chooses their provider:
services.AddLogging(b => b.AddSerilog());  // or AddNLog(), AddConsole(), etc.
```

Our library has zero OTel dependencies. Everything uses `System.Diagnostics.DiagnosticSource` (ActivitySource) and `System.Diagnostics.Metrics` (Meter), both in-box since .NET 6.

---

## Architecture

### Static Entry Points

```csharp
/// <summary>
/// Central diagnostics for the IbkrConduit library.
/// Consumers subscribe via OpenTelemetry or any System.Diagnostics listener.
/// </summary>
public static class IbkrConduitDiagnostics
{
    /// <summary>The ActivitySource name for tracing.</summary>
    public const string ActivitySourceName = "IbkrConduit";

    /// <summary>The Meter name for metrics.</summary>
    public const string MeterName = "IbkrConduit";

    /// <summary>ActivitySource for creating spans.</summary>
    public static ActivitySource ActivitySource { get; } = new(ActivitySourceName);

    /// <summary>Meter for creating metric instruments.</summary>
    public static Meter Meter { get; } = new(MeterName);
}
```

### Log Field Constants

```csharp
/// <summary>
/// Standard structured log field names used across all IbkrConduit components.
/// </summary>
public static class LogFields
{
    public const string TenantId = "ibkr.tenant_id";
    public const string AccountId = "ibkr.account_id";
    public const string Conid = "ibkr.conid";
    public const string OrderId = "ibkr.order_id";
    public const string Symbol = "ibkr.symbol";
    public const string Endpoint = "ibkr.endpoint";
    public const string Method = "ibkr.method";
    public const string StatusCode = "ibkr.status_code";
    public const string DurationMs = "ibkr.duration_ms";
    public const string QuestionCount = "ibkr.question_count";
    public const string PollCount = "ibkr.poll_count";
    public const string Trigger = "ibkr.trigger";
    public const string Topic = "ibkr.topic";
    public const string QueryId = "ibkr.query_id";
    public const string Attempt = "ibkr.attempt";
    public const string Cached = "ibkr.cached";
    public const string PreflightNeeded = "ibkr.preflight_needed";
    public const string Side = "ibkr.side";
    public const string OrderType = "ibkr.order_type";
    public const string ErrorCode = "ibkr.error_code";
}
```

---

## Task O.1 — IbkrConduitDiagnostics + LogFields

Create:
- `src/IbkrConduit/Diagnostics/IbkrConduitDiagnostics.cs`
- `src/IbkrConduit/Diagnostics/LogFields.cs`

Both are static classes with no dependencies. Unit test verifies the constants are set and the ActivitySource/Meter can be created.

---

## Task O.2 — Tracing (Spans)

Add `Activity` spans to all key operations. Pattern:

```csharp
using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Xxx.Yyy");
activity?.SetTag("ibkr.account_id", accountId);
// ... do work ...
activity?.SetTag("ibkr.status_code", (int)response.StatusCode);
```

### Complete Span List (38 spans)

**OAuth/Auth (3):**

| Component | Activity Name | Attributes |
|---|---|---|
| LiveSessionTokenClient.GetLiveSessionTokenAsync | `IbkrConduit.OAuth.AcquireLst` | tenant_id |
| OAuthCrypto.DecryptAccessTokenSecret | `IbkrConduit.OAuth.DecryptSecret` | |
| OAuthCrypto.GenerateDhKeyPair | `IbkrConduit.OAuth.DhKeyPair` | |

**Session Lifecycle (6):**

| Component | Activity Name | Attributes |
|---|---|---|
| SessionManager.EnsureInitializedAsync | `IbkrConduit.Session.Initialize` | tenant_id |
| SessionManager.ReauthenticateAsync | `IbkrConduit.Session.Reauthenticate` | tenant_id, trigger |
| SessionManager.DisposeAsync | `IbkrConduit.Session.Shutdown` | tenant_id |
| SessionTokenProvider.GetLiveSessionTokenAsync | `IbkrConduit.Session.GetToken` | cached |
| SessionTokenProvider.RefreshAsync | `IbkrConduit.Session.RefreshToken` | |
| TickleTimer tick | `IbkrConduit.Session.Tickle` | authenticated |

**HTTP Pipeline (5):**

| Component | Activity Name | Attributes |
|---|---|---|
| OAuthSigningHandler.SendAsync | `IbkrConduit.Http.Request` | method, url, status_code |
| TokenRefreshHandler retry | `IbkrConduit.Http.TokenRefreshRetry` | original_status_code |
| ResilienceHandler retry | `IbkrConduit.Http.ResilienceRetry` | attempt, status_code |
| GlobalRateLimitingHandler | `IbkrConduit.Http.GlobalRateLimit.Wait` | wait_ms |
| EndpointRateLimitingHandler | `IbkrConduit.Http.EndpointRateLimit.Wait` | endpoint, wait_ms |

**Portfolio Operations (11):**

| Component | Activity Name | Attributes |
|---|---|---|
| GetAccountsAsync | `IbkrConduit.Portfolio.GetAccounts` | |
| GetPositionsAsync | `IbkrConduit.Portfolio.GetPositions` | account_id, page |
| GetAccountSummaryAsync | `IbkrConduit.Portfolio.GetAccountSummary` | account_id |
| GetLedgerAsync | `IbkrConduit.Portfolio.GetLedger` | account_id |
| GetAccountInfoAsync | `IbkrConduit.Portfolio.GetAccountInfo` | account_id |
| GetAccountAllocationAsync | `IbkrConduit.Portfolio.GetAllocation` | account_id |
| GetPositionByConidAsync | `IbkrConduit.Portfolio.GetPositionByConid` | account_id, conid |
| GetPositionAndContractInfoAsync | `IbkrConduit.Portfolio.GetPositionContractInfo` | conid |
| InvalidatePortfolioCacheAsync | `IbkrConduit.Portfolio.InvalidateCache` | account_id |
| GetAccountPerformanceAsync | `IbkrConduit.Portfolio.GetPerformance` | period |
| GetTransactionHistoryAsync | `IbkrConduit.Portfolio.GetTransactionHistory` | currency, days |

**Contract Operations (2):**

| Component | Activity Name | Attributes |
|---|---|---|
| SearchBySymbolAsync | `IbkrConduit.Contract.SearchBySymbol` | symbol |
| GetContractDetailsAsync | `IbkrConduit.Contract.GetDetails` | conid |

**Order Operations (4):**

| Component | Activity Name | Attributes |
|---|---|---|
| PlaceOrderAsync | `IbkrConduit.Order.Place` | account_id, conid, side, order_type, question_count |
| CancelOrderAsync | `IbkrConduit.Order.Cancel` | account_id, order_id |
| GetLiveOrdersAsync | `IbkrConduit.Order.GetLiveOrders` | |
| GetTradesAsync | `IbkrConduit.Order.GetTrades` | |

**Market Data Operations (2):**

| Component | Activity Name | Attributes |
|---|---|---|
| GetSnapshotAsync | `IbkrConduit.MarketData.Snapshot` | conid_count, field_count, preflight_needed |
| GetHistoryAsync | `IbkrConduit.MarketData.History` | conid, period, bar |

**WebSocket Streaming (4):**

| Component | Activity Name | Attributes |
|---|---|---|
| ConnectAsync | `IbkrConduit.WebSocket.Connect` | url |
| DisconnectAsync | `IbkrConduit.WebSocket.Disconnect` | reason |
| Reconnect | `IbkrConduit.WebSocket.Reconnect` | trigger |
| SubscribeTopicAsync | `IbkrConduit.WebSocket.Subscribe` | topic |

**Flex Web Service (3):**

| Component | Activity Name | Attributes |
|---|---|---|
| ExecuteQueryAsync | `IbkrConduit.Flex.ExecuteQuery` | query_id, poll_count |
| SendRequestAsync | `IbkrConduit.Flex.SendRequest` | query_id |
| GetStatementAsync | `IbkrConduit.Flex.GetStatement` | reference_code, attempt |

---

## Task O.3 — Metrics

Add metric instruments to all measurable operations. Pattern:

```csharp
private static readonly Counter<long> _requestCount =
    IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.http.request.count");

// In method:
_requestCount.Add(1, new KeyValuePair<string, object?>("endpoint", endpoint));
```

### Complete Metrics List (32 instruments)

**HTTP (5):**

| Metric | Type | Tags |
|---|---|---|
| `ibkr.conduit.http.request.duration` | Histogram\<double\> | endpoint, method, status_code |
| `ibkr.conduit.http.request.count` | Counter\<long\> | endpoint, method, status_code |
| `ibkr.conduit.http.active_requests` | UpDownCounter\<long\> | |
| `ibkr.conduit.http.status_429.count` | Counter\<long\> | endpoint |
| `ibkr.conduit.http.retry.count` | Counter\<long\> | endpoint, attempt |

**Rate Limiting (5):**

| Metric | Type | Tags |
|---|---|---|
| `ibkr.conduit.ratelimiter.global.wait_duration` | Histogram\<double\> | |
| `ibkr.conduit.ratelimiter.global.queue_depth` | ObservableGauge\<int\> | |
| `ibkr.conduit.ratelimiter.global.rejected.count` | Counter\<long\> | |
| `ibkr.conduit.ratelimiter.endpoint.wait_duration` | Histogram\<double\> | endpoint |
| `ibkr.conduit.ratelimiter.endpoint.rejected.count` | Counter\<long\> | endpoint |

**Session (6):**

| Metric | Type | Tags |
|---|---|---|
| `ibkr.conduit.session.active` | UpDownCounter\<long\> | |
| `ibkr.conduit.session.initialize.duration` | Histogram\<double\> | |
| `ibkr.conduit.session.refresh.count` | Counter\<long\> | trigger |
| `ibkr.conduit.session.refresh.duration` | Histogram\<double\> | |
| `ibkr.conduit.session.tickle.count` | Counter\<long\> | success |
| `ibkr.conduit.session.tickle.failure.count` | Counter\<long\> | |

**Orders (4):**

| Metric | Type | Tags |
|---|---|---|
| `ibkr.conduit.order.submission.duration` | Histogram\<double\> | |
| `ibkr.conduit.order.submission.count` | Counter\<long\> | side, order_type |
| `ibkr.conduit.order.cancel.count` | Counter\<long\> | |
| `ibkr.conduit.order.question.count` | Counter\<long\> | |

**Market Data (5):**

| Metric | Type | Tags |
|---|---|---|
| `ibkr.conduit.marketdata.snapshot.duration` | Histogram\<double\> | preflight |
| `ibkr.conduit.marketdata.snapshot.count` | Counter\<long\> | |
| `ibkr.conduit.marketdata.snapshot.preflight.count` | Counter\<long\> | |
| `ibkr.conduit.marketdata.history.duration` | Histogram\<double\> | |
| `ibkr.conduit.marketdata.history.count` | Counter\<long\> | |

**WebSocket (5):**

| Metric | Type | Tags |
|---|---|---|
| `ibkr.conduit.websocket.connection_state` | ObservableGauge\<int\> | |
| `ibkr.conduit.websocket.messages.received` | Counter\<long\> | topic |
| `ibkr.conduit.websocket.messages.sent` | Counter\<long\> | |
| `ibkr.conduit.websocket.reconnect.count` | Counter\<long\> | trigger |
| `ibkr.conduit.websocket.heartbeat.count` | Counter\<long\> | |

**Flex (4 — note: was listed as 4 in exhaustive list but I had miscounted earlier, correcting the total):**

| Metric | Type | Tags |
|---|---|---|
| `ibkr.conduit.flex.query.duration` | Histogram\<double\> | |
| `ibkr.conduit.flex.query.count` | Counter\<long\> | |
| `ibkr.conduit.flex.poll.count` | Counter\<long\> | |
| `ibkr.conduit.flex.error.count` | Counter\<long\> | error_code |

**Total: 34 instruments** (corrected from 32 — added `http.active_requests` and `ratelimiter.global.queue_depth`)

---

## Task O.4 — Structured Logging Audit

Ensure every component:

1. Uses `[LoggerMessage]` source generation (partial methods, not string interpolation)
2. Includes relevant structured fields from `LogFields` constants
3. Uses appropriate log levels:
   - `Trace` — per-tick, per-message details (heartbeat sent, WebSocket message received)
   - `Debug` — operation start/completion (session init, order submitted, snapshot requested)
   - `Information` — lifecycle events (session ready, WebSocket connected, token refreshed)
   - `Warning` — recoverable issues (question auto-confirmed, tickle failure triggering re-auth, pre-flight retry)
   - `Error` — failures (token acquisition failed, WebSocket disconnect, Flex query error)
   - `Critical` — unrecoverable (not expected in normal operation)
4. Uses log scopes for correlation where applicable (order submission scope carries order ID)

### Components to audit:

- `LiveSessionTokenClient` — add structured fields for LST flow
- `SessionManager` — already has some logging, ensure consistent fields
- `SessionTokenProvider` — add logging for cache hits/misses
- `TickleTimer` — already has logging, add structured fields
- `OAuthSigningHandler` — add request/response logging
- `TokenRefreshHandler` — add retry logging
- `ResilienceHandler` — add retry logging
- `GlobalRateLimitingHandler` — add wait/reject logging
- `EndpointRateLimitingHandler` — add wait/reject logging
- `OrderOperations` — already has question logging, add structured fields
- `MarketDataOperations` — add pre-flight logging with structured fields
- `IbkrWebSocketClient` — add connect/disconnect/message logging
- `StreamingOperations` — add subscribe logging
- `FlexClient` — add request/poll/retrieve logging
- `SessionLifecycleNotifier` — add notification logging

---

## Task O.5 — Tests + Documentation

### Unit Tests

- Verify `IbkrConduitDiagnostics.ActivitySource` and `Meter` are created
- Verify `LogFields` constants are non-empty strings
- Verify spans are emitted: use `ActivityListener` to capture activities from key operations
- Verify metrics are recorded: use `MeterListener` to capture metric values

### Documentation

Add a `docs/observability.md` guide covering:
- How to subscribe to traces (`AddSource("IbkrConduit")`)
- How to subscribe to metrics (`AddMeter("IbkrConduit")`)
- Complete list of span names and their attributes
- Complete list of metric names and their tags
- How to configure logging providers (Serilog, NLog, Console)
- Example Grafana dashboard queries
- Example alert rules (429 count > 0, session refresh duration > 30s)

---

## Project Structure (New/Modified Files)

### New Files

```
src/IbkrConduit/
  Diagnostics/
    IbkrConduitDiagnostics.cs
    LogFields.cs

docs/
  observability.md

tests/IbkrConduit.Tests.Unit/
  Diagnostics/
    DiagnosticsTests.cs
```

### Modified Files (all components get spans + metrics + logging audit)

```
src/IbkrConduit/Auth/LiveSessionTokenClient.cs
src/IbkrConduit/Auth/OAuthSigningHandler.cs
src/IbkrConduit/Auth/SessionTokenProvider.cs
src/IbkrConduit/Session/SessionManager.cs
src/IbkrConduit/Session/TickleTimer.cs
src/IbkrConduit/Session/TokenRefreshHandler.cs
src/IbkrConduit/Session/SessionLifecycleNotifier.cs
src/IbkrConduit/Http/GlobalRateLimitingHandler.cs
src/IbkrConduit/Http/EndpointRateLimitingHandler.cs
src/IbkrConduit/Http/ResilienceHandler.cs
src/IbkrConduit/Client/OrderOperations.cs
src/IbkrConduit/Client/MarketDataOperations.cs
src/IbkrConduit/Client/PortfolioOperations.cs
src/IbkrConduit/Client/ContractOperations.cs
src/IbkrConduit/Client/StreamingOperations.cs
src/IbkrConduit/Streaming/IbkrWebSocketClient.cs
src/IbkrConduit/Flex/FlexClient.cs
```

---

## Dependency Graph

```
Task O.1 (IbkrConduitDiagnostics + LogFields)
         │
         ├──────────┬──────────┐
         ▼          ▼          ▼
Task O.2 (spans) Task O.3 (metrics) Task O.4 (logging audit)
         │          │          │
         └──────────┴──────────┘
                    │
                    ▼
            Task O.5 (tests + docs)
```

**Parallel opportunities:** O.2, O.3, O.4 are independent (all depend on O.1). O.5 is last.
