# Disconnect/reconnect order & fill recovery — IBKR doc evidence

**Question:** When a Client Portal Web API consumer disconnects (WebSocket closed and/or brokerage session lapses) while an order is working, and later reconnects/re-authenticates in a fresh session, does the Web API lose tracking of the order and any fills that occurred during the gap? What recovers them?
**Date:** 2026-07-09 · **Sources consulted:** DOC-01 (OpenAPI, `info.version` 2.35.0), DOC-03 (v1 narrative), DOC-05 (Trading new docs)
**Skipped:** DOC-02 (renders DOC-01), DOC-04 (orientation-only), DOC-06 (OAuth2 account-mgmt — different product), DOC-07 (Flex — downstream fallback only), DOC-08 (order types), DOC-09 (changelog — no recency angle), DOC-10 (entitlements).

> ⚠️ Doc sections record what IBKR's documentation CLAIMS as of the date above — not wire-verified. Presence/scoping claims are never per-message guarantees. Wire sections cite recordings/ paths and sample counts once probes run.

## Per-source findings

### DOC-01 — OpenAPI JSON (retrieved 2026-07-09, `curl`, `info.version` 2.35.0)
- `GET /iserver/account/orders` (`getOpenOrders`) `description`: **"Returns open orders and filled or cancelled orders submitted during the current brokerage session."** Nested `orders[]` schema repeats: "currently working, or were filled/cancelled in the current brokerage session." Params: `filters` (enum incl. `filled`, `sort_by_time`), `force` = "clear cache of orders and obtain updated view from brokerage backend. Response will be an empty array." Response `liveOrdersResponse` = `{ orders[], snapshot: bool }`.
- `GET /iserver/account/trades` (`getTradeHistory`) `description`: **"Retrieve a list of trades, up to a maximum of 7 days prior."** `days` (int, ≤7 in prose only — no JSON-Schema `maximum`; omitted ⇒ current day only). **No `accountId` parameter** (session-implicit).
- `GET /iserver/account/order/status/{orderId}` `description`: **"Retrieve the status of a single order. Only displays orders from the current brokerage session. If orders executed on a previous day or session, queries will 503 error."**
- `sor`/`str` WebSocket topics: **absent** (REST-only doc — proves nothing about them).

### DOC-03 — Web API v1.0 narrative (retrieved 2026-07-09, curl + browser UA)
- **`sor` topic** ("Request Live Order Updates"): "Once live orders are requested we will start to relay back when there is an update. To receive all orders for the current day the endpoint /iserver/account/orders can be used. **It is advised to query all orders for the current day first before subscribing to live orders.**" → forward-only; no snapshot-on-subscribe; REST is the documented pre-subscribe backfill. No "reconnect/replay/resume" wording anywhere on the page.
- **`str` topic** ("Request Trades data"): "Subscribes the user to trades data. This will return all executions data while streamed." Params: `realtimeUpdatesOnly` (bool, **default false** — "display any historical executions, or only … real time"); `days` (int, **default 1**). → **documented replay-on-subscribe of historical executions**, bounded by `days`.
- **Live Orders REST** ("Live Orders"): "orders: … Contains all orders placed on the account **for the day**." Max **1000** orders. `snapshot: bool` = "Returns if the data is a snapshot of the account's orders" (semantics otherwise undefined). ⚠️ Foible: "filtering orders using /iserver/account/orders will prevent order details from coming through over the websocket 'sor' topic … set 'force=true' in a follow-up call to clear cached behavior prior to the websocket request."
- **Trades REST** ("Trades"): "Returns a list of trades for the currently selected account for **current day and six previous days**. It is advised to call this endpoint once per session." `days` up to max 7; unspecified ⇒ current day.
- **Auth / session** ("Authentication FAQ"): session valid ≤24h; **times out after ~6 min** without requests/tickle. "If the brokerage session has timed out but … still connected …, /auth/status returns 'connected':true and 'authenticated':false. Calling /iserver/auth/ssodh/init … will initialize a **new brokerage session**." `/iserver/reauthenticate` marked Deprecated → use `ssodh/init`.
- **Order Status note** ("Order Status"): "If an order has been cancelled or filled **prior to the active session** and there is no cached information saved, querying the order status endpoint would be expected to result in a '503' error."
- Deferral flagged (low relevance): systemStatus.php for server-reset times.

