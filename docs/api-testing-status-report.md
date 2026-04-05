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

### Wrapper Column

| Value | Meaning |
|-------|---------|
| Yes | Refit interface + Operations facade method exposed on `IIbkrClient` |
| Internal | Refit interface exists but used internally (session/auth lifecycle) |
| No | No Refit interface or wrapper — not yet implemented |
| N/S | Not supported — endpoint belongs to a separate API surface (Gateway, FA Model Portfolio) |

## Watchlists (4 endpoints)

| Endpoint | Method | Wrapper | DTO Source | Integration Tests | Notes |
|----------|--------|---------|-----------|-------------------|-------|
| `/iserver/watchlist` | POST | Yes | Recording | Yes + 401 | Full CRUD validated with 17 capture scenarios |
| `/iserver/watchlists` | GET | Yes | Recording | Yes + 401 | Includes user_lists wrapper structure |
| `/iserver/watchlist?id=` | GET | Yes | Recording | Yes + 401 | Verified first/second call field behavior |
| `/iserver/watchlist?id=` | DELETE | Yes | Recording | Yes + 401 | Warm session returns 200 for nonexistent IDs |

**Findings:** Duplicate ID = silent overwrite. Empty/missing rows accepted. Invalid conids accepted. ID must be numeric.

## Alerts (6 endpoints)

| Endpoint | Method | Wrapper | DTO Source | Integration Tests | Notes |
|----------|--------|---------|-----------|-------------------|-------|
| `/iserver/account/{id}/alerts` | GET | Yes | OpenAPI | Yes + 401 | Paper account returns empty list |
| `/iserver/account/mta` | GET | Yes | Recording | Yes + 401 | Full 26-field MTA response captured |
| `/iserver/account/alert/{id}` | GET | Yes | OpenAPI | Yes + 401 | Requires `?type=Q` query param |
| `/iserver/account/{id}/alert` | POST | Yes | OpenAPI | Yes + 401 | Returns 403 on paper accounts with OAuth |
| `/iserver/account/{id}/alert/activate` | POST | Yes | OpenAPI | Yes + 401 | New endpoint — was missing from Refit |
| `/iserver/account/{id}/alert/{id}` | DELETE | Yes | OpenAPI | Yes + 401 | Response has Success/FailureList fields |

**Findings:** Alert creation returns 403 on paper accounts with OAuth — known limitation. MTA alert always exists. Get detail with ID 0 returns 200 with error body.

## Session (7 endpoints)

| Endpoint | Method | Wrapper | DTO Source | Integration Tests | Notes |
|----------|--------|---------|-----------|-------------------|-------|
| `/iserver/auth/status` | POST | Internal | Recording | Yes | Used by session lifecycle |
| `/iserver/auth/ssodh/init` | POST | Internal | Recording | Yes | Used by session lifecycle |
| `/logout` | POST | Internal | Recording | Yes | Used by session lifecycle |
| `/tickle` | POST | Internal | Recording | Yes | Missing ssoExpires/collission/userId in some responses |
| `/iserver/questions/suppress/reset` | POST | Internal | Recording | Yes | Used by session lifecycle |
| `/iserver/reauthenticate` | POST | No | Not captured | Not Tested | Deprecated endpoint |
| `/sso/validate` | GET | No | Not captured | Not Tested | CP Gateway / OAuth 2.0 only |

## Accounts (11 endpoints)

