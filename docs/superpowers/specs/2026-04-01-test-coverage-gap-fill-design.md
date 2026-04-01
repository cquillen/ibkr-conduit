# Test Coverage Gap Fill

**Date:** 2026-04-01
**Status:** Draft
**Goal:** Exclude trivial code from coverage metrics and write targeted tests for untested branching logic.

---

## Scope

Two concerns:

1. **Exclude trivial code** — apply `[ExcludeFromCodeCoverage]` to records, pass-through operations, and static constant classes so coverage metrics reflect real logic only
2. **Test untested branching logic** — write unit tests for the critical code paths that currently have 0% coverage: WebSocket client, Flex polling, HTTP handler edge cases, SessionManager error paths

### Not In Scope

- Increasing coverage for the sake of a number
- Testing auto-generated record constructors/properties
- Testing single-line Refit delegation methods

---

## Task T.1 — Apply `[ExcludeFromCodeCoverage]` Exclusions

Add `[ExcludeFromCodeCoverage]` to the following files. Each file must include `using System.Diagnostics.CodeAnalysis;`.

### Records/Models (no logic, just data containers):

- `src/IbkrConduit/Portfolio/IIbkrPortfolioApiModels.cs` — Account, Position, AccountSummaryEntry, LedgerEntry, AccountInfo, AccountAllocation, PositionContractInfo, AccountPerformance, TransactionHistory, PerformanceRequest, TransactionHistoryRequest
- `src/IbkrConduit/Session/IIbkrSessionApiModels.cs` — SsodhInitRequest, SsodhInitResponse, TickleResponse, TickleIserverStatus, TickleAuthStatus, SuppressRequest, SuppressResponse, LogoutResponse
- `src/IbkrConduit/Contracts/IIbkrContractApiModels.cs` — ContractSearchResult, ContractSection, ContractDetails
- `src/IbkrConduit/Orders/IIbkrOrderApiModels.cs` — OrderRequest, OrderResult, CancelOrderResponse, OrdersPayload, OrderWireModel, OrderSubmissionResponse, ReplyRequest, OrdersResponse, LiveOrder, Trade
- `src/IbkrConduit/MarketData/IIbkrMarketDataApiModels.cs` — MarketDataSnapshotRaw, MarketDataSnapshot, HistoricalDataResponse, HistoricalBar
- `src/IbkrConduit/Streaming/StreamingModels.cs` — MarketDataTick, OrderUpdate, PnlUpdate, AccountSummaryUpdate, AccountLedgerUpdate
- `src/IbkrConduit/Flex/FlexModels.cs` — FlexTrade, FlexPosition
- `src/IbkrConduit/Auth/IbkrOAuthCredentials.cs` — credential record
- `src/IbkrConduit/Auth/LiveSessionToken.cs` — token record
- `src/IbkrConduit/Session/IbkrClientOptions.cs` — options record

### Pure Pass-Through Operations:

- `src/IbkrConduit/Client/PortfolioOperations.cs` — all methods are single-line delegation to Refit
- `src/IbkrConduit/Client/ContractOperations.cs` — same pattern

### Static Constant Classes:

- `src/IbkrConduit/MarketData/MarketDataFields.cs` — 110 string constants
- `src/IbkrConduit/Session/SuppressibleMessages.cs` — message ID constants + AutomatedTrading list
- `src/IbkrConduit/Diagnostics/LogFields.cs` — log field name constants
- `src/IbkrConduit/Diagnostics/IbkrConduitDiagnostics.cs` — ActivitySource + Meter static instances

### Other Trivial:

- `src/IbkrConduit/Streaming/CancellationDisposable.cs` — one-line dispose
- `src/IbkrConduit/Http/RateLimitRejectedException.cs` — exception with constructors only
- `src/IbkrConduit/Flex/FlexQueryException.cs` — exception with ErrorCode property

After applying exclusions, run coverage and record the new baseline (should be significantly higher since the denominator shrinks).

---

## Task T.2 — IWebSocketAdapter + WebSocket Client Tests

### Extract IWebSocketAdapter

