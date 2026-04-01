# Test Quality Spec 2: Concurrency & Resilience — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add ~10 tests for concurrent 401 handling, missing HTTP error status integration tests, retry exhaustion, and cross-account order parallelism.

**Architecture:** WireMock integration tests for HTTP error scenarios and concurrent 401 recovery. Unit tests for cross-account order parallelism. All tests use existing patterns — no new dependencies.

**Tech Stack:** xUnit v3, Shouldly, WireMock.Net, existing hand-written fakes

---

## Dependency Graph

```
Task 1 (concurrent 401)     — integration test in SessionLifecycleTests.cs
Task 2 (error responses)    — integration tests in RateLimitingAndResilienceTests.cs
Task 3 (retry exhaustion)   — integration test in RateLimitingAndResilienceTests.cs
Task 4 (cross-account)      — unit test in OrderOperationsTests.cs

All tasks are independent and can run in any order.
```

**Branch:** `test/concurrency-resilience-tests` (single branch for all tasks)

---

## Task 1 — Concurrent 401 Handling (2 tests)

### Files Modified

- `tests/IbkrConduit.Tests.Integration/Session/SessionLifecycleTests.cs`

### Important

Read the existing `UnauthorizedApiCall_TriggersReauthAndRetries` test in this file. It uses:
- WireMock scenario states to return 401 first, then 200
- `FakeTokenProvider` (internal class in the file) for `ISessionTokenProvider`
- `CreatePortfolioApi` helper that wires `TokenRefreshHandler` → `OAuthSigningHandler` → `HttpClientHandler`
- `CreateSessionApi` helper for the session API
- `SessionManager` with `IbkrClientOptions`, `TickleTimerFactory`, `SessionLifecycleNotifier`

The concurrent test fires multiple parallel requests. The `TokenRefreshHandler` calls `_sessionManager.ReauthenticateAsync()` on 401. The `SessionManager.ReauthenticateAsync()` uses a semaphore — concurrent calls serialize. The first caller performs the re-auth, subsequent callers wait for the semaphore, check state is Ready, and return.

WireMock scenario setup for concurrent 401:
- Use `AtMost(N)` or a counter-based scenario. The simplest approach: WireMock responds with 401 for the first 3 requests (initial attempts), then 200 for all subsequent (retries after reauth). Use scenario states: initial → state "reauthed" after the ssodh/init call. Then portfolio/accounts in state "reauthed" returns 200.

Actually the cleanest approach: configure portfolio/accounts to always return 401 in default state, and 200 when state is "reauthed". Configure ssodh/init to set state to "reauthed". This way: all initial calls get 401, SessionManager.ReauthenticateAsync calls ssodh/init (flips state), all retry calls get 200.

### Test Inventory

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| 1 | `ConcurrentUnauthorized_SingleReauthServesAllRequests` | Fire 3 parallel portfolio/accounts requests. All get 401. One re-auth occurs (ssodh/init). All retry and get 200. Verify ssodh/init called exactly once. |
| 2 | `DoubleUnauthorized_ReauthFailsOnRetry_ReturnsFinalStatus` | First call gets 401, reauth occurs, retry also gets 401 (server persistently unauthorized). Verify the final 401 is returned to the caller (no infinite loop). |

### Steps

- [ ] Read existing `SessionLifecycleTests.cs` for helpers and patterns
- [ ] Add test `ConcurrentUnauthorized_SingleReauthServesAllRequests`:
  ```
  Setup:
  - ssodh/init returns 200 and sets scenario to "reauthed"
  - tickle returns 200
  - portfolio/accounts in default state returns 401
  - portfolio/accounts in "reauthed" state returns 200

  Act:
  - Initialize SessionManager (triggers ssodh/init, sets scenario to "reauthed")
  - Reset WireMock scenarios back to default (so portfolio calls get 401 again)
  - Fire 3 parallel GetAccountsAsync via Task.WhenAll

  Assert:
  - All 3 results are not null and contain "DU1234567"
  - ssodh/init was called exactly 2 times (1 for init + 1 for reauth)
  ```
- [ ] Add test `DoubleUnauthorized_ReauthFailsOnRetry_ReturnsFinalStatus`:
  ```
  Setup:
  - ssodh/init returns 200 (reauth "succeeds" but server still rejects)
  - tickle returns 200
  - portfolio/accounts ALWAYS returns 401

  Act:
  - Call GetAccountsAsync (via Refit)

  Assert:
  - Should throw Refit.ApiException with StatusCode 401
  ```
