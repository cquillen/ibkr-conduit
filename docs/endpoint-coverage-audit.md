# IBKR API Endpoint Coverage Audit

**Last updated:** 2026-04-01
**Source:** [IBKR Client Portal Web API v1.0 Documentation](https://www.interactivebrokers.com/campus/ibkr-api-page/cpapi-v1)

**Total: 75 implemented / 83 documented = 90%**

---

## Session / Auth

| Endpoint | Method | Status | Notes |
|---|---|---|---|
| `/oauth/live_session_token` | POST | ✅ | LiveSessionTokenClient |
| `/iserver/auth/ssodh/init` | POST | ✅ | SessionManager |
| `/tickle` | POST | ✅ | TickleTimer |
| `/logout` | POST | ✅ | SessionManager.DisposeAsync |
| `/iserver/questions/suppress` | POST | ✅ | SessionManager |
| `/iserver/questions/suppress/reset` | POST | ✅ | IIbkrSessionApi |
| `/iserver/auth/status` | GET | ✅ | IIbkrSessionApi |
| `/iserver/reauthenticate` | POST | ✅ | IIbkrSessionApi (Obsolete) |
| `/sso/validate` | GET | ✅ | IIbkrSessionApi |
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
| `/iserver/account/{id}/order/{orderId}` | POST | ✅ | Modify order |
| `/iserver/account/{id}/orders/whatif` | POST | ✅ | What-if / commission preview |
| `/iserver/account/order/status/{orderId}` | GET | ✅ | Single order status |

---

## Contracts

| Endpoint | Method | Status | Notes |
|---|---|---|---|
| `/iserver/secdef/search` | GET | ✅ | Symbol search |
| `/iserver/contract/{conid}/info` | GET | ✅ | Contract details |
| `/iserver/secdef/info` | GET | ✅ | Derivatives info |
| `/iserver/secdef/strikes` | GET | ✅ | Options strikes |
| `/iserver/contract/rules` | POST | ✅ | Trading rules |
| `/trsrv/secdef` | GET | ✅ | Security definitions by conid |
| `/trsrv/all-conids` | GET | ✅ | All conids by exchange |
| `/trsrv/futures` | GET | ✅ | Futures by symbol |
| `/trsrv/stocks` | GET | ✅ | Stocks by symbol |
| `/trsrv/secdef/schedule` | GET | ✅ | Trading schedule |
| `/iserver/currency/pairs` | GET | ✅ | Currency pairs |
| `/iserver/exchangerate` | GET | ✅ | Exchange rate |

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

## Alerts

| Endpoint | Method | Status | Notes |
|---|---|---|---|
| `POST /iserver/account/{id}/alert` | POST | ✅ | IIbkrAlertApi |
| `GET /iserver/account/mta` | GET | ✅ | IIbkrAlertApi |
| `GET /iserver/account/alert/{id}` | GET | ✅ | IIbkrAlertApi |
| `DELETE /iserver/account/{id}/alert/{id}` | DELETE | ✅ | IIbkrAlertApi |

---

## Accounts

| Endpoint | Method | Status | Notes |
|---|---|---|---|
| `GET /iserver/accounts` | GET | ✅ | IIbkrAccountApi |
| `POST /iserver/account` | POST | ✅ | IIbkrAccountApi |
| `POST /iserver/dynaccount` | POST | ✅ | IIbkrAccountApi |
| `GET /iserver/account/search/{pattern}` | GET | ✅ | IIbkrAccountApi |
| `GET /iserver/account/{id}` | GET | ✅ | IIbkrAccountApi |

---

## FA Allocation

| Endpoint | Method | Status | Notes |
|---|---|---|---|
| `GET /iserver/account/allocation/accounts` | GET | ✅ | IIbkrAllocationApi |
| `GET /iserver/account/allocation/group` | GET | ✅ | IIbkrAllocationApi |
| `POST /iserver/account/allocation/group` | POST | ✅ | IIbkrAllocationApi |
| `POST /iserver/account/allocation/group/single` | POST | ✅ | IIbkrAllocationApi |
| `POST /iserver/account/allocation/group/delete` | POST | ✅ | IIbkrAllocationApi |
| `PUT /iserver/account/allocation/group` | PUT | ✅ | IIbkrAllocationApi |
| `GET /iserver/account/allocation/presets` | GET | ✅ | IIbkrAllocationApi |
| `POST /iserver/account/allocation/presets` | POST | ✅ | IIbkrAllocationApi |

---

## FYI / Notifications

| Endpoint | Method | Status | Notes |
|---|---|---|---|
| `GET /fyi/unreadnumber` | GET | ✅ | IIbkrFyiApi |
| `GET /fyi/settings` | GET | ✅ | IIbkrFyiApi |
| `POST /fyi/settings/{typecode}` | POST | ✅ | IIbkrFyiApi |
| `GET /fyi/disclaimer/{typecode}` | GET | ✅ | IIbkrFyiApi |
| `PUT /fyi/disclaimer/{typecode}` | PUT | ✅ | IIbkrFyiApi |
| `GET /fyi/deliveryoptions` | GET | ✅ | IIbkrFyiApi |
| `PUT /fyi/deliveryoptions/email` | PUT | ✅ | IIbkrFyiApi |
| `POST /fyi/deliveryoptions/device` | POST | ✅ | IIbkrFyiApi |
| `DELETE /fyi/deliveryoptions/{deviceId}` | DELETE | ✅ | IIbkrFyiApi |
| `GET /fyi/notifications` | GET | ✅ | IIbkrFyiApi |
| `GET /fyi/notifications/more` | GET | ✅ | IIbkrFyiApi |
| `PUT /fyi/notifications/{notificationId}` | PUT | ✅ | IIbkrFyiApi |

---

## Watchlists

| Endpoint | Method | Status | Notes |
|---|---|---|---|
| `POST /iserver/watchlist` | POST | ✅ | IIbkrWatchlistApi |
| `GET /iserver/watchlists` | GET | ✅ | IIbkrWatchlistApi |
| `GET /iserver/watchlist` | GET | ✅ | IIbkrWatchlistApi |
| `DELETE /iserver/watchlist` | DELETE | ✅ | IIbkrWatchlistApi |
