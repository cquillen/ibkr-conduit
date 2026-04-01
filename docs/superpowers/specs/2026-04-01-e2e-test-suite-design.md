# End-to-End Test Suite Design

## Goal

Design a comprehensive E2E test suite that exercises every implemented endpoint against a real IBKR paper account. Tests are organized as multi-step scenarios that simulate real trading workflows. A response recording infrastructure captures real API responses in WireMock-compatible format for future offline replay tests.

## Design Principles

1. **Test identity**: Every test-created entity (order, alert, watchlist, allocation group) uses a prefix `E2E-{guid}` so cleanup can find and remove only test artifacts, never user data.
2. **Pre-existing state resilience**: Tests never assume the account is empty. They query current state, work relative to it, and clean up only what they created.
3. **Cleanup-on-failure**: Each scenario wraps its "act" phase in try/finally, with cleanup in the finally block.
4. **Serialization**: All E2E tests use `[Collection("IBKR E2E")]` to prevent session competition.
5. **Market-hours independence**: Order tests use GTC limit orders priced far from market ($1.00 for a stock trading at $500+) so they don't fill and work outside market hours.
6. **DI pipeline**: All tests use the full `AddIbkrClient` DI pipeline exactly as a real consumer would — no manual HttpClient wiring.

## Response Recording Infrastructure

### Purpose

Capture real IBKR API request/response pairs during E2E runs for later use as WireMock fixtures. This enables building offline integration tests that replay real responses without hitting the paper account.

### Activation

Opt-in via environment variable `IBKR_RECORD_RESPONSES=true`. When not set, the recording handler is a no-op passthrough with zero overhead.

### Mechanism

A `RecordingDelegatingHandler` sits at the outermost position in the HTTP pipeline (before all other handlers). It:
1. Passes requests through the pipeline normally
2. Captures the request method, path, query string, and body (if any)
3. Captures the response status code, headers, and body
4. Writes a WireMock-compatible JSON mapping file to disk

### Output Format

Each recorded interaction is a WireMock mapping JSON file:

```json
{
  "Request": {
    "Path": "/v1/api/portfolio/accounts",
    "Methods": ["GET"],
    "Body": null
  },
  "Response": {
    "StatusCode": 200,
    "Headers": {
      "Content-Type": "application/json"
    },
    "Body": "[{\"id\":\"DU1234567\",\"accountTitle\":\"Paper Trading\"}]"
  },
  "Metadata": {
    "Scenario": "portfolio-deep-dive",
    "Step": 1,
    "RecordedAt": "2026-04-01T12:00:00Z"
  }
}
```

### File Organization

```
tests/IbkrConduit.Tests.Integration/Recordings/
  scenario-01-session-discovery/
    001-POST-ssodh-init.json
    002-GET-sso-validate.json
    003-GET-auth-status.json
    ...
  scenario-04-order-lifecycle/
    001-GET-secdef-search.json
    002-POST-whatif.json
    003-POST-place-order.json
    004-POST-reply.json
    005-GET-order-status.json
    ...
```

File naming: `{step:D3}-{method}-{endpoint-slug}.json`

### Sanitization

Before writing to disk, the handler redacts sensitive data:
- `Authorization` header value → `"REDACTED"`
- `oauth_token` query parameter in URLs → `"REDACTED"`
- `Cookie` header → `"api=REDACTED"`
- OAuth signature parameters → `"REDACTED"`