### DOC-05 — Trading new docs (retrieved 2026-07-09, curl + browser UA)
- `/iserver/account/orders` ("Monitoring Live Orders"): "retrieve the status of all recently open orders … includes orders currently working as well as those cancelled or filled **within the same brokerage session**." Example shows `filters=filled&force=true` and `"snapshot": true`.
- `/iserver/account/trades` ("Monitoring Executions"): **"Documentation coming soon"** — placeholder, no detail.
- Sessions: two-tier (read-only + brokerage); "A single username can only have one brokerage session active at a time across all IB platforms"; indicators `connected`/`authenticated`/`established`/`competing`. Silent on working-order survival; no `ssodh/init`/`reauthenticate` mention.

## Wire observations (paper account, 2026-07-09)

Probes drive the library via the DI pipeline as a real consumer; raw wire captured alongside. Captures under `recordings/disconnect-order-tracking/` (gitignored — PII; account id → `REDACTED_ACCT`, tokens redacted). Per-sample truth, not "always".

### Scenario A — resting-order survival across a session boundary
Boundary is genuine: provider dispose issues a real `LogoutAsync` (`SkipLogoutOnDispose` default `false`) ending brokerage session A; a fresh provider's first call forces a new `ssodh/init` = brokerage session B. SPY (conid 756733), GTC far-from-market, RTH, ~40s window.

