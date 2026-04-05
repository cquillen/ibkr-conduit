# Future Enhancements

Ideas that aren't worth building now but may be valuable later. Each entry describes the enhancement, why it's deferred, and what would trigger building it.

---

## Selective Order Question Answers

**What:** Allow consumers to provide a `Dictionary<string, bool>` answer map when submitting orders, controlling which IBKR confirmation questions are accepted vs rejected (similar to ibind's `Answers` pattern).

**Current behavior:** Auto-confirm all questions with a warning log. Known question types are suppressed at session init via `SuppressibleMessages`.

**Why deferred:** Auto-confirm is sufficient for automated trading systems that validate orders before submission. The suppression mechanism already prevents most questions. Adding a per-order answer map adds API complexity without clear value today.

**Trigger to build:** A consumer needs to reject specific question types at order submission time (e.g., fail-safe against mispriced orders that bypass their own validation). Could be implemented as an optional `Answers` parameter on the order submission method.

---

## 429 Adaptive Rate Limiting

**What:** Dynamically tighten the local rate limiter by 10-20% on 429 response, run at tightened rate for 60 seconds, then gradually relax back to configured limits. A control loop, not a permanent ratchet.

**Current behavior:** Local token bucket prevents most 429s. The `ResilienceHandler` (Polly retry) was removed because IBKR's misuse of 5xx codes made retries counterproductive. 429 responses now flow through as `Result.Failure` with `IbkrRateLimitError` including `RetryAfter`. Consumers handle retry at the application level.

**Why deferred:** YAGNI — the local rate limiter should prevent 429s entirely. The adaptive loop adds significant complexity for a scenario that shouldn't occur.

**Trigger to build:** Observing repeated 429s in production despite correct rate limiter configuration, suggesting drift between local and server-side enforcement.

---

## Vogen Strongly-Typed Identifiers

**What:** Replace raw `string` parameters (`accountId`, `conid`, `orderId`, `flexQueryId`) with Vogen value objects (`AccountId`, `Conid`, `OrderId`, `FlexQueryId`) across all Refit interfaces, operations, and public API surface. Prevents accidental parameter swaps at compile time.

**Current behavior:** All identifiers are plain strings. Nothing stops `GetPositionByConidAsync(conid, accountId)` from compiling with swapped arguments.

**Why deferred:** Spike on `spike/vogen-value-objects` branch confirmed Vogen works cleanly with Refit path parameters and System.Text.Json. However, the conversion is a high-churn mechanical refactor that touches nearly every file. Better to do after the public API surface stabilizes.

**Trigger to build:** Pre-v1.0 API stabilization pass. Good candidates: `AccountId`, `Conid`, `OrderId`, `FlexQueryId`, `FlexToken`.

---

## Combo Position Capture

**What:** Capture a populated combo/positions response by placing a combo/spread order during market hours, waiting for fill, then capturing the response.

**Current behavior:** The combo/positions endpoint has a wrapper and integration tests, but the recording returns an empty array `[]` because the paper account has no combo positions. The `ComboTest` category in the capture tool has the order flow scaffolded but needs market hours to execute.

**Why deferred:** Markets were closed when we tested. The combo order placement requires confirmation (message ID `o451`), and market orders won't fill on weekends.

**Trigger to build:** Run `dotnet run --project tools/ApiCapture -- combotest -v` during market hours. May need to suppress `o451` or adjust the capture flow to handle the confirmation step.

---

## Notification Endpoint (WebSocket-Triggered)

**What:** Add wrapper for `POST /iserver/notification` — responds to server prompts received via WebSocket `ntf` messages.

**Current behavior:** Endpoint exists in the API spec but has no Refit interface, no wrapper, and no tests. It requires an active WebSocket connection to receive the `ntf` message containing `orderId`, `reqId`, and response options.

**Why deferred:** Can't be tested without WebSocket integration. The endpoint is part of an asynchronous prompt flow that doesn't fit the standard request/response capture model.

**Trigger to build:** When WebSocket streaming is integration-tested and consumers need to respond to server prompts programmatically.

---

## WebSocket Integration Tests

**What:** Integration tests for the WebSocket streaming pipeline — `IStreamingOperations` methods (MarketData, OrderUpdates, ProfitAndLoss, AccountSummary, AccountLedger).

**Current behavior:** `StreamingOperations` has zero integration tests. The WebSocket client, topic subscription, `ChannelObservable`, and all five streaming methods are completely untested at the integration level.

**Why deferred:** WebSocket testing requires a mock WebSocket server, which is more complex than WireMock HTTP stubs. The streaming implementation works at the unit level.

**Trigger to build:** When a WebSocket mock framework is evaluated, or when streaming reliability becomes critical for production use.

---

## Completed (removed from active list)

These items were previously on this list and have been implemented:

- **AccountSummaryFields Static Constants** — implemented with all observed field names from live recordings, grouped by endpoint (Segments, Balances, AvailableFunds, Margins, MarketValue)
- **OpenTelemetry + Structured Logging** — Activity spans on every facade method, histograms for latency, counters for operations and retries, gauges for rate limiter queue depth
- **Sub-account Support (FA/IBroker)** — subaccounts, subaccounts2 (paginated), combo positions all have wrappers, recordings, and integration tests
- **Hand-Crafted WireMock Fixtures** — ErrorResultTests covers all error body patterns (JSON, empty, text, HTML, hidden 200 errors, 429 with Retry-After); AuthFailureTests covers persistent 401 and re-auth failures; preflight test covers metadata-only retry
- **Refactor ServiceCollectionExtensions** — split into ConsumerPipelineRegistration, SessionServiceRegistration, RateLimitingAndResilienceRegistration, StreamingAndFlexRegistration
- **Circuit Breaker** — removed along with ResilienceHandler; IBKR's misuse of 5xx codes made automated retry counterproductive
- **Multi-Tenant Session Registry** — decided not to implement in this library
