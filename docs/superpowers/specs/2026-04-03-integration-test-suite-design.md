# Integration Test Suite — Design Spec

## Goal

Build a comprehensive WireMock integration test suite for trade automation endpoints using real recorded API response shapes, with TDD driving DTO correctness and library bug fixes.

## Background

The spike (PR #73) proved the test pattern:
- Full DI stack via `AddIbkrClient` with `IbkrClientOptions.BaseUrl` pointed at WireMock — no fakes
- `MockLstServer` implements server-side DH key exchange using real `OAuthCrypto`
- `TestHarness` provides shared setup: WireMock server, synthetic credentials, LST handshake, ssodh/init, DI pipeline
- WireMock stubs validate OAuth headers (consumer key, access token, signature method)
- Fixture files from captured real API responses provide ground truth

The spike also found and fixed a real bug: `ErrorNormalizationHandler` was throwing `IbkrSessionException` on 401 before `TokenRefreshHandler` could intercept and retry.

## Approach

**TDD Flow:**
1. Write integration test using fixture with assertions on all fields
2. Test fails because DTO is incomplete/wrong
3. Fix DTO to match recorded response shape (using `generate_dtos.py` output as reference)
4. Test passes
5. For library bugs found by testing: write test first, then fix

**Test Infrastructure (proven in spike):**
- `TestHarness` — creates WireMock + full DI pipeline per test class
- `MockLstServer` — server-side DH exchange callback
- `TestCredentials` — synthetic OAuth credentials with ephemeral RSA keys
- `FixtureLoader` — loads sanitized fixture JSON from `Fixtures/` directories
- `StubAuthenticated(method, path, body)` — registers stubs with OAuth header validation

**Rules:**
- No fakes — full `AddIbkrClient()` DI pipeline, only the base URL changes
- Every endpoint must include a 401 recovery test
- WireMock stubs validate `oauth_consumer_key`, `oauth_token`, and `HMAC-SHA256` in Authorization header
- All requests verified for User-Agent presence
- Endpoint-specific error scenarios where applicable

## Endpoints in Scope

### Session (5 endpoints)
- `POST /iserver/auth/ssodh/init`
- `POST /tickle`
- `GET /iserver/auth/status`
- `POST /iserver/questions/suppress`
- `POST /iserver/questions/suppress/reset`

### Accounts (2 endpoints)
- `GET /iserver/accounts`
- `POST /iserver/account` (switch)

### Portfolio (5 endpoints)
- `GET /portfolio/accounts`
- `GET /portfolio/{accountId}/positions/{page}`
- `GET /portfolio/{accountId}/summary`
- `GET /portfolio/{accountId}/ledger`
- `GET /iserver/account/pnl/partitioned`

### Orders (8 endpoints)
- `POST /iserver/account/{accountId}/orders` (place)
- `POST /iserver/reply/{replyId}` (confirm)
- `POST /iserver/account/{accountId}/order/{orderId}` (modify)
- `DELETE /iserver/account/{accountId}/order/{orderId}` (cancel)
- `GET /iserver/account/order/status/{orderId}`
- `GET /iserver/account/orders` (live orders)
- `GET /iserver/account/trades`
- `POST /iserver/account/{accountId}/orders/whatif`

## Test Scenarios

### Per-Endpoint Tests

Every endpoint gets:

1. **Success** — Fixture-based, assert all DTO fields match recorded response
2. **401 Recovery** — First call returns 401, re-auth triggers new LST + ssodh/init, retry succeeds. Verify LST and ssodh/init called at least twice.

Plus endpoint-specific error scenarios where applicable:

| Endpoint | Additional Scenarios |
|---|---|
| `POST /iserver/account/{id}/orders` (place) | Direct submission, confirmation required, order rejection (`{"error":"..."}` as 200) |
| `POST /iserver/reply/{id}` | 401 between place and confirm |
| `GET /iserver/account/orders` (live orders) | Empty response `{"orders":[]}`, two-call priming pattern |
| `GET /portfolio/{id}/positions/{page}` | Empty page (beyond range), pagination with sort |
| `GET /portfolio/{id}/summary` | Large payload (143 fields) |
| `POST /iserver/account/{id}/orders/whatif` | Invalid conid error |
| `DELETE /iserver/account/{id}/order/{id}` | Cancel already-filled order |

### Pipeline-Level Tests (tested once, not per-endpoint)

**Auth failure scenarios (requires library fix to `TokenRefreshHandler`):**

3. **401 → re-auth succeeds → retry gets 401 again** — Credentials invalidated (e.g., new PEM uploaded to portal while client running). `TokenRefreshHandler` must not loop — one retry max, then throw `IbkrSessionException`.
4. **401 → re-auth fails (LST acquisition throws)** — DH exchange or signature validation fails (e.g., access token regenerated in portal). Throw `IbkrSessionException` wrapping the inner exception.
5. **401 → re-auth fails (ssodh/init returns error)** — Session init rejected. Throw `IbkrSessionException`.

**Server failure scenarios:**

6. **503 Service Unavailable** — `ResilienceHandler` retries with backoff, eventually succeeds (WireMock scenario: 503 → 503 → 200).
7. **Non-JSON error page** — Server returns HTML (e.g., `<html>Service Unavailable</html>`) on 500. `ErrorNormalizationHandler` must not crash parsing it.
8. **Connection refused / timeout** — `HttpRequestException` propagates as a clean error, not an obscure stack trace.
9. **500 "Not ready"** — `{"error":"Not ready"}` from endpoints needing cache warm-up. `ResilienceHandler` retries.

## Library Fixes Required

### TokenRefreshHandler: Retry limit and error handling

**Current behavior:** On 401, calls `ReauthenticateAsync()` then retries with no limit. If retry also 401s, loops forever. If re-auth throws, exception is unhandled.

**Required behavior:**
- One retry max after re-auth
- If retry also returns 401: throw `IbkrSessionException` ("Re-authentication succeeded but request still unauthorized — credentials may be invalidated")
- If `ReauthenticateAsync` throws: catch, wrap in `IbkrSessionException` with inner exception, throw

**TDD approach:** Write failing integration tests for scenarios 3-5 first, then fix `TokenRefreshHandler`.

### ErrorNormalizationHandler: 401 pass-through

**Already fixed in spike:** 401 responses now pass through to `TokenRefreshHandler` instead of throwing `IbkrSessionException`.

## Milestones

### M1: Session/Accounts + Pipeline Auth Failures

**Deliverables:**
- Generate fixture files from recordings for session and accounts endpoints
- Write session tests: init, tickle, auth status, suppress (success + 401 recovery)
- Write accounts tests: get accounts, switch account (success + 401 recovery)
- Write pipeline auth failure tests (scenarios 3-5) — these will fail initially
- Fix `TokenRefreshHandler` to pass auth failure tests
- Update Session and Account DTOs as needed
- All tests pass

**Estimated tests:** ~15

### M2: Portfolio

**Deliverables:**
- Generate fixtures for portfolio endpoints
- Write portfolio tests: accounts, positions, summary, ledger, PnL (success + 401 recovery)
- Endpoint-specific: empty positions page, large summary payload, pagination with sort
- Update Portfolio DTOs (Position, AccountSummaryEntry, LedgerEntry, PartitionedPnl)
- All tests pass

**Estimated tests:** ~15

### M3: Orders

**Deliverables:**
- Generate fixtures for order endpoints
- Write order tests: place, reply, cancel, status, live orders, trades, whatif (success + 401 recovery)
- Endpoint-specific: direct submission, confirmation flow, order rejection, cancel filled order, empty orders/trades
- Update Order DTOs (LiveOrder, OrderStatus, Trade, WhatIfResponse)
- All tests pass

**Estimated tests:** ~20

### M4: Server Failure Resilience

**Deliverables:**
- Hand-craft fixtures for server failure scenarios
- Write pipeline resilience tests (scenarios 6-9):
  - 503 with retry and eventual success
  - HTML error page on 500 doesn't crash
  - Connection timeout clean error
  - 500 "Not ready" retried
- All tests pass

**Estimated tests:** ~8

## DTO Update Strategy

For each endpoint:
1. Run `python3 tools/generate_dtos.py` to produce the wire-faithful generated record
2. Compare generated output against current DTO
3. Update the current DTO to include all fields from the recording
4. Use `[JsonExtensionData]` on all response DTOs for future-proofing
5. Types match wire format exactly — `long` for integers, `decimal` for fractional, `string` for strings. No `JsonNumberHandling`, no custom converters in first pass.
6. XML doc comments from `ibkr_api.md` where available

## Files

### Existing (from spike)
| Path | Purpose |
|---|---|
| `tests/.../TestHarness.cs` | Shared WireMock + DI setup |
| `tests/.../TestCredentials.cs` | Synthetic OAuth credentials |
| `tests/.../MockLstServer.cs` | Server-side DH exchange |
| `tests/.../Fixtures/FixtureLoader.cs` | Fixture file loading |
| `tests/.../Portfolio/PortfolioTests.cs` | Portfolio tests (2 from spike) |
| `tests/.../Orders/OrderTests.cs` | Order tests (4 from spike) |

### New Files
| Path | Purpose |
|---|---|
| `tests/.../Fixtures/{Module}/*.json` | Generated and hand-crafted fixture files |
| `tests/.../Session/SessionTests.cs` | Session endpoint tests |
| `tests/.../Accounts/AccountsTests.cs` | Accounts endpoint tests |
| `tests/.../Pipeline/AuthFailureTests.cs` | 401 retry limit and re-auth failure tests |
| `tests/.../Pipeline/ResilienceTests.cs` | Server failure and retry tests |

### Modified Files
| Path | Change |
|---|---|
| `src/IbkrConduit/Session/TokenRefreshHandler.cs` | Add retry limit, handle re-auth failures |
| `src/IbkrConduit/Portfolio/IIbkrPortfolioApiModels.cs` | Update Position, LedgerEntry, AccountSummaryEntry, PartitionedPnl |
| `src/IbkrConduit/Orders/IIbkrOrderApiModels.cs` | Update LiveOrder, OrderStatus, Trade, WhatIfResponse |
| `src/IbkrConduit/Session/IIbkrSessionApiModels.cs` | Update SsodhInitResponse, TickleResponse, AuthStatusResponse |
| `src/IbkrConduit/Accounts/IIbkrAccountApiModels.cs` | Update IserverAccountsResponse, SwitchAccountResponse |

## Scope Boundaries

### In Scope
- Integration tests for 20 trade automation endpoints
- Fixture generation from recordings
- Hand-crafted fixtures for untriggerable scenarios
- DTO updates for all tested endpoints
- `TokenRefreshHandler` fix for retry limit and re-auth failure handling
- Pipeline-level resilience tests

### Out of Scope
- Contracts, MarketData, Alerts, Watchlists, FYI tests (not needed for trade automation)
- E2E tests (existing in `_Old` project, not touched)
- Unit tests (may need minor updates for DTO changes and `TokenRefreshHandler` fix)
- WebSocket streaming tests
- Flex Web Service tests
