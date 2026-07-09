# Does the Web API lose tracking of orders/fills across a disconnect + reconnect?

**Status:** ✅ Complete — Phase 1 (web research), Phase 2 (theories), Phase 3 (doc scout + live paper probes) all done. **Verified answer + wire evidence + contract-gap flags: [`docs/ibkr-doc-evidence/2026-07-09-disconnect-order-fill-recovery.md`](../ibkr-doc-evidence/2026-07-09-disconnect-order-fill-recovery.md).**

> **TL;DR (verified 2026-07-09 against the paper account):** **No — the Web API does not lose tracking of orders or fills across a same-day disconnect+reconnect.** `/iserver/account/orders` is **day-scoped** (working LIMIT+STOP survive a logout→fresh-`ssodh/init` boundary); fills are recoverable via `/iserver/account/trades` and replayed by a fresh `str` subscribe; `order/status/{id}` returns 200 (not the documented 503) for recent prior-session orders. The only genuine losses are the un-replayed real-time `sor` frames during the gap (closed by REST-seeding, exactly the library's `OrderMonitor` design), the 7-day `trades` horizon (Flex beyond it), and **cold-read false-empty** on a fresh session's first `orders`/`trades` read — the last of which is a real library gap for `trades` (no priming signal, unlike orders' `IsSnapshot`).
**Date opened:** 2026-07-09
**Question (operator):** IBKR's desktop client (TWS) reportedly must stay online to catch trade confirmations, fills, and order-status updates — place an order, shut TWS down, come back later in a fresh session, and you supposedly "miss things." Is that real desktop behavior? If so, what does it imply for the **Client Portal Web API** (what IbkrConduit wraps)? **Bottom line we want:** does the Web API lose tracking of orders/fills if you disconnect and reconnect later?

> Scope note: this is a **contract-design research pass** against the library's highest-risk guarantee surface (delivery & backpressure — streaming completeness / fill delivery — and session lifecycle). No library code changes. Output = a verified answer + any backlog items for genuine contract gaps.

---

## Phase 1 — Desktop TWS / TWS-API behavior (general web research)

Sources are the TWS API reference (github.io), IBKR Campus, IBKR guides, and practitioner forums (see Sources). Findings, most-load-bearing first:

### F1. Transmitted orders live **server-side**, not in the client
Once an order is *transmitted* to the IB server it persists and continues to work/execute regardless of whether the client (TWS / IB Gateway / API app) stays connected. Only **untransmitted** orders (`Transmit=false`, held locally in a TWS session) are session-local and cleared on restart. During IB-side resets, "existing orders … operate normally although execution reports … will be delayed until the reset is complete" — i.e. order/exec state is authoritative on the server.

**Implication:** the *data* (working orders, fills, executions) is **not lost** when a client disconnects. It is retained server-side and re-queryable.

### F2. Real-time push requires the client to be **connected at the moment of the event**
`execDetails` / `commissionReport` (fills) and `openOrder` / `orderStatus` (status transitions) are **push** callbacks delivered only while the API client is connected. If TWS/Gateway is closed when a fill happens, no live callback is delivered for it. Worse, IBKR explicitly warns: **"There are not guaranteed to be orderStatus callbacks for every change in order status"** — so even while connected, the live stream is best-effort, not a guaranteed transition log. This is why IBKR recommends monitoring `execDetails` *in addition to* `orderStatus`.

**Implication:** the *live notification* during a disconnect window **is lost** (no automatic replay). This is a delivery gap, not a state loss.

### F3. Recovery on (re)connect is **pull, not replay**
The API does **not** auto-replay missed events on reconnect. The client must proactively re-query current state:
- `reqOpenOrders` / `reqAllOpenOrders` / `reqAutoOpenOrders` → currently active orders (a snapshot).
- `reqExecutions` → executions; **by default only executions since midnight** for the account. Up to **7 days** is retrievable only if the TWS **Trade Log "Show trades for…"** setting is widened.
- `reqCompletedOrders` → orders completed during the current day (including ones completed before this client connected).

**Implication:** correct clients treat reconnect as "re-query the snapshot," never "wait for the missed events to arrive."

### F4. The live-API execution window is **short** (~1–2 calendar days in practice)
Even though `reqExecutions` (and the Web API `days` param) advertise up to 7 days, practitioners report the API execution list realistically only surfaces the **most recent ~1–2 calendar days**; older trade detail is available only via account/portal downloads or the **Flex Web Service**.

**Implication:** "come back *later*" has a time dimension. Reconnect within the window → fill recoverable via the live API. Reconnect *days* later → the fill may have aged out of the live API and be recoverable only via Flex/statements.

### Phase-1 answer (desktop/TWS)
The operator's premise is **half true and worth restating precisely**:
- **True:** you miss the *real-time notifications* that fire while the client is offline — TWS shows no live fill pop, the API delivers no `execDetails`/`orderStatus` for the gap, and there is no replay.
- **False (the important correction):** you do **not** lose the *orders or fills themselves*. They executed server-side and are visible again on reconnect via the Trade Log / account and via `reqOpenOrders` + `reqExecutions` + `reqCompletedOrders` — **within the retention window (F4).**

So "must stay online or you miss things" really means "must stay online to receive *live* things; otherwise re-query on reconnect, and don't wait too long."

---

## Phase 2 — Theories about the Client Portal Web API (to validate in Phase 3)

The Web API is a **different transport** from the TWS socket API (REST + a `wss` WebSocket over an authenticated brokerage session, not a persistent socket to a local Gateway process). But the underlying **server-side order/execution model is the same IB backend**, so the desktop findings map over as hypotheses, not facts. Each theory below names how to pin it.

| # | Theory | Maps from | How to pin (Phase 3) |
|---|---|---|---|
| **T1** | **Server-side persistence.** An order placed via the Web API then fully abandoned (WS closed, brokerage session left to expire) still works/executes; on a **fresh** session `/iserver/account/orders` returns it and `/iserver/account/trades` returns its fill. | F1 | **Live probe** (definitive). Docs likely silent. |
| **T2** | **WebSocket is live-push, no replay.** The `sor` (live orders) and trade/`str` WS topics deliver only updates from subscription time forward. Order-status transitions / fills that occur while unsubscribed or disconnected are **not** replayed on (re)subscribe. | F2 | **Scout** WS docs for any "snapshot vs replay" wording; **live probe** to confirm no backfill frames. |
| **T3** | **Snapshot-on-subscribe / on-poll.** Subscribing to `sor` (or polling `/iserver/account/orders`) yields the **current** state of open+recent orders as a snapshot — the recovery mechanism. IbkrConduit already leans on this (OrderMonitor REST-seeds from `GetLiveOrdersAsync` then merges sparse deltas). | F3 | **Scout** `/iserver/account/orders` + `sor` docs; confirm library's existing assumption. |
| **T4** | **Bounded trades window.** `/iserver/account/trades` returns executions for a bounded window (`days` param 1..7, practical floor ~1–2 days). Beyond it → Flex only. | F3/F4 | **Scout** the trades endpoint (`days` param, max, retention wording). |
| **T5** | **Session lifecycle ⟂ order lifecycle.** Losing/expiring the brokerage session does **not** cancel or orphan working orders; re-auth via `/iserver/auth/ssodh/init` restores access to the same server-side order/exec state. (`/reauthenticate` is deprecated in favour of ssodh/init.) | F1 | **Scout** session docs; **live probe** — let session lapse, re-init, confirm order still there. |

### The crisp answer we expect to confirm
> **No — the Web API does not lose *tracking* of orders/fills across a disconnect+reconnect.** Order and fill state is durable server-side and recoverable on a fresh session by *re-querying* `/iserver/account/orders` and `/iserver/account/trades` (T1, T3). What is genuinely lost is the set of **real-time WebSocket frames** that streamed during the gap — the WS is a live push channel with **no replay** (T2) — but that is a *notification-delivery* gap the consumer closes by snapshotting REST on reconnect, exactly as IbkrConduit's OrderMonitor already does. The one real *tracking-loss* risk is **time-based**: reconnect after the trades retention window (T4, ~1–2 days) and the fill is no longer in the live API — Flex is the fallback.

This has direct consequences for the library's **delivery & backpressure** and **session-lifecycle** guarantees — candidate contract gaps to check in Phase 3:
- Does IbkrConduit **document** the "WS has no replay; re-snapshot REST on reconnect" consumer obligation? (Design §7 / §9 delivery contract.)
- Does the WS reconnect path (epoch guard, single-send replay — PVR-16) **re-emit a REST snapshot** after reconnect, or only resume the live stream? If the latter, a fill during the reconnect gap is silently missed by a `sor`-only consumer — a real delivery-completeness gap.
- Is the **trades retention window** (T4) surfaced anywhere, so a consumer knows "reconnect within N days or use Flex"?

---

## Sources (Phase 1)

- [TWS API — Placing Orders](https://interactivebrokers.github.io/tws-api/order_submission.html)
- [TWS API — Executions and Commissions](https://interactivebrokers.github.io/tws-api/executions_commissions.html)
- [TWS API — Retrieving currently active orders](https://interactivebrokers.github.io/tws-api/open_orders.html)
- [TWS API — Considerations for Automated Systems](https://interactivebrokers.github.io/tws-api/automated_considerations.html)
- [TWS API — EWrapper Interface Reference](https://interactivebrokers.github.io/tws-api/interfaceIBApi_1_1EWrapper.html)
- [Classic TWS Pending (All) Tab](https://www.ibkrguides.com/traderworkstation/classic-pending-orders-tab.htm)
- [IBKR Campus — Websockets (Client Portal)](https://www.interactivebrokers.com/campus/trading-lessons/websockets/)
- [Client Portal API Documentation](https://interactivebrokers.github.io/cpwebapi/)
- [Web API v1.0 Documentation | IBKR Campus](https://www.interactivebrokers.com/campus/ibkr-api-page/cpapi-v1/)
- [ib_async discussion #68 — personal transaction data / API execution window](https://github.com/ib-api-reloaded/ib_async/discussions/68)

> ⚠️ Phase 1 sources are the *claim* tier (docs + practitioner reports). Phase 3 scouts the live Web API docs and **verifies** the Web-API-specific theories against `recordings/` + attended paper-account probes before any of this becomes a contract change. Documented ≠ verified.