| Endpoint | Method | Wrapper | DTO Source | Integration Tests | Notes |
|----------|--------|---------|-----------|-------------------|-------|
| `/iserver/accounts` | GET | Yes | Recording | Yes + 401 | Full response with acctProps, allowFeatures, chartPeriods |
| `/iserver/account` | POST | Yes | Recording | Yes + 401 | Switch account — returns "Account already set" for same account |
| `/iserver/account/pnl/partitioned` | GET | Yes | Recording | Yes + 401 | Existing tests |
| `/iserver/account/{id}/summary` | GET | Yes | Recording | Yes + 401 | Typed DTO with numeric fields |
| `/iserver/account/{id}/summary/available_funds` | GET | Yes | Recording | Yes + 401 | Segmented dict response with string values |
| `/iserver/account/{id}/summary/balances` | GET | Yes | Recording | Yes + 401 | Segmented dict; abbreviated field names |
| `/iserver/account/{id}/summary/margins` | GET | Yes | Recording | Yes + 401 | Segmented dict; abbreviated field names |
| `/iserver/account/{id}/summary/market_value` | GET | Yes | Recording | Yes + 401 | Dynamic currency keys |
| `/iserver/account/search/{pattern}` | GET | Yes | OpenAPI | Yes + 401 | DYNACCT only — returns 503 on non-DYNACCT accounts; synthetic WireMock fixture |
| `/iserver/dynaccount` | POST | Yes | OpenAPI | Yes + 401 | DYNACCT only — returns 401 on non-DYNACCT accounts; synthetic WireMock fixture |
| `/acesws/{id}/signatures-and-owners` | GET | Yes | Recording | Yes + 401 | Contains PII (name, DOB) — fixtures sanitized |

**Findings:** DYNACCT endpoints return 503/401 for non-DYNACCT accounts. Signatures endpoint returns full owner entity details including DOB. Switch account with invalid ID returns 500, missing body returns 400. Summary sub-endpoints return string-formatted values (`"1,005,254 USD"`) with heavily abbreviated field names; constants class provided. Invalid account on summary returns 400, not 401.

## Contract (17 endpoints)

| Endpoint | Method | Wrapper | DTO Source | Integration Tests | Notes |
|----------|--------|---------|-----------|-------------------|-------|
| `/trsrv/secdef` | GET | Yes | Recording | Yes + 401 | OA has 1 field, recording has 34 |
| `/trsrv/all-conids` | GET | Yes | Recording | Yes + 401 | |
| `/iserver/contract/{id}/info` | GET | Yes | Recording | Yes + 401 | |
| `/iserver/currency/pairs` | GET | Yes | Recording | Yes + 401 | |
| `/iserver/exchangerate` | GET | Yes | Recording | Yes + 401 | |
| `/iserver/contract/{id}/info-and-rules` | GET | Yes | Recording | Yes + 401 | Full contract details + rules in one call |
| `/iserver/contract/{id}/algos` | GET | Yes | Recording | Yes + 401 | Returns algo list with params; invalid conid returns 503 |
| `/iserver/secdef/bond-filters` | GET | Yes | Recording | Yes + 401 | Invalid issuerId returns 200 (IBKR quirk) |
| `/iserver/secdef/search` | GET | Yes | Recording | Yes + 401 | |
| `/iserver/secdef/search` | POST | Yes | Recording | Yes + 401 | POST variant; same response shape as GET |
| `/iserver/contract/rules` | POST | Yes | Recording | Yes + 401 | Field naming mismatches with OA |
| `/iserver/secdef/info` | GET | Yes | Recording | Yes + 401 | |
| `/iserver/secdef/strikes` | GET | Yes | Recording | Yes + 401 | |
| `/trsrv/futures` | GET | Yes | Recording | Yes + 401 | |
| `/trsrv/stocks` | GET | Yes | Recording | Yes + 401 | |
| `/trsrv/secdef/schedule` | GET | Yes | Not captured | Not Tested | |
| `/contract/trading-schedule` | GET | Yes | Recording | Yes + 401 | Dynamic date keys; distinct from /trsrv/secdef/schedule |

## Market Data (5 endpoints)

