# Future Enhancements

Ideas that aren't worth building now but may be valuable later. Each entry should describe the enhancement, why it's deferred, and what would trigger building it.

---

## Selective Order Question Answers

**What:** Allow consumers to provide a `Dictionary<string, bool>` answer map when submitting orders, controlling which IBKR confirmation questions are accepted vs rejected (similar to ibind's `Answers` pattern).

**Current behavior:** Auto-confirm all questions with a warning log. Known question types are suppressed at session init via `SuppressibleMessages`.

**Why deferred:** Auto-confirm is sufficient for automated trading systems that validate orders before submission. The suppression mechanism already prevents most questions. Adding a per-order answer map adds API complexity without clear value today.

**Trigger to build:** A consumer needs to reject specific question types at order submission time (e.g., fail-safe against mispriced orders that bypass their own validation). Could be implemented as an optional `Answers` parameter on the order submission method.

---

## 429 Adaptive Rate Limiting

**What:** Dynamically tighten the local rate limiter by 10-20% on 429 response, run at tightened rate for 60 seconds, then gradually relax back to configured limits. A control loop, not a permanent ratchet.

**Current behavior:** Local token bucket prevents most 429s. If one slips through, Polly retries with exponential backoff.

**Why deferred:** YAGNI — the local rate limiter should prevent 429s entirely. The adaptive loop adds significant complexity for a scenario that shouldn't occur.

**Trigger to build:** Observing repeated 429s in production despite correct rate limiter configuration, suggesting drift between local and server-side enforcement.

---

## Circuit Breaker

**What:** Polly circuit breaker that opens after N consecutive failures, preventing request storms during extended outages.

**Current behavior:** SessionManager handles session-level failures (re-auth on 401, tickle detects dead sessions). Polly retries transient errors.

**Why deferred:** Two competing recovery mechanisms (circuit breaker + SessionManager) add complexity. SessionManager already handles the recovery path.

**Trigger to build:** Observing cascading failure patterns where rapid retries during an outage cause additional problems (e.g., IP penalty box from retry storms).

---

## Multi-Tenant Session Registry

**What:** A registry that manages N independent `SessionManager` instances, one per tenant. Consumers register multiple tenants and the registry handles lifecycle for all of them.

**Current behavior:** Single-tenant `SessionManager` with clean interfaces. Multi-tenant requires multiple `AddIbkrClient` registrations.

**Why deferred:** Single-tenant is sufficient for initial consumers. The per-tenant `SessionManager` has clean interfaces that make a registry wrapper straightforward when needed.

**Trigger to build:** A consumer needs to manage 3+ tenant sessions concurrently with shared infrastructure (e.g., an advisor managing multiple client accounts).

---

## OpenTelemetry + Structured Logging

**What:** OTel spans for LST acquisition, session init, re-auth flows, and structured log fields (tenant ID, endpoint, duration) across all components.

**Current behavior:** Basic `ILogger` with `LoggerMessage` source generation in TickleTimer and SessionManager.

**Why deferred:** Core functionality takes priority. The logging infrastructure is in place (`Microsoft.Extensions.Logging.Abstractions`) — adding OTel is additive, not a redesign.

**Trigger to build:** Moving toward production deployment where observability is required for operations.

---

## Sub-account Support (FA/IBroker)

**What:** Support for tiered account structures — Financial Advisors and IBroker accounts managing multiple sub-accounts. Endpoints: `/portfolio/subaccounts`, `/portfolio/subaccounts2` (paginated).

**Current behavior:** Only individual accounts supported via `/portfolio/accounts`.

**Why deferred:** Sub-accounts are specific to FA/IBroker account types. Individual traders don't need this. Adding it requires pagination handling and a different account discovery flow.

**Trigger to build:** A consumer with an FA or IBroker account needs to manage sub-accounts programmatically.

---

## Vogen Strongly-Typed Identifiers

**What:** Replace raw `string` parameters (`accountId`, `conid`, `orderId`, `flexQueryId`) with Vogen value objects (`AccountId`, `Conid`, `OrderId`, `FlexQueryId`) across all Refit interfaces, operations, and public API surface. Prevents accidental parameter swaps at compile time.

**Current behavior:** All identifiers are plain strings. Nothing stops `GetPositionByConidAsync(conid, accountId)` from compiling with swapped arguments.

**Why deferred:** Spike on `spike/vogen-value-objects` branch confirmed Vogen works cleanly with Refit path parameters and System.Text.Json. However, the conversion is a high-churn mechanical refactor that touches nearly every file. Better to do after the public API surface stabilizes.

**Trigger to build:** Pre-v1.0 API stabilization pass. Good candidates: `AccountId`, `Conid`, `OrderId`, `FlexQueryId`, `FlexToken`.

---

## AccountSummaryFields Static Constants

**What:** A static class with named constants for all account summary field keys (e.g., `NetLiquidationValue = "netliquidationvalue"`, `TotalCashValue = "totalcashvalue"`) so consumers don't need to hardcode string keys.

**Current behavior:** The `/portfolio/{accountId}/summary` endpoint returns `Dictionary<string, AccountSummaryEntry>` with dynamic string keys. Consumers must know the key names.

**Why deferred:** The IBKR documentation does not exhaustively list all possible summary field keys. Building constants from incomplete documentation risks missing fields or including incorrect names. Real API responses are needed to discover the full set of keys.

**Trigger to build:** Observe real API responses to catalog all summary field keys, then create a `AccountSummaryFields` static class similar to `MarketDataFields`.

---

## Hand-Crafted WireMock Fixtures for Untriggerable Scenarios

**What:** WireMock fixture files for API scenarios that can't be captured from the real API on demand. These would be hand-crafted based on observed behavior and API documentation, then used in integration tests.

Scenarios to cover:

- **429 Rate Limiting** — Response with `{"error":"rate limited"}` and `Retry-After` header. Validates resilience handler retries with backoff.
- **401 Session Expiry** — Mid-session auth failure. Validates TokenRefreshHandler re-authenticates and retries.
- **500 "Not ready"** — `{"error":"Not ready"}` from endpoints that need cache warm-up (e.g., combo/positions). Validates retry behavior.
- **Competing Session** — Auth status returning `{"authenticated":false,"competing":true}`. Validates session recovery.
- **Order Confirmation Flow** — Place order returning the confirmation shape `[{"id":"...","message":["warning"]}]` vs success `[{"order_id":"..."}]` vs rejection `{"error":"rejected"}`. Three distinct response shapes for the same endpoint.
- **Pagination Edge Cases** — Positions page 0 with data vs page 999 returning empty results.
- **Large Response Payloads** — Account summary with all 143 fields populated. Validates deserialization handles the full payload.
- **Empty Collection Responses** — `[]` or `{"orders":[]}` when no data exists. Different from an error response.

**Current behavior:** WireMock tests use minimal inline JSON or captured fixtures. Error scenarios use guessed response shapes.

**Why deferred:** The capture tool can't trigger these conditions on demand. Hand-crafting requires careful analysis of recorded error responses and API documentation.

**Trigger to build:** After the capture tool has populated fixtures for all success paths and capturable error paths. The hand-crafted fixtures fill the remaining gaps.

---

## Refactor ServiceCollectionExtensions

**What:** Break up `ServiceCollectionExtensions.cs` which has grown into a monolith handling LST client setup, session token provider, rate limiter creation, resilience pipeline, session API pipeline, all consumer Refit client registrations, operations registrations, WebSocket, Flex, and the unified facade. Split into focused registration methods or separate extension classes by concern.

**Current behavior:** Single 290+ line file with one large `AddIbkrClient` method that does everything. Hard to navigate, hard to modify one concern without reading the whole file.

**Why deferred:** It works correctly and the structure is stable. Refactoring it risks introducing registration order bugs. Better to do when the API surface is settled.

**Trigger to build:** When adding new features or pipelines becomes difficult due to the file size, or when the base URL override pattern (added for testing) needs to evolve into something more configurable.
