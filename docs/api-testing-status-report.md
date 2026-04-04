# API Testing Status Report

Tracks the validation status of each endpoint's response schema. Endpoints are validated through one of two sources:

- **Recording** — Schema validated against an actual live API recording from a paper account. High confidence.
- **OpenAPI** — Schema derived from the OpenAPI spec (`docs/ibkr-web-api-openapi.json`). Used as fallback when live recording cannot be captured (e.g., paper account permission limitations). Medium confidence — may not match actual API behavior.

## Legend

| Status | Meaning |
|--------|---------|
| Recording | DTO validated against live API recording |
| OpenAPI | DTO based on OpenAPI spec (no live recording available) |
| Not Tested | No integration test coverage |

## Watchlists (4 endpoints)

| Endpoint | Method | DTO Source | Integration Tests | Notes |
|----------|--------|-----------|-------------------|-------|
| `/iserver/watchlist` | POST | Recording | Yes + 401 | Full CRUD validated with 17 capture scenarios |
| `/iserver/watchlists` | GET | Recording | Yes + 401 | Includes user_lists wrapper structure |
| `/iserver/watchlist?id=` | GET | Recording | Yes + 401 | Verified first/second call field behavior |
| `/iserver/watchlist?id=` | DELETE | Recording | Yes + 401 | Warm session returns 200 for nonexistent IDs |

**Findings:** Duplicate ID = silent overwrite. Empty/missing rows accepted. Invalid conids accepted. ID must be numeric.

## Alerts (6 endpoints)

| Endpoint | Method | DTO Source | Integration Tests | Notes |
|----------|--------|-----------|-------------------|-------|
| `/iserver/account/{id}/alerts` | GET | OpenAPI | Yes + 401 | Paper account returns empty list |
| `/iserver/account/mta` | GET | Recording | Yes + 401 | Full 26-field MTA response captured |
| `/iserver/account/alert/{id}` | GET | OpenAPI | Yes + 401 | Requires `?type=Q` query param |
| `/iserver/account/{id}/alert` | POST | OpenAPI | Yes + 401 | Returns 403 on paper accounts with OAuth |
| `/iserver/account/{id}/alert/activate` | POST | OpenAPI | Yes + 401 | New endpoint — was missing from Refit |
| `/iserver/account/{id}/alert/{id}` | DELETE | OpenAPI | Yes + 401 | Response has Success/FailureList fields |

**Findings:** Alert creation returns 403 on paper accounts with OAuth — known limitation. MTA alert always exists. Get detail with ID 0 returns 200 with error body.

## Session (6 endpoints)

| Endpoint | Method | DTO Source | Integration Tests | Notes |
|----------|--------|-----------|-------------------|-------|
| `/iserver/auth/status` | POST | Recording | Yes | Existing tests |
| `/iserver/auth/ssodh/init` | POST | Recording | Yes | Existing tests |
| `/logout` | POST | Recording | Yes | Existing tests |
| `/tickle` | POST | Recording | Yes | Missing ssoExpires/collission/userId in some responses |
| `/iserver/reauthenticate` | POST | Not captured | Not Tested | Deprecated endpoint |
| `/sso/validate` | GET | Not captured | Not Tested | CP Gateway / OAuth 2.0 only |

## Accounts (6 endpoints)

| Endpoint | Method | DTO Source | Integration Tests | Notes |
|----------|--------|-----------|-------------------|-------|
| `/iserver/accounts` | GET | Recording | Yes + 401 | Existing tests, needs DTO review |
| `/iserver/account` | POST | Recording | Yes | Switch account |
| `/iserver/account/pnl/partitioned` | GET | Recording | Yes + 401 | Existing tests |
| `/iserver/account/search/{pattern}` | GET | Not captured | Not Tested | DYNACCT only |
| `/iserver/dynaccount` | POST | Not captured | Not Tested | DYNACCT only |
| `/acesws/{id}/signatures-and-owners` | GET | Not captured | Not Tested | |

## Contract (16 endpoints)

