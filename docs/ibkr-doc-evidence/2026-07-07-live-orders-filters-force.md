# Live-orders `filters`/`force` semantics — IBKR doc evidence

**Question:** What do IBKR's live docs claim about `GET /iserver/account/orders`'s `filters` and `force` parameters, the `snapshot` field, and the filtered-call → `sor`-suppression → `force=true` follow-up interaction? (Re-grooms design doc §10.6's claim-side citation — previously the deprecated mirror :4150 — and PVR-18's sufficiency claim.)
**Date:** 2026-07-07 · **Sources consulted:** DOC-01, DOC-03, DOC-05

> ⚠️ Doc sections record what IBKR's documentation CLAIMS as of the date above — not wire-verified.
> Presence claims are never per-message guarantees: WS is confirmed sparse; REST sparseness unconfirmed.
> Wire sections cite recordings/ paths and sample counts.

## Per-source findings

### DOC-03 (retrieved 2026-07-07, `<section id="live-orders">` under Endpoints → Orders; also `#order-status-value` and Websockets `#ws-orders-positions`)

**The sor-suppression warning — the live counterpart of the mirror:4150 note, exact:**

> "This endpoint requires a pre-flight request. … Please be aware that filtering orders using the /iserver/account/orders endpoint will prevent order details from coming through over the websocket "sor" topic. To resolve this issue, developers should set "force=true" in a **follow-up** /iserver/account/orders call to clear any cached behavior surrounding the endpoint prior to calling for the websocket request."

The prose prescribes a **second, separate call** — yet the worked example directly beneath it combines both in one request (`GET /iserver/account/orders?filters=filled&force=true`, Python and cURL tabs) with no reconciling sentence. **The source never states whether the combined single call is sufficient** — an internal ambiguity, reported as-is. Parameters: `filters` — "Optionally filter your list of orders by a unique status value. More than one filter can be passed, separated by commas." (values via the `#order-status-value` table: `inactive, pending_submit, pre_submitted, submitted, pending_cancel, pre_cancelled, cancelled, filled, warn_state` + sort directive `sort_by_time`); `force` — "Force the system to clear saved information and make a fresh request for orders. Submission will appear as a blank array."; `snapshot` — "Returns if the data is a snapshot of the account's orders." (no elaboration). **No unprimed-first-call claim anywhere.** The Websockets `sor` section advises "query all orders for the current day first before subscribing" but never repeats the suppression warning or mentions `force`.

### DOC-01 (retrieved 2026-07-07, `paths./iserver/account/orders.get`; live `info.version` 2.35.0, matches registry)

`filters`: "Filter results using a comma-separated list of Order Status values. Also accepts a value to sort results by time." — enum `["inactive", "pending_submit", "pre_submitted", "submitted", "filled", "pending_cancel", "cancelled", "warn_state", "sort_by_time"]` (note: **no `pre_cancelled`**, unlike DOC-03's table). `force`: "Instructs IB to clear cache of orders and obtain updated view from brokerage backend. **Response will be an empty array.**" `snapshot`: "Whether the response is a snapshot." — nothing more. **No filters↔force interaction, no `sor` claim (REST-only document), no unprimed-first-call claim.** The two parameters are documented in isolation.

### DOC-05 (retrieved 2026-07-07, h3 "Monitoring Live Orders" `#monitoring-live-orders-38`)

Example-only: one paragraph ("retrieve the status of all recently open orders… includes orders currently working as well as those cancelled or filled within the same brokerage session"), one example request — notably `?filters=filled&force=true&accountId=U1234567`, both parameters together, unexplained — and one example response ending `"snapshot": true`. **No prose on `filters`, `force`, `snapshot`, or any interaction; no `sor` mention on the whole page.** ("Monitoring Executions" h3 is a "Documentation coming soon" stub.)

## Wire observations

(2026-07-07 grooming probes, `recordings/priming/001-003`, local per repo convention; sanitized fixtures carry the shapes — from the VCR-05/PVR-18 grooming evidence:)

- A **filtered** call returned fake-empty `{"orders":[], "snapshot":false}` while cancelled orders demonstrably existed (1 sample) — an unprimed/suppressed state no source documents.
- `force=true` returned the documented blank array (1 sample) — matches DOC-01/DOC-03's claims.
- The next unforced call returned `snapshot:true` with data (1 sample).
- The `sor`-suppression effect itself is **documented-only** — not independently observable on demand (requires a live order flow racing a WS subscription); no recording pins it.

## Reconciliation

- **Agreed (DOC-01 + DOC-03):** `force=true` clears cached/saved order state and that call's own response is an empty/blank array. `filters` is a comma-separated order-status list.
- **Conflicts:** filter value set — DOC-03's table includes `pre_cancelled`; DOC-01's enum omits it. Unresolved (harmless — the library passes filters through as strings). Casing: the wire's `order_status` values are CamelCase while filter values are snake_case (DOC-03 documents the mapping explicitly).
- **Internal ambiguity (DOC-03):** prose prescribes a **follow-up** `force=true` call; its own example (and DOC-05's only example) shows filters+force **combined in one call**, unexplained. No source states the combined form is sufficient.
- **Gaps:** no source documents the unprimed-first-call fake-empty behavior the wire exhibits (`snapshot:false` + empty while orders exist); no source documents `snapshot` beyond one sentence; DOC-05 documents essentially nothing.
- **Presence claims:**
  - sor-suppression-by-filtered-call: **documented (DOC-03 only), not wire-observed** (not observable on demand) — the design treats it as documented-not-verified and acts defensively.
  - force-returns-empty-array: **documented (DOC-01 + DOC-03) + observed (1 sample)**.
  - unprimed-first-call fake-empty: **observed (1 sample), undocumented** — wire wins; the doc gap is recorded here.

**Answer for the consuming decisions (design doc §10.6, PVR-18):** the groomed decisions stand and are strengthened. §10.6's claim-side citation migrates from the mirror (:4150) to DOC-03's live warning above. PVR-18's "drop the filters+`force` exemption — always issue the follow-up" is exactly what the live prose prescribes (follow-up call), and remains safe under the combined-call-example reading too. The library-owns-quirks posture (auto follow-up, `IsSnapshot` surfaced) also covers the wire-observed-but-undocumented unprimed state.