Account IDs are preserved (paper account IDs aren't secret, and WireMock needs them for URL matching).

### Scenario Context

Each E2E test scenario sets a recording context (scenario name) that the handler uses to organize output files. This is passed via `HttpRequestMessage.Options` or a shared `RecordingContext` object injected alongside the handler.

```csharp
public class RecordingContext
{
    public string? ScenarioName { get; set; }
    public int StepCounter { get; set; }

    public int NextStep() => Interlocked.Increment(ref StepCounter);
}
```

### What This Does NOT Do (Out of Scope)

Building the WireMock-based replay test suite that consumes the recordings. That is future work. This spec only covers recording the responses.

---

## Endpoints That May Not Be Fully Testable

| Endpoint | Reason | Mitigation |
|---|---|---|
| FA Allocation (8 endpoints) | FA/IBroker accounts only — individual paper accounts may not support it | Attempt calls; skip gracefully on "not supported" errors. May need an FA paper account for full coverage. |
| `fyi/deliveryoptions/device` (POST) | Requires a real mobile device token | Use a synthetic token; verify API accepts the request format even if registration fails. |
| `fyi/deliveryoptions/{deviceId}` (DELETE) | Depends on successful device registration | Chain with device POST; skip if POST returned an error. |
| `md/regsnapshot` | Costs $0.01 per request | Gate behind separate env var `IBKR_ALLOW_PAID_ENDPOINTS`. |
| `iserver/reauthenticate` | Obsolete endpoint | Excluded from E2E scope. |
| `portfolio/subaccounts`, `subaccounts2` | FA only — will likely return empty or error for individual accounts | Call and verify response shape; don't assert content. |

---

## Scenario 1: Account Discovery & Session Validation

**Use case**: A new user connects the library and discovers their accounts.

**Steps**:
1. Session initializes automatically via DI pipeline (covers: `oauth/live_session_token`, `ssodh/init`, `tickle`, `questions/suppress`)
2. Validate SSO session (`sso/validate`)
3. Check auth status (`iserver/auth/status`)
4. Get iserver accounts (`iserver/accounts`)
5. Search for account by pattern — use first 2 chars of account ID (`iserver/account/search/{pattern}`)
6. Get account info for found account (`iserver/account/{id}`)
7. Switch account — switch to current account (`iserver/account` POST)
8. Set dynamic account (`iserver/dynaccount` POST)
9. Reset suppressed questions (`iserver/questions/suppress/reset`)

**Cleanup**: Re-suppress the configured message IDs to restore original state.

**Error & Edge Cases**:
- Search for non-existent account pattern (e.g., "ZZZZZ") → verify empty results, no exception
- Get account info for bogus account ID (e.g., "INVALID999") → verify appropriate error response (ApiException)
- Switch to non-existent account ID → verify error response

**Endpoints covered (11)**: `oauth/live_session_token`, `ssodh/init`, `tickle`, `questions/suppress`, `sso/validate`, `iserver/auth/status`, `iserver/accounts`, `iserver/account/search`, `iserver/account/{id}`, `iserver/account` (switch), `iserver/dynaccount`, `questions/suppress/reset`

---

## Scenario 2: Research a Trade

**Use case**: A trader researches a stock, explores its options chain, checks futures, and looks up FX rates.

**Steps**:
1. Search for "AAPL" (`iserver/secdef/search`) — capture conid
2. Get contract details for AAPL conid (`iserver/contract/{conid}/info`)
3. Get trading rules for AAPL (`iserver/contract/rules`)
4. Get security definitions by conid (`trsrv/secdef`)
5. Get trading schedule for AAPL (`trsrv/secdef/schedule`)
6. Get option strikes for AAPL — nearest month (`iserver/secdef/strikes`)
7. Get security definition info for a specific option month (`iserver/secdef/info`)
8. Get stocks by symbol — "AAPL" (`trsrv/stocks`)
9. Get futures by symbol — "ES" (`trsrv/futures`)
10. Get all conids for exchange "NASDAQ" (`trsrv/all-conids`)
11. Get currency pairs for "USD" (`iserver/currency/pairs`)
12. Get exchange rate USD/EUR (`iserver/exchangerate`)

**Cleanup**: None needed (all read-only).

**Error & Edge Cases**:
- Search for non-existent symbol (e.g., "ZZZZNOTREAL") → verify empty results, no exception
- Get contract details for invalid conid (e.g., "0" or "999999999") → verify error response
- Get option strikes for a non-optionable instrument or invalid month → verify error or empty response
- Get exchange rate for same currency (e.g., USD/USD) → verify response (should return 1.0 or an error)
- Get trading rules with conid = 0 → verify error response

**Endpoints covered (12)**: All 12 contract endpoints.

---

## Scenario 3: Market Data & Scanners

**Use case**: A trader checks live quotes, pulls historical charts, and runs market scanners.

**Steps**:
1. Get snapshot for SPY — first call triggers pre-flight (`iserver/marketdata/snapshot`)
2. Wait briefly, get snapshot again — data should be populated
3. Get historical bars for SPY — 1D bars, 5 day period (`iserver/marketdata/history`)
4. Get scanner parameters — discover available scanner types (`iserver/scanner/params`)
5. Run a market scanner — e.g., "TOP_PERC_GAIN" (`iserver/scanner/run`)
6. Run an HMDS scanner (`hmds/scanner`)
7. *(Opt-in, gated by `IBKR_ALLOW_PAID_ENDPOINTS`)*: Get regulatory snapshot for SPY (`md/regsnapshot`)
8. Unsubscribe SPY from market data (`iserver/marketdata/unsubscribe`)
9. Unsubscribe all market data (`iserver/marketdata/unsubscribeall`)

**Cleanup**: Unsubscribe all (step 9).

**Error & Edge Cases**:
- Get snapshot for invalid conid (e.g., 0) → verify error or empty fields
- Get historical bars with invalid period (e.g., "0min") → verify error response
- Unsubscribe a conid that was never subscribed → verify no-op or graceful error
- Run scanner with invalid scan type → verify error response

**Endpoints covered (8)**: All 8 market data endpoints (`regsnapshot` conditional).

---

## Scenario 4: Full Order Lifecycle

**Use case**: A trader previews a trade, places it, monitors it, modifies it, then cancels it.

**Steps**:
1. Search for SPY conid (`iserver/secdef/search`)
2. Preview the trade — what-if for 1 share of SPY at $1.00 GTC limit (`iserver/account/{id}/orders/whatif`)
3. Place a GTC limit order: 1 share of SPY at $1.00 (far below market, won't fill) (`iserver/account/{id}/orders` POST)
4. Auto-confirm any questions (`iserver/reply/{replyId}` — handled implicitly by OrderOperations)
5. Get order status (`iserver/account/order/status/{orderId}`)
6. Get live orders — verify our order appears (`iserver/account/orders` GET)
7. Modify the order — change price to $1.01 (`iserver/account/{id}/order/{orderId}` POST)
8. Get order status again — verify new price
9. Cancel the order (`iserver/account/{id}/order/{orderId}` DELETE)
10. Get live orders — verify order no longer appears
11. Get trades — verify response shape (`iserver/account/trades`)

**Cleanup**: In finally block, list live orders, cancel any that match our E2E order (identified by the $1.00-$1.01 price on SPY). This handles cases where the test fails mid-scenario.

**Error & Edge Cases**:
- Get order status for non-existent order ID (e.g., "000000000") → verify error response (ApiException or empty)
- Cancel a non-existent order ID → verify error response
- Modify a non-existent order ID → verify error response
- Cancel the same order twice (after successful cancel in step 9) → verify error or "order not found" response
- What-if with invalid conid (e.g., 0) → verify error response
- Place order with quantity 0 → verify rejection/error

**Endpoints covered (8)**: All 8 order endpoints.

---

## Scenario 5: Portfolio Deep Dive

**Use case**: A trader reviews their full portfolio — positions, balances, P&L, performance, and transaction history.

**Depends on**: At least one position existing in the account. If no positions exist, position-specific steps verify empty/null responses instead of asserting content.

**Steps**:
1. Get portfolio accounts (`portfolio/accounts`)
2. Get account summary (`portfolio/{id}/summary`)
3. Get ledger — cash balances by currency (`portfolio/{id}/ledger`)
4. Get account metadata (`portfolio/{id}/meta`)
5. Get account allocation (`portfolio/{id}/allocation`)
6. Get positions page 0 (`portfolio/{id}/positions/0`)
7. If positions exist: get position by conid for first position (`portfolio/{id}/position/{conid}`)
8. If positions exist: get position + contract info across all accounts (`portfolio/positions/{conid}`)
9. Get real-time positions — bypasses server cache (`portfolio2/{id}/positions`)
10. Invalidate portfolio cache (`portfolio/{id}/positions/invalidate`)
11. Get combo/spread positions — may be empty (`portfolio/{id}/combo/positions`)
12. Get consolidated allocation across accounts (`portfolio/allocation` POST)
13. Get performance for last 1 month (`pa/performance`)
14. Get transaction history (`pa/transactions`)
15. Get all-periods performance (`pa/allperiods`)
16. Get partitioned P&L (`iserver/account/pnl/partitioned`)
17. Get subaccounts — will return empty for individual accounts (`portfolio/subaccounts`)
18. Get subaccounts paged (`portfolio/subaccounts2`)

**Cleanup**: None needed (all read-only except cache invalidate, which is harmless).

**Error & Edge Cases**:
- Get position by conid for a conid not held in the portfolio (e.g., a random conid) → verify empty list response
- Get position + contract info for non-existent conid → verify empty or error response
- Get performance with empty account list → verify error or empty response
- Get positions page 999 (far beyond actual pages) → verify empty list response

**Endpoints covered (18)**: All 18 portfolio endpoints.

---

## Scenario 6: Alert Management

**Use case**: A trader sets up a price alert on SPY, reviews it, then removes it.

**Steps**:
1. Look up SPY conid (reuse search)
2. Create a price alert: name "E2E-{guid}", condition: SPY price > $9999 (`iserver/account/{id}/alert` POST)
3. List all alerts — verify our alert appears (`iserver/account/mta` GET)
4. Get alert detail by ID (`iserver/account/alert/{id}` GET)
5. Delete the alert (`iserver/account/{id}/alert/{id}` DELETE)
6. List alerts again — verify our alert is gone

**Cleanup**: In finally block, list all alerts, find any with name matching `E2E-` prefix, delete them.

**Error & Edge Cases**:
- Get alert detail for non-existent alert ID (e.g., "0") → verify error response
- Delete a non-existent alert ID → verify error response
- Delete the same alert twice (after successful delete in step 5) → verify error or "not found" response

**Endpoints covered (4)**: All 4 alert endpoints.

---

## Scenario 7: Watchlist Management

**Use case**: A trader creates a watchlist for tracking stocks, reviews it, then removes it.

**Steps**:
1. Create a watchlist named "E2E-{guid}" containing SPY and AAPL conids (`iserver/watchlist` POST)
2. List all watchlists — verify our watchlist appears (`iserver/watchlists` GET)
3. Get the specific watchlist by ID (`iserver/watchlist` GET with query param)
4. Delete the watchlist (`iserver/watchlist` DELETE)
5. List watchlists again — verify ours is gone

**Cleanup**: In finally block, list all watchlists, find any with name matching `E2E-` prefix, delete them.

**Error & Edge Cases**:
- Get watchlist with non-existent ID (e.g., "FAKE-ID-99999") → verify error response
- Delete a non-existent watchlist ID → verify error response
- Delete the same watchlist twice (after successful delete in step 4) → verify error or "not found" response

**Endpoints covered (4)**: All 4 watchlist endpoints.

---

## Scenario 8: Notification Preferences

**Use case**: A trader configures their notification settings and reviews recent notifications.

**Steps**:
1. Get unread notification count (`fyi/unreadnumber`)
2. Get current FYI settings — capture original state (`fyi/settings`)
3. Pick a setting typecode from the response — toggle its enabled state (`fyi/settings/{typecode}` POST)
4. Restore the original setting value (`fyi/settings/{typecode}` POST)
5. Get disclaimer for a typecode (`fyi/disclaimer/{typecode}`)
6. Mark disclaimer as read (`fyi/disclaimer/{typecode}` PUT)
7. Get delivery options (`fyi/deliveryoptions`)
8. Toggle email delivery — set and restore (`fyi/deliveryoptions/email` PUT)
9. Attempt to register a device with synthetic token (`fyi/deliveryoptions/device` POST) — capture response, may fail
10. If device registered: delete device (`fyi/deliveryoptions/{deviceId}` DELETE)
11. Get notifications (`fyi/notifications`)
12. If notifications exist: get more notifications for pagination (`fyi/notifications/more`)
13. If notifications exist: mark one as read (`fyi/notifications/{notificationId}` PUT)

**Cleanup**: Settings restored inline (steps 4, 8). Device deleted if registered (step 10).

**Error & Edge Cases**:
- Update setting with invalid typecode (e.g., "NONEXISTENT") → verify error response
- Get disclaimer for invalid typecode → verify error response
- Mark non-existent notification as read (e.g., ID "0") → verify error response
- Delete a non-existent device ID → verify error response

**Endpoints covered (12)**: All 12 FYI endpoints (device endpoints best-effort).

---

## Scenario 9: FA Allocation Management

**Use case**: A financial advisor manages allocation groups and presets.

**Gate**: Run `iserver/account/allocation/accounts` as a probe. If it returns an error indicating the account type doesn't support allocations, skip the entire scenario with a descriptive message.

**Steps**:
1. Get allocation accounts (`iserver/account/allocation/accounts`) — if fails, skip scenario
2. Get allocation groups (`iserver/account/allocation/group`)
3. Create a group named "E2E-{guid}" (`iserver/account/allocation/group` POST)
4. Get the specific group detail (`iserver/account/allocation/group/single` POST)
5. Modify the group — change a property (`iserver/account/allocation/group` PUT)
6. Get presets (`iserver/account/allocation/presets`)
7. Set presets — save current, modify, then restore (`iserver/account/allocation/presets` POST)
8. Delete the group (`iserver/account/allocation/group/delete` POST)

**Cleanup**: In finally block, attempt to delete any group matching our E2E prefix. Restore presets to original.

**Error & Edge Cases**:
- Get detail for non-existent group name → verify error response
- Delete a non-existent group name → verify error response
- Create a group with duplicate name (same name twice) → verify error or idempotent response

**Endpoints covered (8)**: All 8 FA allocation endpoints.

---

## Scenario 10: WebSocket Streaming

**Use case**: A trader subscribes to live market data and account updates via WebSocket.

**Steps**:
1. Connect to WebSocket (exercises `tickle` for session cookie, WebSocket connect with OAuth token)
2. Subscribe to market data for SPY (`smd+{conid}+{"fields":["31","84"]}`)
3. Wait for at least one market data message — verify it contains expected fields
4. Subscribe to order updates (`sor+{}`)
5. Subscribe to account summary (`ssd+{}`)
6. Wait briefly for account summary data
7. Unsubscribe from all topics
8. Disconnect / dispose

**Cleanup**: Dispose handles disconnect and unsubscribe.

**Error & Edge Cases**:
- Subscribe to an invalid topic prefix → verify no crash, no messages received
- Receive messages after unsubscribing from a topic → verify messages are not delivered

**Endpoints covered**: WebSocket connect, subscribe, message receive, unsubscribe, disconnect. Also exercises `tickle` implicitly.

---

## Scenario 11: Flex Web Service

**Use case**: A trader runs a Flex report to get trade confirmations and activity data.

**Gate**: Requires `IBKR_FLEX_TOKEN` and `IBKR_FLEX_QUERY_ID` environment variables.

**Steps**:
1. Execute Flex query with query ID and optional date range (`FlexWebService/SendRequest`)
2. Poll for results — handles "in progress" retries (`FlexWebService/GetStatement`)
3. Parse the XML response
4. Verify the response contains expected elements (FlexStatements, TradeConfirm or similar)

**Cleanup**: None needed (read-only).

**Error & Edge Cases**:
- Execute query with invalid query ID → verify `FlexQueryException` with appropriate error code
- Execute query with invalid token → verify authentication error

**Endpoints covered (2)**: Flex send request, Flex poll for statement.

---

## Endpoint Coverage Matrix

| Category | Total Implemented | Covered by Scenarios | Notes |
|---|---|---|---|
| Session/Auth | 9 | 9 (Scenario 1) | `reauthenticate` excluded (obsolete) |
| Portfolio | 18 | 18 (Scenario 5) | subaccounts may return empty |
| Orders | 8 | 8 (Scenario 4) | |
| Contracts | 12 | 12 (Scenario 2) | |
| Market Data | 8 | 8 (Scenario 3) | `regsnapshot` opt-in |
| Alerts | 4 | 4 (Scenario 6) | |
| Accounts | 5 | 5 (Scenario 1) | |
| FA Allocation | 8 | 8 (Scenario 9) | May skip if not FA account |
| FYI | 12 | 12 (Scenario 8) | Device endpoints best-effort |
| Watchlists | 4 | 4 (Scenario 7) | |
| WebSocket | N/A | Scenario 10 | |
| Flex | 2 | 2 (Scenario 11) | Requires separate token |
| **Total** | **75** | **75** | **100% coverage** |

---

## Implementation Milestones

| Milestone | Scenarios | Endpoints | Rationale |
|---|---|---|---|
| **M0: Recording infrastructure** | — | — | Build `RecordingDelegatingHandler`, `RecordingContext`, file writer. Foundation for all scenarios. |
| **M1: Read-only workflows** | 1 (Session), 2 (Contracts), 3 (Market Data) | 31 | No state mutation, safest to run first. Validates session pipeline works end-to-end. |
| **M2: Stateful CRUD** | 4 (Orders), 6 (Alerts), 7 (Watchlists) | 16 | Create/modify/delete with cleanup. Tests state management and cleanup patterns. |
| **M3: Portfolio & Notifications** | 5 (Portfolio), 8 (FYI) | 30 | Portfolio is read-heavy; FYI has settings toggle/restore. |
| **M4: Streaming & Specialized** | 9 (FA), 10 (WebSocket), 11 (Flex) | 10+ | Conditional/specialized — may skip FA, Flex requires separate token. |

---

## Environment Variables

| Variable | Required | Purpose |
|---|---|---|
| `IBKR_CONSUMER_KEY` | Yes | Gates all E2E tests |
| `IBKR_ACCESS_TOKEN` | Yes | OAuth access token |
| `IBKR_ACCESS_TOKEN_SECRET` | Yes | Encrypted access token secret |
| `IBKR_DH_PRIME` | Yes | Diffie-Hellman prime |
| `IBKR_SIGNING_KEY` | Yes | RSA signing private key (PEM) |
| `IBKR_ENCRYPTION_KEY` | Yes | RSA encryption private key (PEM) |
| `IBKR_RECORD_RESPONSES` | No | Set to `true` to enable response recording |
| `IBKR_ALLOW_PAID_ENDPOINTS` | No | Set to `true` to include `md/regsnapshot` tests |
| `IBKR_FLEX_TOKEN` | No | Flex Web Service token (for Scenario 11) |
| `IBKR_FLEX_QUERY_ID` | No | Flex query ID (for Scenario 11) |