Create `src/IbkrConduit/Streaming/IWebSocketAdapter.cs`:

```csharp
internal interface IWebSocketAdapter : IAsyncDisposable
{
    WebSocketState State { get; }
    Task ConnectAsync(Uri uri, CancellationToken cancellationToken);
    Task SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType,
        bool endOfMessage, CancellationToken cancellationToken);
    ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(
        Memory<byte> buffer, CancellationToken cancellationToken);
    Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
        CancellationToken cancellationToken);
    void SetRequestHeader(string name, string value);
    IWebProxy? Proxy { set; }
}
```

Create `src/IbkrConduit/Streaming/ClientWebSocketAdapter.cs` — wraps `ClientWebSocket`, implements `IWebSocketAdapter`. Production use.

Update `IbkrWebSocketClient` to take `Func<IWebSocketAdapter>` factory instead of creating `ClientWebSocket` directly. DI registration provides `() => new ClientWebSocketAdapter()`.

### FakeWebSocketAdapter for Tests

Create in test project: sends/receives from in-memory queues. Allows tests to:
- Inject messages as if received from server
- Verify messages sent by the client
- Simulate connection states (Open, Closed, Aborted)
- Simulate connection failures

### Test Cases

| Test | What It Verifies |
|---|---|
| `ConnectAsync_SetsUrlAndHeaders` | URL includes oauth_token, cookie and User-Agent headers set |
| `ConnectAsync_StartsHeartbeatAndMessagePump` | After connect, heartbeat messages are sent periodically |
| `MessagePump_RoutesMessagesByTopicPrefix` | smd messages go to smd subscribers, sor to sor, etc. |
| `MessagePump_IgnoresUnknownTopics` | Messages with unrecognized topics don't crash |
| `SubscribeTopicAsync_SendsSubscribeMessage` | Calling subscribe sends the topic string on the socket |
| `SubscribeTopicAsync_ConnectsIfNotConnected` | Lazy connect on first subscription |
| `ReconnectAsync_ReplaysActiveSubscriptions` | After reconnect, all prior subscription messages are re-sent |
| `DisconnectAsync_StopsHeartbeatAndPump` | Clean disconnect stops background tasks |
| `DisposeAsync_UnsubscribesFromNotifier` | Lifecycle notifier subscription is cleaned up |
| `OnSessionRefreshed_TriggersReconnect` | Session refresh notification causes disconnect + reconnect |

---

## Task T.3 — FlexClient Polling Unit Tests

Use a `FakeHttpMessageHandler` that returns different responses for sequential calls.

### Test Cases

| Test | What It Verifies |
|---|---|
| `ExecuteQueryAsync_SuccessOnFirstPoll_ReturnsImmediately` | No retry when GetStatement returns data immediately |
| `ExecuteQueryAsync_RetryOnError1019_SucceedsOnSecondAttempt` | Polls again after "in progress" response |
| `ExecuteQueryAsync_RetryOnError1019_SucceedsAfterMultipleAttempts` | Polls 3+ times before success |
| `ExecuteQueryAsync_TimeoutAfterMaxDuration_ThrowsTimeoutException` | Throws after 60s of polling |
| `ExecuteQueryAsync_NonRetryableError_ThrowsFlexQueryException` | Error 1004 (invalid token) throws immediately, no retry |
| `ExecuteQueryAsync_SendRequestFails_ThrowsFlexQueryException` | Step 1 failure (bad query ID) propagates error |
| `ExecuteQueryAsync_DateRangeOverride_IncludesInUrl` | fd and td query params included when dates provided |
| `ExecuteQueryAsync_PassesCancellationToken` | Cancellation during polling stops the loop |

---

## Task T.4 — HTTP Handler Edge Case Tests

### TokenRefreshHandler

| Test | What It Verifies |
|---|---|
| `SendAsync_401WithBody_ClonesBodyForRetry` | POST request body is preserved and replayed after re-auth |
| `SendAsync_TicklePath_SkipsRetryOn401` | 401 from /tickle returns directly, no re-auth triggered |
| `SendAsync_RetryAlso401_ReturnsSecondResponse` | Single retry only — if retry also 401, returns it |
| `SendAsync_Non401_PassesThrough` | 200, 400, 500 all pass through without retry |