| Endpoint | Method | Wrapper | DTO Source | Integration Tests | Notes |
|----------|--------|---------|-----------|-------------------|-------|
| `/iserver/marketdata/snapshot` | GET | Yes | Recording | Yes + 401 | Dynamic field IDs as keys |
| `/iserver/marketdata/history` | GET | Yes | Recording | Yes + 401 | |
| `/md/regsnapshot` | GET | Yes | Not captured | Not Tested | $0.01 per request |
| `/iserver/marketdata/unsubscribe` | POST | Yes | Recording | Yes | |
| `/iserver/marketdata/unsubscribeall` | GET | Yes | Recording | Yes | |

## Orders (7 endpoints)

| Endpoint | Method | Wrapper | DTO Source | Integration Tests | Notes |
|----------|--------|---------|-----------|-------------------|-------|
| `/iserver/account/{id}/orders` | POST | Yes | Recording | Yes + 401 | Place order |
| `/iserver/reply/{id}` | POST | Yes | Recording | Yes + 401 | Reply confirmation |
| `/iserver/notification` | POST | No | Not captured | Not Tested | Server prompt response |
| `/iserver/account/{id}/orders/whatif` | POST | Yes | Recording | Yes + 401 | Preview/WhatIf |
| `/iserver/account/{id}/order/{id}` | DELETE | Yes | Recording | Yes + 401 | Cancel order |
| `/iserver/account/{id}/order/{id}` | POST | Yes | Recording | Yes + 401 | Modify order — body is direct object, not wrapped in orders array |
| `/iserver/questions/suppress` | POST | Internal | Recording | Yes | Used by session lifecycle |

## Order Monitoring (3 endpoints)

| Endpoint | Method | Wrapper | DTO Source | Integration Tests | Notes |
|----------|--------|---------|-----------|-------------------|-------|
| `/iserver/account/orders` | GET | Yes | Recording | Yes + 401 | Live orders |
| `/iserver/account/order/status/{id}` | GET | Yes | Recording | Yes + 401 | |
| `/iserver/account/trades` | GET | Yes | Recording | Yes + 401 | |

## Portfolio (14 endpoints)

| Endpoint | Method | Wrapper | DTO Source | Integration Tests | Notes |
|----------|--------|---------|-----------|-------------------|-------|
| `/portfolio/accounts` | GET | Yes | Recording | Yes + 401 | faclient vs faClient casing |
| `/portfolio/subaccounts` | GET | Yes | Recording | Yes + 401 | |
| `/portfolio/subaccounts2` | GET | Yes | Recording | Yes + 401 | Paged response with metadata; not in OpenAPI |
| `/portfolio/{id}/meta` | GET | Yes | Recording | Yes + 401 | |
| `/portfolio/{id}/allocation` | GET | Yes | Recording | Yes + 401 | |
| `/portfolio/{id}/combo/positions` | GET | Yes | Recording | Yes + 401 | Returns empty array when no combos held; needs market-hours capture for populated response |
| `/portfolio/allocation` | POST | Yes | Recording | Yes + 401 | Consolidated allocation; not in OpenAPI |
| `/portfolio/{id}/positions/{page}` | GET | Yes | Recording | Yes + 401 | |
| `/portfolio2/{id}/positions` | GET | Yes | Recording | Yes + 401 | Real-time positions; not in OpenAPI |
| `/portfolio/{id}/position/{conid}` | GET | Yes | Recording | Yes + 401 | |
| `/portfolio/{id}/positions/invalidate` | POST | Yes | Recording | Yes | |
| `/portfolio/{id}/summary` | GET | Yes | Recording | Yes + 401 | OA has 120 fields, we have 6 |
| `/portfolio/{id}/ledger` | GET | Yes | Recording | Yes + 401 | |
| `/portfolio/positions/{conid}` | GET | Yes | Recording | Yes + 401 | |

## Portfolio Analyst (3 endpoints)

| Endpoint | Method | Wrapper | DTO Source | Integration Tests | Notes |
|----------|--------|---------|-----------|-------------------|-------|
| `/pa/allperiods` | POST | Yes | Recording | Yes + 401 | |
| `/pa/performance` | POST | Yes | Recording | Yes + 401 | |
| `/pa/transactions` | POST | Yes | Recording | Yes + 401 | |