- [ ] Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj --configuration Release --filter "FullyQualifiedName~SessionLifecycleTests"`
- [ ] Verify all tests pass (existing + new)
- [ ] Commit: `test: add concurrent 401 handling integration tests`

---

## Task 2 — Error Response Integration Tests (3 tests)

### Files Modified

- `tests/IbkrConduit.Tests.Integration/Http/RateLimitingAndResilienceTests.cs`

### Important

Follow the exact pattern of the existing tests:
- `CreatePipelinedClient` creates a client with the full resilience + rate limiting pipeline
- `CreateTestResiliencePipeline` uses zero-delay retries (max 3 attempts, handles >=500/408/429)
- WireMock scenarios with `InScenario` / `WillSetStateTo` / `WhenStateIs` for error-then-success
- `_server.LogEntries.Count` to verify request count

The retry predicate is: `(int)r.StatusCode >= 500` — so 500, 502, 503, 504, etc. are all retried. 403 is NOT retried (< 500, not 408, not 429).

### Test Inventory

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| 1 | `ForbiddenError403_NotRetried` | WireMock returns 403. Verify response is 403, only 1 request made. |
| 2 | `InternalServerError500_IsRetried` | WireMock returns 500 first, then 200. Verify retry occurs and caller gets 200. |
| 3 | `BadGateway502_IsRetried` | WireMock returns 502 first, then 200. Verify retry occurs and caller gets 200. |

### Steps

- [ ] Add test `ForbiddenError403_NotRetried`:
  ```csharp
  _server.Given(
      Request.Create()
          .WithPath("/v1/api/portfolio/accounts")
          .UsingGet())
      .RespondWith(
          Response.Create()
              .WithStatusCode(403)
              .WithBody("Forbidden"));

  using var client = CreatePipelinedClient(_globalLimiter, _endpointLimiters, _pipeline);
  var response = await client.GetAsync($"{_server.Url}/v1/api/portfolio/accounts", TestContext.Current.CancellationToken);

  response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  _server.LogEntries.Count.ShouldBe(1);
  ```
- [ ] Add test `InternalServerError500_IsRetried` (same pattern as `TransientError_IsRetriedTransparently` but with 500)
- [ ] Add test `BadGateway502_IsRetried` (same pattern but with 502)
- [ ] Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj --configuration Release --filter "FullyQualifiedName~RateLimitingAndResilienceTests"`
- [ ] Verify all tests pass (existing + new)
- [ ] Commit: `test: add 403, 500, 502 error response integration tests`

---

## Task 3 — Retry Exhaustion Integration Test (1 test)

### Files Modified

- `tests/IbkrConduit.Tests.Integration/Http/RateLimitingAndResilienceTests.cs`

### Important

The resilience pipeline has `MaxRetryAttempts = 3` (configured in `CreateTestResiliencePipeline`). So a persistent error will be attempted 1 + 3 = 4 times total. When all retries are exhausted, the final response is returned to the caller (not an exception).

### Test Inventory

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| 1 | `PersistentServerError_ExhaustsRetriesAndReturnsFinalResponse` | WireMock always returns 503. After 4 attempts (1 + 3 retries), caller receives 503. Verify `_server.LogEntries.Count == 4`. |

### Steps

- [ ] Add test `PersistentServerError_ExhaustsRetriesAndReturnsFinalResponse`:
  ```csharp
  _server.Given(
      Request.Create()
          .WithPath("/v1/api/portfolio/accounts")
          .UsingGet())
      .RespondWith(
          Response.Create()
              .WithStatusCode(503)
              .WithBody("Service Unavailable"));

  using var client = CreatePipelinedClient(_globalLimiter, _endpointLimiters, _pipeline);
  var response = await client.GetAsync($"{_server.Url}/v1/api/portfolio/accounts", TestContext.Current.CancellationToken);

  response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
  _server.LogEntries.Count.ShouldBe(4); // 1 initial + 3 retries
  ```
- [ ] Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj --configuration Release --filter "FullyQualifiedName~RateLimitingAndResilienceTests"`
- [ ] Verify all tests pass
- [ ] Commit: `test: add retry exhaustion integration test`

---

## Task 4 — Cross-Account Order Parallelism (1 test)

### Files Modified

- `tests/IbkrConduit.Tests.Unit/Orders/OrderOperationsTests.cs`

### Important

The existing `PlaceOrderAsync_SerializesPerAccount` test verifies that two calls for the SAME account serialize. This test verifies the opposite: two calls for DIFFERENT accounts run in PARALLEL.

Use a similar `BlockingOrderApi` approach but verify that both calls enter the API simultaneously. The key insight: if both calls for different accounts can be blocked at the same time (both waiting on semaphores), they must be running in parallel.

The existing `BlockingOrderApi` class uses sequential semaphores (`_semaphore1` for call 1, `_semaphore2` for call 2). For the parallel test, use a single `SemaphoreSlim(0, 2)` — both calls should be able to reach the semaphore wait simultaneously. Use a `CountdownEvent` or `Barrier` to prove both are inside the API call at the same time.

### Test Inventory

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| 1 | `PlaceOrderAsync_DifferentAccounts_RunInParallel` | Start orders for "ACCT1" and "ACCT2" simultaneously. Both enter the API call concurrently. Verify both complete. |

### Steps

- [ ] Add a new inner fake class `ParallelVerifyingOrderApi` to `OrderOperationsTests.cs`:
  ```
  Uses a CountdownEvent(2). Each PlaceOrderAsync call signals the countdown
  and waits for it to complete (proving both calls are concurrent). Then returns
  a successful OrderSubmissionResponse.
  If calls were serialized, the second call would never enter and the countdown
  would time out.
  ```
- [ ] Add test `PlaceOrderAsync_DifferentAccounts_RunInParallel`:
  ```
  Create ParallelVerifyingOrderApi with a 2-second timeout.
  Fire two PlaceOrderAsync calls for "ACCT1" and "ACCT2" via Task.WhenAll.
  Both should complete successfully. If they were serialized, the CountdownEvent
  would timeout and the test would fail.
  ```
- [ ] Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~OrderOperationsTests"`
- [ ] Verify all tests pass (existing + new)
- [ ] Commit: `test: add cross-account order parallelism test`

---

## Final Verification

After all tasks are committed:

- [ ] Run full check: `dotnet build /workspace/ibkr-conduit --configuration Release`
- [ ] Run full check: `dotnet test /workspace/ibkr-conduit --configuration Release`
- [ ] Run full check: `dotnet format /workspace/ibkr-conduit --verify-no-changes`

### Test Count Summary

| Task | Tests |
|------|-------|
| Task 1: Concurrent 401 handling | 2 |
| Task 2: Error response (403, 500, 502) | 3 |
| Task 3: Retry exhaustion | 1 |
| Task 4: Cross-account parallelism | 1 |
| **Total** | **7** |