### GlobalRateLimitingHandler

| Test | What It Verifies |
|---|---|
| `SendAsync_QueueFull_ThrowsRateLimitRejectedException` | Limiter with 0 queue rejects immediately |
| `SendAsync_TokenAvailable_PassesThrough` | Normal operation — acquires token and sends |

### EndpointRateLimitingHandler

| Test | What It Verifies |
|---|---|
| `SendAsync_MatchedEndpoint_AppliesLimiter` | Request to /iserver/account/orders uses endpoint limiter |
| `SendAsync_UnmatchedEndpoint_PassesThrough` | Request to unknown path skips endpoint limiting |
| `SendAsync_MatchedEndpoint_QueueFull_ThrowsRateLimitRejectedException` | Endpoint queue full → rejection |

### ResilienceHandler

| Test | What It Verifies |
|---|---|
| `SendAsync_503_RetriesWithBackoff` | 503 triggers retry, subsequent 200 returned |
| `SendAsync_429_RetriesWithBackoff` | 429 triggers retry |
| `SendAsync_408_RetriesWithBackoff` | Request timeout triggers retry |
| `SendAsync_400_DoesNotRetry` | Client errors pass through immediately |
| `SendAsync_MaxRetriesExceeded_ReturnsLastResponse` | After 3 retries, returns the final failed response |

---

## Task T.5 — SessionManager + FlexOperations Gap Tests

### SessionManager

| Test | What It Verifies |
|---|---|
| `ReauthenticateAsync_WhenAlreadyReady_SkipsIfTokenNotExpiring` | Concurrent re-auth callers — second one short-circuits |
| `DisposeAsync_DuringInit_CleansUpGracefully` | Dispose while initializing doesn't deadlock |
| `DisposeAsync_LogoutFailure_SwallowsException` | Failed logout during dispose doesn't throw |
| `EnsureInitializedAsync_SuppressEmpty_SkipsSuppressCall` | No suppress call when SuppressMessageIds is empty |

### FlexOperations

| Test | What It Verifies |
|---|---|
| `ExecuteQueryAsync_NullToken_ThrowsInvalidOperationException` | Flex not configured — clear error |
| `ExecuteQueryAsync_WithDateRange_DelegatesCorrectly` | Date range overload passes through to FlexClient |

---

## Task T.6 — Run Coverage, Verify Improvement

1. Run `coverlet` with the same command as before
2. Compare new coverage vs baseline (71.7% line / 53.6% branch)
3. The metric should improve significantly since:
   - Denominator shrinks (trivial code excluded)
   - Numerator grows (new tests covering branching logic)
4. Document the final numbers

---

## Project Structure (New/Modified Files)

### New Files

```
src/IbkrConduit/Streaming/IWebSocketAdapter.cs
src/IbkrConduit/Streaming/ClientWebSocketAdapter.cs

tests/IbkrConduit.Tests.Unit/
  Streaming/IbkrWebSocketClientTests.cs
  Flex/FlexClientPollingTests.cs
  Http/TokenRefreshHandlerEdgeTests.cs
  Session/SessionManagerEdgeTests.cs
```

### Modified Files

```
~25 source files — adding [ExcludeFromCodeCoverage]
src/IbkrConduit/Streaming/IbkrWebSocketClient.cs — use IWebSocketAdapter factory
src/IbkrConduit/Http/ServiceCollectionExtensions.cs — register adapter factory
```

---

## Dependency Graph

```
Task T.1 (exclusions)  →  independent, do first
Task T.2 (WebSocket)   →  requires IWebSocketAdapter extraction
Task T.3 (FlexClient)  →  independent
Task T.4 (HTTP handlers) → independent
Task T.5 (SessionManager + FlexOps) → independent
Task T.6 (verify)      →  depends on all above
```

**Parallel:** T.2, T.3, T.4, T.5 are independent of each other (all can follow T.1).