## FYIs and Notifications (12 endpoints)

| Endpoint | Method | Wrapper | DTO Source | Integration Tests | Notes |
|----------|--------|---------|-----------|-------------------|-------|
| `/fyi/unreadnumber` | GET | Yes | Recording | Yes + 401 | Returns `{"BN": 0}` for zero unread |
| `/fyi/settings` | GET | Yes | Recording | Yes + 401 | Array of ~27 notification setting items |
| `/fyi/settings/{typecode}` | POST | Yes | Recording | Yes + 401 | Succeeds even for invalid typecodes |
| `/fyi/disclaimer/{typecode}` | GET | Yes | Recording | Yes + 401 | |
| `/fyi/disclaimer/{typecode}` | PUT | Yes | Recording | Yes + 401 | |
| `/fyi/deliveryoptions` | GET | Yes | Recording | Yes + 401 | |
| `/fyi/deliveryoptions/device` | POST | Yes | OpenAPI | Yes + 401 | Synthetic fixture; same ack response shape |
| `/fyi/deliveryoptions/{deviceId}` | DELETE | Yes | OpenAPI | Yes + 401 | Returns 404 HTML for nonexistent; facade returns Result\<bool\> |
| `/fyi/deliveryoptions/email` | PUT | Yes | OpenAPI | Yes + 401 | Synthetic fixture; same ack response shape |
| `/fyi/notifications` | GET | Yes | Recording | Yes + 401 | |
| `/fyi/notifications/{id}` | PUT | Yes | Recording | Yes + 401 | |
| `/fyi/notifications` | GET | Yes | Recording | Yes + 401 | GetMoreNotifications variant |

## Scanner (3 endpoints)

| Endpoint | Method | Wrapper | DTO Source | Integration Tests | Notes |
|----------|--------|---------|-----------|-------------------|-------|
| `/iserver/scanner/params` | GET | Yes | Recording | Yes + 401 | Large params response; rate limited 1 req/15 min |
| `/iserver/scanner/run` | POST | Yes | Recording | Yes + 401 | Returns up to 50 contracts |
| `/hmds/scanner` | POST | Yes | OpenAPI | Yes + 401 | Returns 404 on live API; synthetic fixture |

## Event Contracts (5 endpoints)

| Endpoint | Method | Wrapper | DTO Source | Integration Tests | Notes |
|----------|--------|---------|-----------|-------------------|-------|
| `/forecast/category/tree` | GET | Yes | Recording | Yes + 401 | Large response; 3-level category hierarchy with markets |
| `/forecast/contract/market` | GET | Yes | Recording | Yes + 401 | Returns contracts with conid, strike, expiry for an underlying |
| `/forecast/contract/rules` | GET | Yes | Recording | Yes + 401 | Trading rules for specific event contract |
| `/forecast/contract/details` | GET | Yes | Recording | Yes + 401 | Yes/No conids, question, resolution details |
| `/forecast/contract/schedules` | GET | Yes | Recording | Yes + 401 | Daily trading schedules with open/close times |

## OAuth (4 endpoints)

| Endpoint | Method | Wrapper | DTO Source | Integration Tests | Notes |
|----------|--------|---------|-----------|-------------------|-------|
| `/oauth/request_token` | POST | Internal | — | — | OAuth 1.0a request token |
| `/oauth/access_token` | POST | Internal | — | — | OAuth 1.0a access token |
| `/oauth/live_session_token` | POST | Internal | — | — | OAuth 1.0a LST generation |
| `/oauth2/api/v1/token` | POST | Internal | — | — | OAuth 2.0 token endpoint |

## FA Allocation Management (8 endpoints)

> **Not currently supported.** These endpoints manage FA allocation groups and presets for sub-account allocation. Requires FA account type.

