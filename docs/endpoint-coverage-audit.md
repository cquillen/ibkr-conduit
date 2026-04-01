# IBKR API Endpoint Coverage Audit

**Last updated:** 2026-04-01
**Source:** [IBKR Client Portal Web API v1.0 Documentation](https://www.interactivebrokers.com/campus/ibkr-api-page/cpapi-v1)

**Total: 38 implemented / 83 documented = 46%**

---

## Session / Auth

| Endpoint | Method | Status | Notes |
|---|---|---|---|
| `/oauth/live_session_token` | POST | ✅ | LiveSessionTokenClient |
| `/iserver/auth/ssodh/init` | POST | ✅ | SessionManager |
| `/tickle` | POST | ✅ | TickleTimer |
| `/logout` | POST | ✅ | SessionManager.DisposeAsync |
| `/iserver/questions/suppress` | POST | ✅ | SessionManager |
| `/iserver/questions/suppress/reset` | POST | ❌ | |
| `/iserver/auth/status` | GET | ❌ | |
| `/iserver/reauthenticate` | POST | ❌ | Deprecated by IBKR |
| `/sso/validate` | GET | ❌ | |
| `/oauth/request_token` | POST | ❌ | Third-party OAuth only |
| `/oauth/access_token` | POST | ❌ | Third-party OAuth only |

---

## Portfolio

| Endpoint | Method | Status | Notes |
|---|---|---|---|
| `/portfolio/accounts` | GET | ✅ | |
| `/portfolio/{id}/positions/{page}` | GET | ✅ | Paginated |
| `/portfolio/{id}/summary` | GET | ✅ | |
| `/portfolio/{id}/ledger` | GET | ✅ | |
| `/portfolio/{id}/meta` | GET | ✅ | |
| `/portfolio/{id}/allocation` | GET | ✅ | Single account |
| `/portfolio/{id}/position/{conid}` | GET | ✅ | |
| `/portfolio/positions/{conid}` | GET | ✅ | Position + contract info |
| `/portfolio/{id}/positions/invalidate` | POST | ✅ | Cache invalidation |
| `/pa/performance` | POST | ✅ | |
| `/pa/transactions` | POST | ✅ | |
| `/portfolio/allocation` | POST | ✅ | Consolidated allocation |
| `/portfolio/{id}/combo/positions` | GET | ✅ | Combination positions |
| `/portfolio2/{id}/positions` | GET | ✅ | Real-time positions (no cache) |
| `/portfolio/subaccounts` | GET | ✅ | FA/IBroker only |
| `/portfolio/subaccounts2` | GET | ✅ | FA/IBroker paginated |
| `/pa/allperiods` | POST | ✅ | All period performance |
| `/iserver/account/pnl/partitioned` | GET | ✅ | Partitioned P&L |

---

## Orders

| Endpoint | Method | Status | Notes |
|---|---|---|---|
| `/iserver/account/{id}/orders` | POST | ✅ | Place order (with question/reply) |
| `/iserver/reply/{replyId}` | POST | ✅ | Auto-confirm questions |
| `/iserver/account/{id}/order/{orderId}` | DELETE | ✅ | Cancel order |
| `/iserver/account/orders` | GET | ✅ | Live orders (session-scoped) |
| `/iserver/account/trades` | GET | ✅ | Trades (session-scoped) |
| `/iserver/account/{id}/order/{orderId}` | POST | ❌ | Modify order |
| `/iserver/account/{id}/orders/whatif` | POST | ❌ | What-if / commission preview |
| `/iserver/account/order/status/{orderId}` | GET | ❌ | Single order status |

---

## Contracts

| Endpoint | Method | Status | Notes |
|---|---|---|---|
| `/iserver/secdef/search` | GET | ✅ | Symbol search |
| `/iserver/contract/{conid}/info` | GET | ✅ | Contract details |
| `/iserver/secdef/info` | GET | ❌ | Derivatives info |
| `/iserver/secdef/strikes` | GET | ❌ | Options strikes |
| `/iserver/contract/rules` | POST | ❌ | Trading rules |
| `/trsrv/secdef` | GET | ❌ | Security definitions by conid |
| `/trsrv/all-conids` | GET | ❌ | All conids by exchange |
| `/trsrv/futures` | GET | ❌ | Futures by symbol |
| `/trsrv/stocks` | GET | ❌ | Stocks by symbol |
| `/trsrv/secdef/schedule` | GET | ❌ | Trading schedule |
| `/iserver/currency/pairs` | GET | ❌ | Currency pairs |
| `/iserver/exchangerate` | GET | ❌ | Exchange rate |

---

## Market Data

| Endpoint | Method | Status | Notes |
|---|---|---|---|
| `/iserver/marketdata/snapshot` | GET | ✅ | With pre-flight handling |
| `/iserver/marketdata/history` | GET | ✅ | Historical OHLCV bars |
| `/md/regsnapshot` | GET | ✅ | Regulatory snapshot ($0.01/req) |
| `/iserver/marketdata/unsubscribe` | POST | ✅ | Unsubscribe conid |
| `/iserver/marketdata/unsubscribeall` | GET | ✅ | Unsubscribe all |
| `/iserver/scanner/run` | POST | ✅ | Market scanner |
| `/iserver/scanner/params` | GET | ✅ | Scanner parameters |
| `/hmds/scanner` | POST | ✅ | HMDS market scanner |

---

## Alerts — Not Implemented

| Endpoint | Method | Status |
|---|---|---|
| `POST /iserver/account/{id}/alert` | POST | ❌ |
| `GET /iserver/account/mta` | GET | ❌ |
| `GET /iserver/account/alert/{id}` | GET | ❌ |
| `DELETE /iserver/account/{id}/alert/{id}` | DELETE | ❌ |

---

## Accounts — Not Implemented

| Endpoint | Method | Status |
|---|---|---|
| `GET /iserver/accounts` | GET | ❌ |
| `POST /iserver/account` | POST | ❌ |
| `POST /iserver/dynaccount` | POST | ❌ |
| `GET /iserver/account/search/{pattern}` | GET | ❌ |
| `GET /iserver/account/{id}` | GET | ❌ |

---

## FA Allocation — Not Implemented (FA/IBroker Only)

| Endpoint | Method | Status |
|---|---|---|
| `GET /iserver/account/allocation/accounts` | GET | ❌ |
| `GET /iserver/account/allocation/group` | GET | ❌ |
| `POST /iserver/account/allocation/group` | POST | ❌ |
| `POST /iserver/account/allocation/group/single` | POST | ❌ |
| `POST /iserver/account/allocation/group/delete` | POST | ❌ |
| `PUT /iserver/account/allocation/group` | PUT | ❌ |
| `GET /iserver/account/allocation/presets` | GET | ❌ |
| `POST /iserver/account/allocation/presets` | POST | ❌ |

---

## FYI / Notifications — Not Implemented

| Endpoint | Method | Status |
|---|---|---|
| `GET /fyi/unreadnumber` | GET | ❌ |
| `GET /fyi/settings` | GET | ❌ |
| `POST /fyi/settings/{typecode}` | POST | ❌ |
| `GET /fyi/disclaimer/{typecode}` | GET | ❌ |
| `PUT /fyi/disclaimer/{typecode}` | PUT | ❌ |
| `GET /fyi/deliveryoptions` | GET | ❌ |
| `PUT /fyi/deliveryoptions/email` | PUT | ❌ |
| `POST /fyi/deliveryoptions/device` | POST | ❌ |
| `DELETE /fyi/deliveryoptions/{deviceId}` | DELETE | ❌ |
| `GET /fyi/notifications` | GET | ❌ |
| `GET /fyi/notifications/more` | GET | ❌ |
| `PUT /fyi/notifications/{notificationId}` | PUT | ❌ |

---

## Watchlists — Not Implemented

| Endpoint | Method | Status |
|---|---|---|
| `POST /iserver/watchlist` | POST | ❌ |
| `GET /iserver/watchlists` | GET | ❌ |
| `GET /iserver/watchlist` | GET | ❌ |
| `DELETE /iserver/watchlist` | DELETE | ❌ |
