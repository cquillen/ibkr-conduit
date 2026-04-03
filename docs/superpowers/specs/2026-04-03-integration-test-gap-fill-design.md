# Integration Test Gap-Fill — Design Spec

## Goal

Complete the new WireMock integration test suite by covering all remaining endpoints that have real API recordings but lack tests in the new project. This enables retiring the old integration test project (`Tests.Integration_Old`), which uses fakes instead of the real DI pipeline.

## Background

M1-M4 established a proven test pattern: full DI stack via `AddIbkrClient` pointed at WireMock, fixture files from real API recordings, `TestHarness` for shared setup, 401 recovery on every consumer-pipeline endpoint, and Shouldly assertions. 47 integration tests now cover Session, Accounts, Portfolio (core), Orders, and Pipeline resilience/auth.

The old project has ~85 WireMock tests across all domains. Gap analysis identified these untested domains with real recordings available:

| Domain | Endpoints | Recordings Available |
|--------|-----------|---------------------|
| Portfolio (remaining) | 8 | 12 files |
| Contracts | 10 | 12 files |
| Market Data | 6 | 7 files |
| Error Normalization Pipeline | 4 scenarios | Hand-crafted (from old tests) |
| Session Lifecycle | 3 scenarios | Hand-crafted (existing stubs) |

**Deferred** (needs hand-crafted fixtures or missing recordings): Alerts, Watchlists, FYI, Allocation (FA), Flex, WebSocket Streaming.

## Approach

Single branch, single PR. Same TDD flow as M1-M4:
1. Create fixture files from recordings (sanitized)
2. Write tests — success + 401 recovery per endpoint
3. DTO updates as TDD reveals field mismatches
4. All tests pass, format clean, no regressions

## Endpoints in Scope

### Portfolio Remaining (8 endpoints)

| Endpoint | Method | Recording |
|----------|--------|-----------|
| `/portfolio/{accountId}/meta` | GET | `005-GET-portfolio-DUO873728-meta.json` |
| `/portfolio/{accountId}/allocation` | GET | `006-GET-portfolio-DUO873728-allocation.json` |
| `/portfolio/{accountId}/position/{conid}` | GET | `007-GET-portfolio-DUO873728-position-756733.json` |
| `/portfolio/positions/{conid}` | GET | `008-GET-portfolio-positions-756733.json` |
| `/portfolio/{accountId}/combo/positions` | GET | `009-GET-portfolio-DUO873728-combo-positions.json` |
| `/portfolio2/{accountId}/positions` | GET | `010-GET-portfolio2-DUO873728-positions.json` |
| `/portfolio/subaccounts` | GET | `011-GET-portfolio-subaccounts.json` |
| `/portfolio/{accountId}/positions/invalidate` | POST | `014-POST-portfolio-DUO873728-positions-invalidate.json` |

Additional portfolio analytics endpoints with recordings:
| Endpoint | Method | Recording |
|----------|--------|-----------|
| `/pa/performance` | POST | `015-POST-pa-performance.json` |
| `/pa/transactions` | POST | `016-POST-pa-transactions.json` |
| `/portfolio/allocation` (consolidated) | POST | `017-POST-portfolio-allocation.json` |
| `/pa/allperiods` | POST | `018-POST-pa-allperiods.json` |

**Note:** Several of these return complex nested structures. Tests should spot-check representative fields rather than assert every nested value.

### Contracts (10 endpoints)

| Endpoint | Method | Recording |
|----------|--------|-----------|
| `/iserver/secdef/search` | GET | `001-GET-iserver-secdef-search.json` |
| `/iserver/contract/{conid}/info` | GET | `003-GET-iserver-contract-756733-info.json` |
| `/iserver/secdef/strikes` | GET | `005-GET-iserver-secdef-strikes.json` |
| `/iserver/contract/rules` | POST | `006-POST-iserver-contract-rules.json` |
| `/trsrv/secdef` | GET | `007-GET-trsrv-secdef.json` |
| `/trsrv/all-conids` | GET | `008-GET-trsrv-all-conids.json` |
| `/trsrv/futures` | GET | `009-GET-trsrv-futures.json` |
| `/trsrv/stocks` | GET | `010-GET-trsrv-stocks.json` |
| `/iserver/currency/pairs` | GET | `012-GET-iserver-currency-pairs.json` |
| `/iserver/exchangerate` | GET | `013-GET-iserver-exchangerate.json` |

### Market Data (6 endpoints)

| Endpoint | Method | Recording |
|----------|--------|-----------|
| `/iserver/marketdata/snapshot` | GET | `001-GET-iserver-marketdata-snapshot.json` |
| `/iserver/marketdata/history` | GET | `002-GET-iserver-marketdata-history.json` |
| `/iserver/scanner/params` | GET | `004-GET-iserver-scanner-params.json` |
| `/iserver/scanner/run` | POST | `005-POST-iserver-scanner-run.json` |
| `/iserver/marketdata/unsubscribeall` | GET | `007-GET-iserver-marketdata-unsubscribeall.json` |
| `/iserver/marketdata/unsubscribe` | POST | `012-POST-iserver-marketdata-unsubscribe.json` |