| Endpoint | Method | Wrapper | DTO Source | Integration Tests | Notes |
|----------|--------|---------|-----------|-------------------|-------|
| `/iserver/account/allocation/accounts` | GET | N/S | — | — | FA only |
| `/iserver/account/allocation/group/single` | POST | N/S | — | — | FA only |
| `/iserver/account/allocation/group` | GET | N/S | — | — | FA only |
| `/iserver/account/allocation/group` | POST | N/S | — | — | FA only |
| `/iserver/account/allocation/group` | PUT | N/S | — | — | FA only |
| `/iserver/account/allocation/group/delete` | POST | N/S | — | — | FA only |
| `/iserver/account/allocation/presets` | GET | N/S | — | — | FA only |
| `/iserver/account/allocation/presets` | POST | N/S | — | — | FA only |

## FA Model Portfolio (10 endpoints)

> **Not currently supported.** These endpoints manage FA model-based portfolio allocation (invest/divest into models). Requires FA account type.

| Endpoint | Method | Wrapper | DTO Source | Integration Tests | Notes |
|----------|--------|---------|-----------|-------------------|-------|
| `/fa/fa-preset/get` | POST | N/S | — | — | Get model preset |
| `/fa/fa-preset/save` | POST | N/S | — | — | Set model preset |
| `/fa/model/accounts-details` | POST | N/S | — | — | Get models accounts |
| `/fa/model/invest-divest` | POST | N/S | — | — | Invest account into model |
| `/fa/model/invest-divest-positions` | POST | N/S | — | — | Summary of accounts invested in model |
| `/fa/model/list` | POST | N/S | — | — | Request all models |
| `/fa/model/positions` | POST | N/S | — | — | Request model positions |
| `/fa/model/save` | POST | N/S | — | — | Set model allocations |
| `/fa/model/submit-transfers` | POST | N/S | — | — | Submit transfers |
| `/fa/model/summary` | POST | N/S | — | — | Request model summary |

## Gateway — Account Management (11 endpoints)

> **Not currently supported.** The Gateway API (`/gw/api/v1/`) is a separate API surface for account onboarding, KYC, registration, and login messages. Uses different auth model (SSO sessions, signed JWTs).

| Endpoint | Method | Wrapper | DTO Source | Integration Tests | Notes |
|----------|--------|---------|-----------|-------------------|-------|
| `/gw/api/v1/accounts` | GET | N/S | — | — | Retrieve processed application |
| `/gw/api/v1/accounts` | POST | N/S | — | — | Create account |
| `/gw/api/v1/accounts` | PATCH | N/S | — | — | Update account |
| `/gw/api/v1/accounts/documents` | POST | N/S | — | — | Submit agreements and disclosures |
| `/gw/api/v1/accounts/login-messages` | GET | N/S | — | — | Get login messages |
| `/gw/api/v1/accounts/status` | GET | N/S | — | — | Get status of accounts |
| `/gw/api/v1/accounts/{id}/details` | GET | N/S | — | — | Get account information |
| `/gw/api/v1/accounts/{id}/kyc` | GET | N/S | — | — | Retrieve Au10Tix URL |
| `/gw/api/v1/accounts/{id}/login-messages` | GET | N/S | — | — | Get login message by account |
| `/gw/api/v1/accounts/{id}/status` | GET | N/S | — | — | Get status by account |
| `/gw/api/v1/accounts/{id}/tasks` | GET | N/S | — | — | Get registration tasks |

## Gateway — Banking and Transfers (18 endpoints)

> **Not currently supported.** Bank instructions, cash/asset transfers (ACATS, ATON, FOP, DWAC), and instruction management.

