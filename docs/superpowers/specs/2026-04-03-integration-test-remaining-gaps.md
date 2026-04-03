# Integration Test Remaining Gaps

## Context

As of 2026-04-03, the new integration test suite (`Tests.Integration`) has 94 tests covering Session, Accounts, Portfolio, Orders, Contracts, Market Data, and Pipeline (auth failures, resilience, error normalization, session lifecycle). These use the full DI stack via `AddIbkrClient` + WireMock — no fakes.

The old test project (`Tests.Integration_Old`) has ~85 WireMock tests + ~40 E2E tests. The WireMock tests below are the scenarios from the old project that have **no equivalent** in the new project yet. Once these gaps are filled, the old project's WireMock tests can be retired (E2E tests are a separate concern).

## Gap 1: Alerts (4 endpoints, no success recordings)

**Old tests:** `Alerts/AlertEndpointTests.cs` — 4 WireMock tests
- `CreateOrModifyAlertAsync_ReturnsCreatedAlert`
- `GetAlertsAsync_ReturnsAlertList`
- `GetAlertDetailAsync_ReturnsAlertDetail`
- `DeleteAlertAsync_ReturnsDeleteResult`

**Refit interface:** `IIbkrAlertApi` — 4 methods
- `POST /iserver/account/{accountId}/alert` → `CreateAlertResponse`
- `GET /iserver/account/mta` → `List<AlertSummary>`
- `GET /iserver/account/alert/{alertId}` → `AlertDetail`
- `DELETE /iserver/account/{accountId}/alert/{alertId}` → `DeleteAlertResponse`

**Recording status:** Recordings exist but captured **error responses** (403 paper account limitation on create, 400 on detail, 503 on delete). Only the list endpoint (`GET /mta`) returned actual data.

**What's needed:** Hand-craft success fixtures based on the old test stubs and DTO shapes. The old tests have complete WireMock stub JSON that can be converted to fixture format. Also need 401 recovery tests for each endpoint.

**Estimated tests:** ~6 (4 success + 2 representative 401 recovery)

## Gap 2: Watchlists (4 endpoints, no success recordings)

**Old tests:** `Watchlists/WatchlistEndpointTests.cs` — 4 WireMock tests
- `CreateWatchlistAsync_ReturnsCreatedWatchlist`
- `GetWatchlistsAsync_ReturnsWatchlistList`
- `GetWatchlistAsync_ReturnsWatchlistDetail`
- `DeleteWatchlistAsync_ReturnsDeleteResult`

**Refit interface:** `IIbkrWatchlistApi` — 4 methods
- `POST /iserver/watchlist` → `CreateWatchlistResponse`
- `GET /iserver/watchlists` → `List<WatchlistSummary>`
- `GET /iserver/watchlist?id={id}` → `WatchlistDetail`
- `DELETE /iserver/watchlist?id={id}` → `DeleteWatchlistResponse`

**Recording status:** Recordings captured **error responses** (503 on create, 503 on detail, 500 on delete). Only list returned real data (but with system watchlists, not user-created ones).

**What's needed:** Hand-craft success fixtures from old test stubs. The watchlist DTOs use short field names (`C` for conid, `H` for header, `Sym` for symbol).

**Estimated tests:** ~6 (4 success + 2 representative 401 recovery)

## Gap 3: FYI/Notifications (12 endpoints, partial recordings)

**Old tests:** `Fyi/FyiEndpointTests.cs` — 12 WireMock tests
- `GetUnreadCountAsync_ReturnsCount`
- `GetSettingsAsync_ReturnsSettingsList`
- `UpdateSettingAsync_ReturnsAcknowledgement`
- `GetDisclaimerAsync_ReturnsDisclaimer`
- `MarkDisclaimerReadAsync_ReturnsAcknowledgement`
- `GetDeliveryOptionsAsync_ReturnsOptions`
- `SetEmailDeliveryAsync_ReturnsAcknowledgement`
- `RegisterDeviceAsync_ReturnsAcknowledgement`
- `DeleteDeviceAsync_SucceedsWithoutException`
- `GetNotificationsAsync_ReturnsNotifications`
- `GetMoreNotificationsAsync_ReturnsNotifications`
- `MarkNotificationReadAsync_ReturnsReadResponse`

**Refit interface:** `IIbkrFyiApi` — 12 methods

**Recording status:** 7 recordings exist covering:
- `GET /fyi/unreadnumber` — success (`{"BN": 0}`)
- `GET /fyi/settings` — success (array of setting items)
- `GET /fyi/deliveryoptions` — success (devices + email status)
- `GET /fyi/notifications` — success (notification array)
- `GET /fyi/disclaimer/{code}` — success (disclaimer text)
- `POST /fyi/settings/{code}` — success (acknowledgement)
- `PUT /fyi/disclaimer/{code}` — success (acknowledgement)

