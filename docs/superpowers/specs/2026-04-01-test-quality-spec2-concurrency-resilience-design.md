# Spec 2: Concurrency & Resilience Test Gap Fill

## Goal

Add integration and unit tests for concurrent 401 handling, missing HTTP error status codes, retry exhaustion under WireMock, and cross-account order parallelism.

## Context

This is the second of three test quality specs:

- **Spec 1 (done):** Unit test gaps — operations delegation, edge cases for complex components
- **Spec 2 (this):** Concurrency and resilience — concurrent 401s, error response integration tests, retry exhaustion, cross-account parallelism
- **Spec 3:** WebSocket and resource management — reconnect edge cases, message pump errors, dispose paths

## What's Already Covered

The existing test suite handles:
- Retry logic for 503, 429, 408 (unit + integration)
- Rate limiting — global and per-endpoint, queue-full rejection
- Per-account order semaphore serialization (same account)
- SessionTokenProvider concurrent deduplication (10 parallel calls)
- SessionManager concurrent re-auth serialization
- TokenRefreshHandler 401 → reauth → retry, body cloning, tickle skip, double-401

## Tasks

### Task 1: Concurrent 401 Handling (Integration)

**File:** `tests/IbkrConduit.Tests.Integration/Session/SessionLifecycleTests.cs`

Add a WireMock integration test where multiple requests hit 401 simultaneously. The TokenRefreshHandler + SessionManager pipeline should:
- Trigger exactly one re-auth (not N re-auths for N requests)
- All requests eventually succeed after the single re-auth completes
- No request is lost or dropped

Test setup: WireMock returns 401 for the first N requests to an endpoint, then 200 for all subsequent. Fire 3-5 concurrent requests via Task.WhenAll. Verify all return 200 and the re-auth endpoint was called exactly once.

~1-2 new tests.

### Task 2: Error Response Integration Tests (WireMock)

**File:** `tests/IbkrConduit.Tests.Integration/Http/RateLimitingAndResilienceTests.cs`

Add WireMock tests for HTTP statuses not currently covered:

| Status | Expected Behavior | Why It Matters |
|---|---|---|
| 403 Forbidden | Passes through immediately, no retry | Not a transient error — should NOT be retried |
| 500 Internal Server Error | Retried by resilience handler | Covered by `>=500` predicate but no concrete test |
| 502 Bad Gateway | Retried by resilience handler | Same — verify the predicate actually works for 502 |

The 500 and 502 tests follow the same pattern as the existing 503 test: WireMock returns the error first, then 200. Verify retry occurs and caller gets 200.

The 403 test verifies NO retry: WireMock returns 403, and the caller receives 403 directly. Verify only 1 request was made (no retry).

~3-4 new tests.

### Task 3: Retry Exhaustion (Integration)

**File:** `tests/IbkrConduit.Tests.Integration/Http/RateLimitingAndResilienceTests.cs`

Add a WireMock test where the server returns persistent 503s (more responses than the retry count). Verify:
- The resilience handler exhausts all retries
- The final 503 is surfaced to the caller (not swallowed)
- The total number of WireMock requests matches 1 + max retries

~1 new test.

### Task 4: Cross-Account Order Parallelism (Unit)

**File:** `tests/IbkrConduit.Tests.Unit/Orders/OrderOperationsTests.cs`

Add a unit test verifying that orders for **different accounts** run in parallel, not serialized. Use a blocking fake API with timing gates:
- Start PlaceOrderAsync for "ACCT1" and "ACCT2" simultaneously
- Both should enter the API call concurrently (not wait for each other)
- Verify both calls overlap in time (e.g., both blocked at the same semaphore gate)

This complements the existing `PlaceOrderAsync_SerializesPerAccount` test which verifies same-account serialization.

~1 new test.

## Testing Conventions

All new tests follow existing project rules:

- xUnit v3, Shouldly assertions (no `Assert`)
- Naming: `MethodName_Scenario_ExpectedResult`
- Integration tests use WireMock.Net for HTTP mocking
- Unit tests use existing hand-written fakes (no NSubstitute in files that already have fakes)
- `CancellationToken` passed via `TestContext.Current.CancellationToken`

## Out of Scope

- WebSocket reconnect and message pump tests (Spec 3)
- TokenRefreshHandler content cloning edge cases (Spec 3)
- Resource cleanup / dispose tests (Spec 3)

## Success Criteria

- Concurrent 401 test proves single re-auth for multiple simultaneous failures
- 403 test proves no retry for forbidden responses
- 500, 502 tests prove the `>=500` retry predicate works concretely
- Retry exhaustion test proves final error surfaces to caller
- Cross-account parallelism test proves different accounts aren't serialized
- ~8-10 new passing tests
- `dotnet build --configuration Release && dotnet test --configuration Release && dotnet format --verify-no-changes` passes clean