| Endpoint | Method | Wrapper | DTO Source | Integration Tests | Notes |
|----------|--------|---------|-----------|-------------------|-------|
| `/gw/api/v1/bank-instructions` | POST | N/S | — | — | Manage bank instructions |
| `/gw/api/v1/bank-instructions/query` | POST | N/S | — | — | View bank instructions |
| `/gw/api/v1/bank-instructions:bulk` | POST | N/S | — | — | Bulk create bank instructions |
| `/gw/api/v1/client-instructions/{id}` | GET | N/S | — | — | Get status for client instruction |
| `/gw/api/v1/external-asset-transfers` | POST | N/S | — | — | Transfer positions externally |
| `/gw/api/v1/external-asset-transfers:bulk` | POST | N/S | — | — | Bulk external asset transfers |
| `/gw/api/v1/external-cash-transfers` | POST | N/S | — | — | Transfer cash externally |
| `/gw/api/v1/external-cash-transfers/query` | POST | N/S | — | — | View cash balances |
| `/gw/api/v1/external-cash-transfers:bulk` | POST | N/S | — | — | Bulk external cash transfers |
| `/gw/api/v1/instruction-sets/{id}` | GET | N/S | — | — | Get status for instruction set |
| `/gw/api/v1/instructions/cancel` | POST | N/S | — | — | Cancel request |
| `/gw/api/v1/instructions/cancel:bulk` | POST | N/S | — | — | Bulk cancel instructions |
| `/gw/api/v1/instructions/query` | POST | N/S | — | — | Get transaction history |
| `/gw/api/v1/instructions/{id}` | GET | N/S | — | — | Get status for instruction |
| `/gw/api/v1/internal-asset-transfers` | POST | N/S | — | — | Transfer positions internally |
| `/gw/api/v1/internal-asset-transfers:bulk` | POST | N/S | — | — | Bulk internal asset transfers |
| `/gw/api/v1/internal-cash-transfers` | POST | N/S | — | — | Transfer cash internally |
| `/gw/api/v1/internal-cash-transfers:bulk` | POST | N/S | — | — | Bulk internal cash transfers |

## Gateway — Reports (6 endpoints)

> **Not currently supported.** Statements, tax documents, and trade confirmations.

| Endpoint | Method | Wrapper | DTO Source | Integration Tests | Notes |
|----------|--------|---------|-----------|-------------------|-------|
| `/gw/api/v1/statements` | POST | N/S | — | — | Generate statements |
| `/gw/api/v1/statements/available` | GET | N/S | — | — | Fetch available report dates |
| `/gw/api/v1/tax-documents` | POST | N/S | — | — | Fetch tax forms |
| `/gw/api/v1/tax-documents/available` | GET | N/S | — | — | Fetch available tax documents |
| `/gw/api/v1/trade-confirmations` | POST | N/S | — | — | Fetch trade confirmations |
| `/gw/api/v1/trade-confirmations/available` | GET | N/S | — | — | Fetch available trade confirmation dates |

## Gateway — Utilities (9 endpoints)

> **Not currently supported.** Enumerations, forms, participating banks, request tracking, and username validation.

| Endpoint | Method | Wrapper | DTO Source | Integration Tests | Notes |
|----------|--------|---------|-----------|-------------------|-------|
| `/gw/api/v1/balances/query` | POST | N/S | — | — | View cash balances |
| `/gw/api/v1/enumerations/complex-asset-transfer` | GET | N/S | — | — | Get participating brokers by asset type |
| `/gw/api/v1/enumerations/{type}` | GET | N/S | — | — | Get enumerations |
| `/gw/api/v1/forms` | GET | N/S | — | — | Get forms |
| `/gw/api/v1/forms/required-forms` | GET | N/S | — | — | Get required forms |
| `/gw/api/v1/participating-banks` | GET | N/S | — | — | Get participating banks |
| `/gw/api/v1/requests` | GET | N/S | — | — | Get requests by timeframe |
| `/gw/api/v1/requests/{id}/status` | GET | N/S | — | — | Get request status |
| `/gw/api/v1/validations/usernames/{username}` | GET | N/S | — | — | Verify user availability |

## Gateway — Pre-Trade Compliance (2 endpoints)

> **Not currently supported.** Pre-trade compliance restriction management.