**Missing recordings (5 endpoints):**
- `PUT /fyi/deliveryoptions/email` (email toggle)
- `POST /fyi/deliveryoptions/device` (device registration)
- `DELETE /fyi/deliveryoptions/device/{deviceId}` (device deletion)
- `GET /fyi/notifications/more?id={id}` (pagination)
- `PUT /fyi/notifications/{notificationId}` (mark as read)

**What's needed:** Create fixtures from the 7 existing recordings. Hand-craft fixtures for the 5 missing endpoints (simple acknowledgement responses). FYI DTOs use short field names (`BN`, `FC`, `FN`, `FD`, `H`, `A`, `R`, `D`, `MS`, `MD`, `ID`).

**Estimated tests:** ~16 (12 success + 4 representative 401 recovery)

## Gap 4: Allocation/FA (8 endpoints, no recordings)

**Old tests:** `Allocation/AllocationEndpointTests.cs` — 8 WireMock tests
- `GetAccountsAsync_ReturnsSubAccounts`
- `GetGroupsAsync_ReturnsGroupList`
- `AddGroupAsync_ReturnsSuccess`
- `GetGroupAsync_ReturnsSingleGroup`
- `DeleteGroupAsync_ReturnsSuccess`
- `ModifyGroupAsync_ReturnsSuccess`
- `GetPresetsAsync_ReturnsPresets`
- `SetPresetsAsync_ReturnsSuccess`

**Refit interface:** `IIbkrAllocationApi` — 8 methods

**Recording status:** **No recordings.** FA (Financial Advisor) features require a multi-account FA setup. The paper trading account used for recordings is an individual account, so these endpoints either return errors or empty responses.

**What's needed:** Hand-craft all fixtures from old test stubs. Alternatively, if an FA paper account becomes available, run the recording tool to capture real responses.

**Estimated tests:** ~12 (8 success + 4 representative 401 recovery)

## Gap 5: Flex Web Service (3 scenarios, no recordings)

**Old tests:** `Flex/FlexIntegrationTests.cs` — 3 WireMock tests
- `ExecuteQueryAsync_TwoStepFlow_ReturnsParsedResult` (send query + poll for result)
- `ExecuteQueryAsync_PollsOnInProgress_ReturnsOnSecondAttempt` (polling with retry)
- `ExecuteQueryAsync_ErrorResponse_ThrowsFlexQueryException`

**Recording status:** **No recordings.** Flex Web Service uses a separate authentication mechanism (Flex token, not OAuth) and a different base URL (`https://ndcdyn.interactivebrokers.com`). The recording tool targets the Client Portal API, not Flex.

**What's needed:** Hand-craft fixtures. The Flex client (`FlexClient`) is registered separately in DI with its own HttpClient (no OAuth pipeline). Tests would need a different harness setup or a dedicated Flex test harness.

**Estimated tests:** ~4 (3 scenarios + 1 representative error)

## Gap 6: WebSocket Streaming (not applicable for WireMock)

**Old tests:** `Streaming/StreamingTests.cs` — 1 E2E-only test

**Status:** WebSocket streaming cannot be meaningfully tested with WireMock HTTP stubs. The old project only has an E2E test. This gap is expected and should remain an E2E-only concern.

**No action needed** for the WireMock integration test suite.

## Priority Recommendation

| Priority | Domain | Effort | Why |
|----------|--------|--------|-----|
| 1 | FYI/Notifications | Medium | 7 of 12 recordings exist, only 5 need hand-crafting |
| 2 | Alerts | Low | 4 simple CRUD endpoints, old stubs provide fixture data |
| 3 | Watchlists | Low | 4 simple CRUD endpoints, old stubs provide fixture data |
| 4 | Allocation/FA | Medium | All hand-crafted, but FA features are niche |
| 5 | Flex Web Service | Medium | Different auth mechanism, needs separate harness |
| 6 | WebSocket | N/A | E2E only, no WireMock integration test needed |

**Total remaining:** ~44 tests across 5 domains (excluding WebSocket)

## How to Generate Missing Recordings

For domains with partial or no recordings, use the existing recording tool:

1. **Alerts/Watchlists:** These failed on paper accounts due to permission issues. Try with a live (non-paper) account, or accept hand-crafted fixtures.
2. **FYI missing endpoints:** Add the 5 missing FYI scenarios to the recording tool's scenario runner, then re-run.
3. **Allocation:** Requires an FA account type. If available, add allocation scenarios to the recorder.
4. **Flex:** Requires a separate recording approach since Flex uses token auth + different base URL.