**Note:** The snapshot endpoint returns raw field IDs (e.g., `"31"` for last price). The `MarketDataOperations.GetSnapshotAsync` method translates these to named fields via `MarketDataSnapshot`. The test should verify the translation works through the full pipeline. However, the recording captured a "pre-flight" response (first call returns only conidEx/conid, second call returns data). The test should use a fixture with actual field data, hand-crafted if the recording lacks populated fields.

**Note:** Scanner params returns a very large response (~334KB). The fixture should use a trimmed representative subset.

### Error Normalization Pipeline (4 scenarios)

These are hand-crafted (no recordings needed) and test `ErrorNormalizationHandler` behavior through the full pipeline:

| Scenario | Setup | Expected |
|----------|-------|----------|
| 200 with error body | POST `/iserver/account/{id}/orders` returns 200 with `{"error":"insufficient funds"}` | Throws `IbkrOrderRejectedException` |
| 200 with confirmation | POST `/iserver/account/{id}/orders` returns 200 with confirmation array | Passes through as 200 (no exception) |
| 500 remapped to 404 | POST `/iserver/marketdata/unsubscribe` returns 500 with `{"error":"unknown"}` | Remapped to 404, throws `IbkrApiException` (not retried by ResilienceHandler because remapping happens in ErrorNormalizationHandler which is outer) |
| 429 rate limit | GET `/iserver/marketdata/history` returns 429 with `Retry-After: 60` | Throws `IbkrRateLimitException` with `RetryAfter` = 60s |