| Endpoint | Method | DTO Source | Integration Tests | Notes |
|----------|--------|-----------|-------------------|-------|
| `/trsrv/secdef` | GET | Recording | Yes + 401 | OA has 1 field, recording has 34 |
| `/trsrv/all-conids` | GET | Recording | Yes + 401 | |
| `/iserver/contract/{id}/info` | GET | Recording | Yes + 401 | |
| `/iserver/currency/pairs` | GET | Recording | Yes + 401 | |
| `/iserver/exchangerate` | GET | Recording | Yes + 401 | |
| `/iserver/contract/{id}/info-and-rules` | GET | Not captured | Yes + 401 | Existing tests, needs DTO review |
| `/iserver/contract/{id}/algos` | GET | Not captured | Not Tested | OA has 1 field, we have 13 |
| `/iserver/secdef/bond-filters` | GET | Not captured | Not Tested | |
| `/iserver/secdef/search` | GET | Recording | Yes + 401 | |
| `/iserver/contract/rules` | POST | Recording | Yes + 401 | Field naming mismatches with OA |
| `/iserver/secdef/info` | GET | Recording | Yes + 401 | |
| `/iserver/secdef/strikes` | GET | Recording | Yes + 401 | |
| `/trsrv/futures` | GET | Recording | Yes + 401 | |
| `/trsrv/stocks` | GET | Recording | Yes + 401 | |
| `/trsrv/secdef/schedule` | GET | Not captured | Not Tested | |
| `/contract/trading-schedule` | GET | Not captured | Not Tested | |

## Market Data (5 endpoints)

| Endpoint | Method | DTO Source | Integration Tests | Notes |
|----------|--------|-----------|-------------------|-------|
| `/iserver/marketdata/snapshot` | GET | Recording | Yes + 401 | Dynamic field IDs as keys |
| `/iserver/marketdata/history` | GET | Recording | Yes + 401 | |
| `/md/regsnapshot` | GET | Not captured | Not Tested | $0.01 per request |
| `/iserver/marketdata/unsubscribe` | POST | Recording | Yes | |
| `/iserver/marketdata/unsubscribeall` | GET | Recording | Yes | |

## Orders (7 endpoints)

| Endpoint | Method | DTO Source | Integration Tests | Notes |
|----------|--------|-----------|-------------------|-------|
| `/iserver/account/{id}/orders` | POST | Recording | Yes + 401 | Place order |
| `/iserver/reply/{id}` | POST | Recording | Yes + 401 | Reply confirmation |
| `/iserver/notification` | POST | Not captured | Not Tested | Server prompt response |
| `/iserver/account/{id}/orders/whatif` | POST | Recording | Yes + 401 | Preview/WhatIf |
| `/iserver/account/{id}/order/{id}` | DELETE | Recording | Yes + 401 | Cancel order |
| `/iserver/account/{id}/order/{id}` | POST | Not captured | Yes + 401 | Modify order |
| `/iserver/questions/suppress` | POST | Recording | Yes | |

## Order Monitoring (3 endpoints)

| Endpoint | Method | DTO Source | Integration Tests | Notes |
|----------|--------|-----------|-------------------|-------|
| `/iserver/account/orders` | GET | Recording | Yes + 401 | Live orders |
| `/iserver/account/order/status/{id}` | GET | Recording | Yes + 401 | |
| `/iserver/account/trades` | GET | Recording | Yes + 401 | |

## Portfolio (14 endpoints)

| Endpoint | Method | DTO Source | Integration Tests | Notes |
|----------|--------|-----------|-------------------|-------|
| `/portfolio/accounts` | GET | Recording | Yes + 401 | faclient vs faClient casing |
| `/portfolio/subaccounts` | GET | Recording | Yes + 401 | |
| `/portfolio/subaccounts2` | GET | Not captured | Not Tested | Not in OpenAPI |
| `/portfolio/{id}/meta` | GET | Recording | Yes + 401 | |
| `/portfolio/{id}/allocation` | GET | Recording | Yes + 401 | |
| `/portfolio/{id}/combo/positions` | GET | Not captured | Not Tested | Returns 500 |
| `/portfolio/allocation` | POST | Not captured | Not Tested | Not in OpenAPI |
| `/portfolio/{id}/positions/{page}` | GET | Recording | Yes + 401 | |
| `/portfolio2/{id}/positions` | GET | Not captured | Not Tested | Not in OpenAPI |
| `/portfolio/{id}/position/{conid}` | GET | Recording | Yes + 401 | |
| `/portfolio/{id}/positions/invalidate` | POST | Recording | Yes | |
| `/portfolio/{id}/summary` | GET | Recording | Yes + 401 | OA has 120 fields, we have 6 |
| `/portfolio/{id}/ledger` | GET | Recording | Yes + 401 | |
| `/portfolio/positions/{conid}` | GET | Recording | Yes + 401 | |

## Portfolio Analyst (3 endpoints)