- **`/iserver/account/orders` is DAY-scoped, not brokerage-session-scoped** — session B returned session A's working orders. **Resolves the conflict in favour of DOC-03 ("for the day"); DOC-01/DOC-05 "current brokerage session" wording is misleading.** Observed 2/2 clean cycles.
- Resting **LIMIT** survived the boundary, reappearing `Submitted` in B — **3/3**.
- Resting **STOP** survived, reappearing `PreSubmitted` in B — **2/2**. (A 3rd "validation" cycle's STOP was rejected only due to the probe erroneously setting `outsideRTH=true`, which IBKR rejects for STP during RTH — not a survival failure.)
- **`order/status/{orderId}` for a WORKING prior-session order → HTTP 200 with full status (NOT 503)** — 4/4. The documented 503 (DOC-01/DOC-03) is asserted only for *cancelled/filled* prior orders; the working case returns 200.
- **Cold-snapshot priming:** session B's *first* `/iserver/account/orders` read returned `{"orders":[],"snapshot":false}` (cold cache), then `snapshot:true` with the orders after priming. A fresh session's first read can be **empty-but-`snapshot:false`** — a consumer must poll to `snapshot:true` before trusting an empty set.
- Cleanup confirmed: all probe orders `Cancelled`; nothing left working.

### Scenario B — fill recovery across a session boundary (n=1; ack capped mutations at BUY+flatten SELL)
Session A places a marketable BUY 1 SPY (MKT) that fills at 751.68 (execId `…6a56b338`), disposes (logout); fresh session B recovers. Flattened at end (net-zero probe delta; a pre-existing 48-share paper position was untouched). RTH 2026-07-09.

- **(a) Session-A fill recoverable in B via `/iserver/account/trades` — YES (1/1)**, with a **cold-read priming lag**: the *first* trades GET on a fresh session returned `[]` (seen in both A and B); a *subsequent* read returned the populated list including the session-A fill. Recovery holds, but a single cold read can false-empty.
- **(b) B's `/iserver/account/orders?filters=Filled` shows the filled order — YES (1/1)**, day-scoped, after priming to `snapshot:true` (call 1 `snapshot:false`, call 2 populated).
- **(c) `order/status/{filledOrderId}` in B → HTTP 200 `Filled` (NOT 503) (1/1).** The documented 503-for-a-filled-prior-order was **not reproduced** for a recent same-day fill. Caveat: single sample, same trading day — the doc's 503 may pertain to *aged* filled orders; this can't disprove that case.
- **(d) Fresh `str` subscribe REPLAYS the historical execution — YES (1/1)** (`str+{"days":1}`, `realtimeUpdatesOnly` default false; replayed execId `…6a56b338`). **`sor` did NOT replay it** — `sor+{"days":1}` emitted 7 **id-only** frames (`acct/conidex/conid/orderId/isEventTrading`, no status/fill fields); the filled order's id appeared but not as an execution replay. Confirms `sor` forward-only/id-only vs `str` historical-replay.
- **Bonus:** `str days:1` replayed a QQQ execution from `20260706` on a `20260709` run → **`days` counts *trading* days (or is broader than 24h)**, not a rolling 24h window.

Captures: `recordings/fill-recovery-session-boundary/c1-172806-*/` (trades cold+primed, orders `filters=Filled`, filled-order status 200, `str` replay frame, `sor` id-only frames).

## Reconciliation

- **Agreed:**
  - **`sor` (live orders WS) is forward-only** — no snapshot/replay on subscribe; the documented recovery is "REST `/iserver/account/orders` first, then subscribe." (DOC-03 explicit; DOC-05 doesn't cover it; DOC-01 REST-only.) → **This is exactly IbkrConduit's `OrderMonitor` pattern (REST-seed then merge sparse deltas).**
  - **`str` (trades WS) replays historical executions** on subscribe by default (`realtimeUpdatesOnly=false`, `days` default 1). (DOC-03.)
  - **`/iserver/account/trades` is day-windowed to 7 days** (current + 6 prior), session-independent (no `accountId`). (DOC-01 + DOC-03 agree; DOC-05 placeholder.)
  - **`/iserver/account/order/status/{orderId}` 503s** for an order from a previous day/session with no cached info. (DOC-01 + DOC-03 agree.)
  - **`ssodh/init` creates a *new* brokerage session**; `/reauthenticate` deprecated. (DOC-03.)

- **Conflicts:**
  - 🔴 **Scope of `/iserver/account/orders`: "current brokerage session" (DOC-01, DOC-05) vs. "for the day" (DOC-03).** ✅ **RESOLVED BY WIRE 2026-07-09 → DAY-scoped** (Scenario A, 2/2): session B (fresh `ssodh/init` after A's logout) returned A's working orders. DOC-03 is correct; DOC-01/DOC-05 wording is misleading. The "loses tracking of working orders" failure does **not** occur for same-day reconnects.

- **Gaps (no registered source covers):**
  - Whether a brokerage-session lapse **cancels working orders** server-side. No CP Web API source states it. General IB/TWS knowledge says orders persist server-side independent of the API session, but that is **inference**, not a CP-doc claim → wire it.
  - Meaning of the Live Orders `snapshot` boolean beyond its one-line label.
  - Whether a socket-drop-then-reconnect `str` resubscribe re-sends the historical `days` window identically to a first subscribe (implied, not stated).

- **Presence/scoping claims (taxonomy):**
  - `/iserver/account/orders` session-vs-day scope — ✅ **observed: DAY-scoped** (Scenario A, 2/2). Was documented-conflicting; wire resolved to DOC-03.
  - `order/status/{id}` for a **working** prior-session order — ✅ **observed: 200, not 503** (Scenario A, 4/4). Documented 503 is scoped to cancelled/filled only.
  - Cold-snapshot priming (`snapshot:false` empty first read) — ✅ **observed, undocumented** (Scenario A). Consumer must poll to `snapshot:true`.
  - `str` historical replay on subscribe — ✅ **observed** (Scenario B, 1/1): replays the prior-session execution; `days` counts trading days (QQQ from 3 calendar days prior on `days:1`).
  - `sor` forward-only / id-only (no execution replay) — ✅ **observed** (Scenario B, 1/1): id-only frames, no fill replay. Matches the library's REST-seed-first `OrderMonitor` design.
  - fill in `/iserver/account/trades` across the boundary — ✅ **observed recoverable** (Scenario B, 1/1) **with a cold-read priming lag** (first read `[]`, second populated) — and `GetTradesAsync` exposes **no** snapshot signal (unlike orders' `IsSnapshot`).
  - `order/status/{id}` 503 for a **filled** prior-session order — ⚠️ **NOT reproduced** (Scenario B, 1/1 → HTTP 200 `Filled`). Documented 503 unconfirmed for a recent same-day fill; may apply only to aged orders. Single sample.
  - `/iserver/account/trades` 7-day window — **documented, unobserved** (not directly probed; single-day scope only).

## Final answer (doc + wire, 2026-07-09)

**No — the Client Portal Web API does NOT lose tracking of orders or fills across a disconnect+reconnect on the same trading day.** Verified against the paper account:

- **Working orders (resting LIMIT + STOP) survive** a full disconnect (logout on dispose) → fresh `ssodh/init` session, same day: `/iserver/account/orders` is **day-scoped** (not brokerage-session-scoped — DOC-03 correct, DOC-01/DOC-05 wording misleading) and returns them (LIMIT 3/3, STOP 2/2).
- **Fills survive**: recoverable in the fresh session via `/iserver/account/trades` (day-windowed) *and* re-delivered by a fresh `str` subscribe (historical replay, bounded by `days`, trading-day units). `order/status/{id}` returns a live 200 status for both working and recent-filled prior-session orders (documented 503 not reproduced same-day).
- **What IS genuinely "lost"**: only the **real-time `sor`/`str` frames that streamed during the gap** — `sor` is forward-only/id-only and does **not** replay. A *notification-delivery* gap, closed by the documented recovery recipe (REST-seed `/iserver/account/orders` to `snapshot:true`, then subscribe; `str` re-subscribe backfills executions) — which is exactly IbkrConduit's existing `OrderMonitor` design.
- **The only real loss risks: (a) time-based** — beyond the 7-day `trades` window, only Flex/statements recover a fill — **and (b) cold-read false-empty** on a fresh session's *first* `/iserver/account/orders` (`snapshot:false`) or `/iserver/account/trades` (`[]`) read.

### Contract-gap flags for a design pass (route via `.claude/rules/contract-design.md` — not closed here)
1. 🟢 **Orders cold-snapshot: already handled.** `GetLiveOrdersAsync` → `LiveOrdersSnapshot.IsSnapshot`; XML doc mandates treating `IsSnapshot == false` as "call again, never no-orders" (VCR-05/GAP1-1). No action beyond confirming the `OrderMonitor` example models it.
2. 🔴 **Trades cold-read has no priming signal.** `GetTradesAsync` returns a bare `Result<List<Trade>>`; a fresh-session first read can false-empty (`[]`) with no way to distinguish "cache cold" from "no fills" — asymmetric with orders' `IsSnapshot`. Delivery-completeness / fill-delivery (money boundary). **Candidate story.**
3. 🟡 **`str` `days` unit + replay-on-subscribe undocumented.** `days` counts trading days and `str` replays historical executions by default (`realtimeUpdatesOnly=false`) — `TradeExecutionsAsync` XML doc doesn't pin either. Doc/design clarification. **Candidate story.**
4. 🟡 **Doc-vs-wire corrections for the design doc:** `/iserver/account/orders` is day-scoped; `order/status/{id}` returns 200 (not 503) for working and recent-filled prior-session orders (the design doc's order-status 503 note needs an aged-order qualification or retraction). Design-doc/ADR correction, not a code change.
5. 🟢 **Positive guarantee to record:** the reconnect-recovery contract itself (server-side day-scoped persistence; `sor` forward-only ⇒ REST-seed-first; `str` replay; 7-day window ⇒ Flex fallback) is worth stating explicitly in the design doc's delivery + session-lifecycle guarantees.