| Endpoint | Method | Wrapper | DTO Source | Integration Tests | Notes |
|----------|--------|---------|-----------|-------------------|-------|
| `/gw/api/v1/restrictions` | POST | N/S | — | — | Apply PTC CSV |
| `/gw/api/v1/restrictions/verify` | POST | N/S | — | — | Verify PTC CSV |

## Gateway — SSO and Echo (4 endpoints)

> **Not currently supported.** SSO session creation and connectivity echo testing.

| Endpoint | Method | Wrapper | DTO Source | Integration Tests | Notes |
|----------|--------|---------|-----------|-------------------|-------|
| `/gw/api/v1/sso-browser-sessions` | POST | N/S | — | — | Create SSO browser session |
| `/gw/api/v1/sso-sessions` | POST | N/S | — | — | Create SSO API session |
| `/gw/api/v1/echo/https` | GET | N/S | — | — | HTTPS echo test |
| `/gw/api/v1/echo/signed-jwt` | POST | N/S | — | — | Signed JWT echo test |

## Summary

### Client Portal Web API

| Category | Endpoints | Wrapped | Recording Validated | OpenAPI Only | Not Tested | Integration Tests |
|----------|-----------|--------:|--------------------:|-------------:|-----------:|------------------:|
| Watchlists | 4 | 4 | 4 | 0 | 0 | 8 (4+4 401) |
| Alerts | 6 | 6 | 1 | 5 | 0 | 12 (6+6 401) |
| Session | 7 | 5 (int) | 5 | 0 | 2 | 6 |
| Accounts | 11 | 11 | 9 | 2 | 0 | 20 (10+10 401) |
| Contract | 17 | 16 | 15 | 0 | 1 | 26 |
| Market Data | 5 | 5 | 4 | 0 | 1 | 8 |
| Orders | 7 | 5 + 1 (int) | 6 | 0 | 1 | 12 |
| Order Monitoring | 3 | 3 | 3 | 0 | 0 | 6 |
| Portfolio | 14 | 14 | 14 | 0 | 0 | 27 |
| Portfolio Analyst | 3 | 3 | 3 | 0 | 0 | 6 |
| FYIs | 12 | 12 | 8 | 3 | 0 | 24 (12+12 401) |
| Scanner | 3 | 3 | 2 | 1 | 0 | 6 (3+3 401) |
| Event Contracts | 5 | 5 | 5 | 0 | 0 | 10 (5+5 401) |
| **Subtotal** | **97** | **86 + 6 int** | **79** | **11** | **5** | **171** |

### Not Currently Supported

| Category | Endpoints | Status | Notes |
|----------|-----------|--------|-------|
| OAuth | 4 | Internal | Auth lifecycle — not exposed |
| FA Allocation | 8 | N/S | FA group/preset allocation, requires FA account |
| FA Model Portfolio | 10 | N/S | FA model-based allocation, requires FA account |
| GW — Account Mgmt | 11 | N/S | Account onboarding, KYC, registration |
| GW — Banking | 18 | N/S | Bank instructions, cash/asset transfers |
| GW — Reports | 6 | N/S | Statements, tax docs, trade confirmations |
| GW — Utilities | 9 | N/S | Enumerations, forms, banks, request tracking |
| GW — Pre-Trade Compliance | 2 | N/S | PTC CSV restrictions |
| GW — SSO and Echo | 4 | N/S | SSO sessions, echo testing |
| **Subtotal** | **72** | — | — |

### Grand Total

| | Endpoints | Wrapped | Not Supported |
|-|-----------|--------:|--------------:|
| **All** | **169** | **86 + 10 int** | **68 (N/S)** |

**Not wrapped (4 in Client Portal):** 1 order endpoint (notification — needs WebSocket trigger), 1 contract endpoint (trsrv/secdef/schedule — not captured), 2 deprecated session endpoints (reauthenticate, sso/validate).
