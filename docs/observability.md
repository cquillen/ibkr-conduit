# Observability Guide

IbkrConduit includes built-in observability via `System.Diagnostics` (tracing) and `System.Diagnostics.Metrics` (metrics). No additional NuGet dependencies are required in the library itself.

## Quick Start

Add OpenTelemetry packages to **your** project and subscribe to IbkrConduit's sources:

```csharp
services.AddOpenTelemetry()
    .WithTracing(b => b.AddSource("IbkrConduit"))
    .WithMetrics(b => b.AddMeter("IbkrConduit"));
```

For logging, configure your preferred provider:

```csharp
services.AddLogging(b => b.AddConsole());
// or AddSerilog(), AddNLog(), etc.
```

## Tracing (ActivitySource)

Subscribe to the `"IbkrConduit"` ActivitySource to receive distributed traces.

### Span List

| Component | Span Name | Attributes |
|---|---|---|
| **OAuth/Auth** | | |
| LiveSessionTokenClient | `IbkrConduit.OAuth.AcquireLst` | |
| OAuthCrypto.DecryptAccessTokenSecret | `IbkrConduit.OAuth.DecryptSecret` | |
| OAuthCrypto.GenerateDhKeyPair | `IbkrConduit.OAuth.DhKeyPair` | |
| **Session Lifecycle** | | |
| SessionManager.EnsureInitializedAsync | `IbkrConduit.Session.Initialize` | |
| SessionManager.ReauthenticateAsync | `IbkrConduit.Session.Reauthenticate` | |
| SessionManager.DisposeAsync | `IbkrConduit.Session.Shutdown` | |
| SessionTokenProvider.GetLiveSessionTokenAsync | `IbkrConduit.Session.GetToken` | cached |
| SessionTokenProvider.RefreshAsync | `IbkrConduit.Session.RefreshToken` | |
| TickleTimer tick | `IbkrConduit.Session.Tickle` | authenticated |
| **HTTP Pipeline** | | |
| OAuthSigningHandler.SendAsync | `IbkrConduit.Http.Request` | method, url, status_code |
| TokenRefreshHandler retry | `IbkrConduit.Http.TokenRefreshRetry` | original_status_code |
| ResilienceHandler retry | `IbkrConduit.Http.ResilienceRetry` | attempt, status_code |
| GlobalRateLimitingHandler | `IbkrConduit.Http.GlobalRateLimit.Wait` | wait_ms |
| EndpointRateLimitingHandler | `IbkrConduit.Http.EndpointRateLimit.Wait` | endpoint, wait_ms |
| **Portfolio** | | |
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
| **Contracts** | | |
| SearchBySymbolAsync | `IbkrConduit.Contract.SearchBySymbol` | symbol |
| GetContractDetailsAsync | `IbkrConduit.Contract.GetDetails` | conid |
| **Orders** | | |
| PlaceOrderAsync | `IbkrConduit.Order.Place` | account_id, conid, side, order_type, question_count |
| CancelOrderAsync | `IbkrConduit.Order.Cancel` | account_id, order_id |
| GetLiveOrdersAsync | `IbkrConduit.Order.GetLiveOrders` | |
| GetTradesAsync | `IbkrConduit.Order.GetTrades` | |
| **Market Data** | | |
| GetSnapshotAsync | `IbkrConduit.MarketData.Snapshot` | conid_count, field_count, preflight_needed |
| GetHistoryAsync | `IbkrConduit.MarketData.History` | conid, period, bar |
| **WebSocket** | | |
| ConnectAsync | `IbkrConduit.WebSocket.Connect` | url |
| DisconnectAsync | `IbkrConduit.WebSocket.Disconnect` | |
| Reconnect | `IbkrConduit.WebSocket.Reconnect` | trigger |
| SubscribeTopicAsync | `IbkrConduit.WebSocket.Subscribe` | topic |
| **Flex** | | |
| ExecuteQueryAsync | `IbkrConduit.Flex.ExecuteQuery` | query_id, poll_count |
| SendRequestAsync | `IbkrConduit.Flex.SendRequest` | query_id |
| PollForStatementAsync | `IbkrConduit.Flex.GetStatement` | reference_code, attempt |

## Metrics (Meter)

Subscribe to the `"IbkrConduit"` Meter to receive metrics.

### HTTP Metrics

| Metric | Type | Tags |
|---|---|---|
| `ibkr.conduit.http.request.duration` | Histogram (ms) | endpoint, method, status_code |
| `ibkr.conduit.http.request.count` | Counter | endpoint, method, status_code |
| `ibkr.conduit.http.active_requests` | UpDownCounter | |
| `ibkr.conduit.http.status_429.count` | Counter | endpoint |
| `ibkr.conduit.http.retry.count` | Counter | endpoint, attempt |