**Pipeline ordering note:** ErrorNormalizationHandler is between TokenRefreshHandler (outer) and ResilienceHandler (inner). For the 500-remapped scenario, the response flow is: HttpClient returns 500 → ResilienceHandler retries (sees 500, retries) → after retries exhausted, ErrorNormalizationHandler gets the 500 → remaps to 404 → throws. The test needs to account for this: use the zero-delay resilience pipeline (from M4's `configureServices` pattern) to avoid slow retries, and WireMock should return 500 persistently.

For the 429 scenario: ResilienceHandler retries 429 with backoff. If persistent, ErrorNormalizationHandler gets the 429 and throws `IbkrRateLimitException`. Same pattern — persistent 429 + zero-delay pipeline.

### Session Lifecycle (3 scenarios)

Hand-crafted scenarios testing session initialization and shutdown behavior:

| Scenario | What it verifies |
|----------|-----------------|
| Full initialization ordering | First API call triggers LST → ssodh/init → suppress (if configured) in that order, verified by WireMock request log ordering |
| Clean shutdown calls logout | Disposing `IIbkrClient` / `ServiceProvider` calls POST `/logout`, verified by WireMock log |
| Repeated 401 recovery | Two separate API calls both hit 401 and both recover via re-auth — verifies session recovery is not a one-shot mechanism |

## Test Inventory

### Portfolio Remaining (~16 tests)

| # | Test | Type |
|---|------|------|
| 1 | `GetAccountMeta_ReturnsAllFields` | Success |
| 2 | `GetAccountMeta_401Recovery` | 401 |
| 3 | `GetAccountAllocation_ReturnsAllFields` | Success |
| 4 | `GetAccountAllocation_401Recovery` | 401 |
| 5 | `GetPositionByConid_ReturnsAllFields` | Success |
| 6 | `GetPositionByConid_401Recovery` | 401 |
| 7 | `GetPositionsByConid_ReturnsAllFields` | Success (cross-account) |
| 8 | `GetComboPositions_EmptyResponse` | Success (empty) |
| 9 | `GetRealTimePositions_ReturnsAllFields` | Success |
| 10 | `GetRealTimePositions_401Recovery` | 401 |
| 11 | `GetSubAccounts_ReturnsAllFields` | Success |
| 12 | `InvalidatePositionCache_ReturnsMessage` | Success |
| 13 | `GetPerformance_ReturnsAllFields` | Success |
| 14 | `GetTransactionHistory_ReturnsAllFields` | Success |
| 15 | `GetConsolidatedAllocation_ReturnsAllFields` | Success |
| 16 | `GetAllPeriodsPerformance_ReturnsAllFields` | Success |

**Note:** Analytics endpoints (performance, transactions, consolidated allocation, all-periods) are POST with request bodies. 401 recovery tests are skipped for these since the pattern is already well-proven and they'd each require content buffering verification which is already tested in M1. Similarly, some GET endpoints (combo positions returns empty, subaccounts, invalidate) skip 401 tests where the pattern adds no new coverage.

### Contracts (~14 tests)

| # | Test | Type |
|---|------|------|
| 1 | `SearchBySymbol_ReturnsResults` | Success |
| 2 | `SearchBySymbol_401Recovery` | 401 |
| 3 | `GetContractDetails_ReturnsAllFields` | Success |
| 4 | `GetContractDetails_401Recovery` | 401 |
| 5 | `GetOptionStrikes_ReturnsCallsAndPuts` | Success |
| 6 | `GetTradingRules_ReturnsAllFields` | Success |
| 7 | `GetTradingRules_401Recovery` | 401 |
| 8 | `GetSecurityDefinitions_ReturnsAllFields` | Success |
| 9 | `GetAllConidsByExchange_ReturnsList` | Success |
| 10 | `GetFuturesBySymbol_ReturnsMap` | Success |
| 11 | `GetStocksBySymbol_ReturnsMap` | Success |
| 12 | `GetCurrencyPairs_ReturnsMap` | Success |
| 13 | `GetExchangeRate_ReturnsRate` | Success |
| 14 | `GetExchangeRate_401Recovery` | 401 |

**Note:** 401 recovery is tested on a representative subset (search, details, trading rules, exchange rate) rather than every endpoint — the handler doesn't vary by endpoint.

### Market Data (~10 tests)

| # | Test | Type |
|---|------|------|
| 1 | `GetSnapshot_ReturnsFields` | Success |
| 2 | `GetSnapshot_401Recovery` | 401 |
| 3 | `GetHistory_ReturnsBarData` | Success |
| 4 | `GetHistory_401Recovery` | 401 |
| 5 | `GetScannerParameters_ReturnsParams` | Success |
| 6 | `RunScanner_ReturnsContracts` | Success |
| 7 | `RunScanner_401Recovery` | 401 |
| 8 | `UnsubscribeAll_ReturnsConfirmation` | Success |
| 9 | `Unsubscribe_ReturnsResult` | Success |
| 10 | `Unsubscribe_401Recovery` | 401 |

### Error Normalization Pipeline (4 tests)

| # | Test | Type |
|---|------|------|
| 1 | `OrderResponse_200WithError_ThrowsOrderRejectedException` | Error detection |
| 2 | `OrderResponse_200WithConfirmation_PassesThrough` | Passthrough |
| 3 | `UnsubscribeResponse_500Remapped_ThrowsApiException404` | Status remapping |
| 4 | `HistoryResponse_429WithRetryAfter_ThrowsRateLimitException` | Rate limiting |

### Session Lifecycle (3 tests)

| # | Test | Type |
|---|------|------|
| 1 | `Initialization_CallsEndpointsInCorrectOrder` | Ordering |
| 2 | `Dispose_CallsLogout` | Shutdown |
| 3 | `RepeatedUnauthorized_RecoversTwice` | Multi-recovery |

## Total: ~47 new tests

## DTO Update Strategy

Same as M1-M4: write tests first, let compilation/assertion failures reveal missing fields, update DTOs to match recording shapes. Use `[JsonExtensionData]` on all response DTOs. Likely candidates for updates based on recording shapes:

- **Portfolio**: `AccountInfo` (meta), `AccountAllocation`, real-time position model, `AccountPerformance`, `TransactionHistory`, `AllPeriodsPerformance`
- **Contracts**: Most DTOs look well-mapped already; verify `TradingRules`, `SecurityDefinition`, `ContractDetails` against recordings
- **Market Data**: `MarketDataSnapshotRaw` field mappings, `HistoricalDataResponse`, `ScannerResponse`

## Files

### New Test Files
| Path | Purpose |
|------|---------|
| `tests/.../Contracts/ContractTests.cs` | Contract endpoint tests |
| `tests/.../MarketData/MarketDataTests.cs` | Market data endpoint tests |
| `tests/.../Pipeline/ErrorNormalizationTests.cs` | Error normalization pipeline tests |

### Extended Test Files
| Path | Change |
|------|--------|
| `tests/.../Portfolio/PortfolioTests.cs` | Add remaining portfolio endpoint tests |
| `tests/.../Session/SessionTests.cs` | Add lifecycle tests (init ordering, shutdown, repeated recovery) |

### New Fixture Directories
| Path | Content |
|------|---------|
| `tests/.../Fixtures/Contracts/*.json` | ~10 fixture files from contract recordings |
| `tests/.../Fixtures/MarketData/*.json` | ~6 fixture files from market data recordings |
| `tests/.../Fixtures/Portfolio/*.json` | ~8 additional fixture files for remaining portfolio endpoints |

### Potentially Modified DTO Files
| Path | Reason |
|------|--------|
| `src/.../Portfolio/IIbkrPortfolioApiModels.cs` | Add missing fields for meta, allocation, performance, transaction DTOs |
| `src/.../Contracts/IIbkrContractApiModels.cs` | Add missing fields if TDD reveals mismatches |
| `src/.../MarketData/IIbkrMarketDataApiModels.cs` | Add missing fields if TDD reveals mismatches |

## Scope Boundaries

### In Scope
- Integration tests for all endpoints with real recordings
- Error normalization pipeline tests (hand-crafted)
- Session lifecycle tests (hand-crafted)
- DTO updates as needed
- Fixture files from recordings

### Out of Scope (deferred)
- Alerts (recordings are error responses, needs hand-crafted success fixtures)
- Watchlists (same — recordings are error responses)
- FYI/Notifications (partial recordings, missing 5 endpoints)
- Allocation/FA (no recordings, requires FA account)
- Flex Web Service (no recordings, separate auth)
- WebSocket Streaming (not applicable for WireMock)
- E2E tests (existing in old project, not touched)
- Deleting the old test project (separate effort after all gaps filled)