| Endpoint | Method | DTO Source | Integration Tests | Notes |
|----------|--------|-----------|-------------------|-------|
| `/pa/allperiods` | POST | Recording | Yes + 401 | |
| `/pa/performance` | POST | Recording | Yes + 401 | |
| `/pa/transactions` | POST | Recording | Yes + 401 | |

## FYIs and Notifications (12 endpoints)

| Endpoint | Method | DTO Source | Integration Tests | Notes |
|----------|--------|-----------|-------------------|-------|
| `/fyi/unreadnumber` | GET | Recording | Not Tested | Needs integration tests |
| `/fyi/settings` | GET | Recording | Not Tested | |
| `/fyi/settings/{typecode}` | POST | Recording | Not Tested | |
| `/fyi/disclaimer/{typecode}` | GET | Recording | Not Tested | |
| `/fyi/disclaimer/{typecode}` | PUT | Recording | Not Tested | |
| `/fyi/deliveryoptions` | GET | Recording | Not Tested | |
| `/fyi/deliveryoptions/device` | POST | Not captured | Not Tested | |
| `/fyi/deliveryoptions/{deviceId}` | DELETE | Not captured | Not Tested | |
| `/fyi/deliveryoptions/email` | PUT | Not captured | Not Tested | |
| `/fyi/notifications` | GET | Recording | Not Tested | |
| `/fyi/notifications/{id}` | PUT | Recording | Not Tested | |
| `/fyi/notifications` | GET | Recording | Not Tested | |

## Scanner (3 endpoints)

| Endpoint | Method | DTO Source | Integration Tests | Notes |
|----------|--------|-----------|-------------------|-------|
| `/iserver/scanner/params` | GET | Recording | Not Tested | Needs integration tests |
| `/iserver/scanner/run` | POST | Recording | Not Tested | |
| `/hmds/scanner` | POST | Not captured | Not Tested | Not in OpenAPI |

## FA Allocation Management (8 endpoints)

| Endpoint | Method | DTO Source | Integration Tests | Notes |
|----------|--------|-----------|-------------------|-------|
| `/iserver/account/allocation/accounts` | GET | Not captured | Not Tested | FA only |
| `/iserver/account/allocation/group/single` | POST | Not captured | Not Tested | FA only |
| `/iserver/account/allocation/group` | GET | Not captured | Not Tested | FA only |
| `/iserver/account/allocation/group` | POST | Not captured | Not Tested | FA only |
| `/iserver/account/allocation/group` | PUT | Not captured | Not Tested | FA only |
| `/iserver/account/allocation/group/delete` | POST | Not captured | Not Tested | FA only |
| `/iserver/account/allocation/presets` | GET | Not captured | Not Tested | FA only |
| `/iserver/account/allocation/presets` | POST | Not captured | Not Tested | FA only |

## Event Contracts (5 endpoints)

| Endpoint | Method | DTO Source | Integration Tests | Notes |
|----------|--------|-----------|-------------------|-------|
| `/forecast/category/tree` | GET | Not captured | Not Tested | |
| `/forecast/contract/market` | GET | Not captured | Not Tested | |
| `/forecast/contract/rules` | GET | Not captured | Not Tested | |
| `/forecast/contract/details` | GET | Not captured | Not Tested | |
| `/forecast/contract/schedules` | GET | Not captured | Not Tested | |

## Summary

| Category | Endpoints | Recording Validated | OpenAPI Only | Not Tested | Integration Tests |
|----------|-----------|--------------------:|-------------:|-----------:|------------------:|
| Watchlists | 4 | 4 | 0 | 0 | 8 (4+4 401) |
| Alerts | 6 | 1 | 5 | 0 | 12 (6+6 401) |
| Session | 6 | 4 | 0 | 2 | 6 |
| Accounts | 6 | 3 | 0 | 3 | 4 |
| Contract | 16 | 11 | 0 | 5 | 20 |
| Market Data | 5 | 4 | 0 | 1 | 8 |
| Orders | 7 | 5 | 0 | 2 | 10 |
| Order Monitoring | 3 | 3 | 0 | 0 | 6 |
| Portfolio | 14 | 10 | 0 | 4 | 22 |
| Portfolio Analyst | 3 | 3 | 0 | 0 | 6 |
| FYIs | 12 | 8 | 0 | 4 | 0 |
| Scanner | 3 | 2 | 0 | 1 | 0 |
| FA Allocation | 8 | 0 | 0 | 8 | 0 |
| Event Contracts | 5 | 0 | 0 | 5 | 0 |
| **Total** | **98** | **58** | **5** | **35** | **102** |