### Rate Limiting Metrics

| Metric | Type | Tags |
|---|---|---|
| `ibkr.conduit.ratelimiter.global.wait_duration` | Histogram (ms) | |
| `ibkr.conduit.ratelimiter.global.queue_depth` | ObservableGauge | |
| `ibkr.conduit.ratelimiter.global.rejected.count` | Counter | |
| `ibkr.conduit.ratelimiter.endpoint.wait_duration` | Histogram (ms) | endpoint |
| `ibkr.conduit.ratelimiter.endpoint.rejected.count` | Counter | endpoint |

### Session Metrics

| Metric | Type | Tags |
|---|---|---|
| `ibkr.conduit.session.active` | UpDownCounter | |
| `ibkr.conduit.session.initialize.duration` | Histogram (ms) | |
| `ibkr.conduit.session.refresh.count` | Counter | trigger |
| `ibkr.conduit.session.refresh.duration` | Histogram (ms) | |
| `ibkr.conduit.session.tickle.count` | Counter | success |
| `ibkr.conduit.session.tickle.failure.count` | Counter | |

### Order Metrics

| Metric | Type | Tags |
|---|---|---|
| `ibkr.conduit.order.submission.duration` | Histogram (ms) | |
| `ibkr.conduit.order.submission.count` | Counter | side, order_type |
| `ibkr.conduit.order.cancel.count` | Counter | |
| `ibkr.conduit.order.question.count` | Counter | |

### Market Data Metrics

| Metric | Type | Tags |
|---|---|---|
| `ibkr.conduit.marketdata.snapshot.duration` | Histogram (ms) | preflight |
| `ibkr.conduit.marketdata.snapshot.count` | Counter | |
| `ibkr.conduit.marketdata.snapshot.preflight.count` | Counter | |
| `ibkr.conduit.marketdata.history.duration` | Histogram (ms) | |
| `ibkr.conduit.marketdata.history.count` | Counter | |

### WebSocket Metrics

| Metric | Type | Tags |
|---|---|---|
| `ibkr.conduit.websocket.connection_state` | ObservableGauge | |
| `ibkr.conduit.websocket.messages.received` | Counter | topic |
| `ibkr.conduit.websocket.messages.sent` | Counter | |
| `ibkr.conduit.websocket.reconnect.count` | Counter | trigger |
| `ibkr.conduit.websocket.heartbeat.count` | Counter | |

### Flex Metrics

| Metric | Type | Tags |
|---|---|---|
| `ibkr.conduit.flex.query.duration` | Histogram (ms) | |
| `ibkr.conduit.flex.query.count` | Counter | |
| `ibkr.conduit.flex.poll.count` | Counter | |
| `ibkr.conduit.flex.error.count` | Counter | error_code |

## Structured Logging

All log entries use `[LoggerMessage]` source generation for high-performance structured logging. Field names are defined in `LogFields` constants (all prefixed with `ibkr.`).

### Log Levels

| Level | Usage |
|---|---|
| Trace | Per-tick, per-message details (heartbeat, WebSocket message) |
| Debug | Operation start/completion (session init, snapshot requested) |
| Information | Lifecycle events (session ready, WebSocket connected, LST acquired) |
| Warning | Recoverable issues (question auto-confirmed, tickle failure, pre-flight retry) |
| Error | Failures (LST validation failed, WebSocket disconnect, Flex query error) |

## Example Queries

### Grafana / Prometheus

```promql
# Average HTTP request duration by endpoint
rate(ibkr_conduit_http_request_duration_sum[5m])
  / rate(ibkr_conduit_http_request_duration_count[5m])

# 429 rate by endpoint
rate(ibkr_conduit_http_status_429_count_total[5m])

# Session refresh rate
rate(ibkr_conduit_session_refresh_count_total[5m])

# Order submission P99 latency
histogram_quantile(0.99, rate(ibkr_conduit_order_submission_duration_bucket[5m]))
```

### Example Alert Rules

```yaml
# Alert when getting rate limited
- alert: IbkrRateLimited
  expr: rate(ibkr_conduit_http_status_429_count_total[5m]) > 0
  for: 1m
  annotations:
    summary: "IBKR API returning 429 responses"

# Alert when session refresh takes too long
- alert: IbkrSessionRefreshSlow
  expr: ibkr_conduit_session_refresh_duration > 30000
  annotations:
    summary: "Session refresh exceeded 30s"

# Alert when WebSocket disconnects
- alert: IbkrWebSocketDown
  expr: ibkr_conduit_websocket_connection_state == 0
  for: 2m
  annotations:
    summary: "IBKR WebSocket has been disconnected for 2+ minutes"
```
