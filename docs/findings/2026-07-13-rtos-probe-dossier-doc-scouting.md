# RTOS live-paper probe dossier + IBKR doc scouting (2026-07-13)

**Origin:** `RTOS-PROBE-DOSSIER-2026-07-13.md` (repo root, operator-dropped), itself sourced from RTOS's IBV-P live-paper probe suite run 2026-07-13 ~16:43–16:55 UTC against paper account `DUO873728` through **IbkrConduit 0.9.0** (nuget.org). Every schema warning quoted below was emitted by the conduit's own `ResponseSchemaValidationHandler` against live CPAPI responses.

**What this document adds:** for each RTOS item — including the two explicitly flagged as *not* conduit issues — a `scout-ibkr-docs` pass against the live IBKR documentation registry, answering "what do the docs say about this?" The RTOS dossier is preserved verbatim per item; doc scouting is new. Per `.claude/rules/contract-design.md`, everything below is the **claim tier** — doc content is IBKR's claim, not wire verification. Wire evidence remains what RTOS's probe captured (cited inline) plus this repo's own recordings where noted.

**Sources consulted:** DOC-01 (OpenAPI JSON), DOC-03 (Web API v1.0 narrative), DOC-05 (Trading Web API, "new docs"), DOC-08 (Order Types reference) — all retrieved 2026-07-13, no staleness/drift found against their registry `Last verified` dates. **Sources skipped** (topical judgement call, `.claude/rules` / registry heuristic): DOC-02 (pure Redoc shell of DOC-01, no independent content), DOC-04 (orientation-only landing page, no endpoint detail), DOC-06 (different product — OAuth 2.0 account-management API, out of scope), DOC-09 (changelog — no recency/drift question in play here), DOC-10 (market-data entitlements/billing, off-topic for these items).

**This document is not itself a backlog decomposition.** It is evidence for a future `draft-backlog`/`groom-backlog` pass — several items below are 📦 public-surface/contract questions per `.claude/rules/contract-design.md` and will need a design-doc/ADR pass before grooming, same as Stream VCR and Stream FO did.

---

## Headline cross-cutting observation: the "cold cache" pattern recurs across ≥4 endpoints

Before the per-item breakdown: doc scouting surfaced that RTOS's two "not conduit issues" and P3's sparse-first-read finding are very likely **the same underlying IBKR platform behavior**, not three unrelated quirks:

| Endpoint | Symptom | Doc status |
|---|---|---|
| `GET /iserver/account/watchlists/{id}` | first request returns only `C`/`conid`/`name`; subsequent requests add full contract info | **Documented** — DOC-03: *"The first request may only return the values C, conid, and name values. Subsequent requests will add additional contract information."* |
| `GET /iserver/account/orders` (live orders) | filtered call fake-empties (`snapshot:false`) while orders exist; `force=true` needed | **Wire-confirmed** in the 2026-07-07 VCR grooming pass (`recordings/priming/001-003`); not documented in any of DOC-01/03/05 |
| `GET /portfolio/{acct}/positions/{page}` | first read missing `name`/`ticker`; second read enriched (this dossier, P3) | **Undocumented** — DOC-01/03/05 all document `name`/`ticker` unconditionally, no first-read caveat anywhere |
| `GET /iserver/account/trades` | first call of session returns empty despite trades existing (this dossier, NC2) | **Cryptically hinted, not explained** — DOC-03's only prose is *"It is advised to call this endpoint once per session"*, no mechanism stated; DOC-01/05 silent |

One endpoint in this family (Watchlist) is explicitly documented; one (LiveOrders) is wire-confirmed by this repo's own probe; two more (Positions, Trades) are now added by RTOS's probe. No source ties these together as one platform behavior — each doc page (where it says anything at all) treats its own endpoint in isolation. **Recommendation for grooming:** treat this as one cross-cutting session-lifecycle guarantee (a "first-touch-after-session-start may return a thin/stale snapshot" consumer obligation), not four independent field-nullability fixes — likely an ADR-level note in `docs/ibkr_conduit_design.md`, extending the reasoning already recorded for `GetLiveOrdersAsync`'s `{Orders, IsSnapshot}` design (VCR D4, design doc §10.6).

---

## P1 — `LiveOrder` rows: `parentId`, `ocaGroupId`, `order_cancellation_by_system_reason` ⭐ load-bearing

### RTOS dossier (verbatim)

> the venue's bracket child-id census resolves child→parent linkage from these fields — today they surface only via `[JsonExtensionData]` (`AdditionalData`), which the probe had to spelunk (`parent-linkage keys=[parentId]`). `order_cancellation_by_system_reason` is classification-grade data for cancel handling.
>
> ```
> Response schema mismatch for /v1/api/iserver/account/orders -> OrdersResponse: Extra fields: [acct, cashCcy, sizeAndFills, description1, outsideRTH, origOrderType, supportsTaxOpt, lastExecutionTime, bgColor, fgColor, isEventTrading, lastExecutionTime_r, parentId, ocaGroupId, order_cancellation_by_system_reason]. Missing fields: [].
> ```
>
> Notes: `parentId`/`ocaGroupId`/`order_cancellation_by_system_reason` appear only on responses that actually contain bracket/cancelled rows (three of the six captures). `outsideRTH` was absent from one early capture — model all of these as nullable/optional presence.

### Doc scouting

**`order_cancellation_by_system_reason`**
- **DOC-01** — present and documented, on the live-orders order-row schema (`liveOrdersResponse.properties.orders.items`): *`"order_cancellation_by_system_reason": {"type": "string", "description": "Only present for Cancelled orders. Provides the reason for order to have been cancelled or rejected by the system.\n"}`*
- **DOC-03** — absent from the page entirely (zero occurrences, site-wide search).
- **DOC-05** — absent; the "Monitoring Live Orders" example order (a single non-bracket fill) doesn't include it and no prose mentions it.

**`parentId`**
- **DOC-01** — absent from the `liveOrdersResponse` schema (the response). Present only on the **request** schema for order submission: `singleOrderSubmissionRequest.parentId`: *"If the order ticket is a child order in a bracket, the parentId field must be set equal to the cOID provided for the parent order."*
- **DOC-03** — same pattern: appears only in Place Order / Bracket Orders **request** documentation ("Bracket orders can be submitted using the cOID field for the parent order, and then use this same value in each of the child orders in the parentId field"), never in the Live Orders response field list. DOC-03 does not explain how to read parent/child linkage back out of a live-orders response — the only response-side identity field it documents is `order_ref` (sourced from `cOID`).
- **DOC-05** — the "Monitoring Live Orders" section's full example response and field list do not include `parentId`, and the section has no prose on bracket linkage at all.

**`ocaGroupId`**
- **DOC-01, DOC-03, DOC-05** — **zero occurrences in any of the three sources**, in any context (request or response). No `oca*`-prefixed field name of any kind appears anywhere in DOC-01's 864KB document.
- **DOC-08** (order-type reference) — confirms why: the OCA concept exists only on the **TWS API** side of this page, as `IBApi.Order.ocaGroup` / `ocaType` attributes (`o.ocaGroup = ocaGroup`) — a different name (`ocaGroup`, no `Id` suffix) in a different API (TWS, not CP Web API). The Bracket Orders, One-Cancels-All, and OCA Types sections on DOC-08 have **no CP Web API (cURL) tab at all** — confirmed structurally (no `tab-curl` in any of the three sections, versus 116 cURL-tab occurrences elsewhere on the same page). DOC-08's own "Order Type by API" section states plainly: *"Orders labeled in CURL are for use in the Client Portal API"* — and none of the OCA/bracket sections carry that label.

### Reconciliation

- **`order_cancellation_by_system_reason`** → **documented + observed**. This one is not a documentation gap at all — DOC-01's OpenAPI JSON already models it correctly, including the "only present for Cancelled orders" presence caveat RTOS independently inferred from the wire. The fix here is purely internal: promote it from `AdditionalData` to a typed nullable field, using DOC-01's own description text.
- **`parentId`** → **observed, undocumented** (on the response side). Every source that mentions `parentId` at all treats it strictly as a bracket-child *request* field; none of DOC-01/03/05 documents it as appearing in the live-orders *response*. RTOS's wire observation is the only evidence it round-trips back onto order rows. Model as nullable/optional-presence per RTOS, but flag in the story spec that this is an **undocumented response echo**, not a documented contract — worth a live re-probe sample count if the field is going to anchor bracket-census logic (`Risk: high` territory per `.claude/rules/backlog-format.md`).
- **`ocaGroupId`** → **observed, undocumented**, and unusually isolated: no source names anything resembling it for CP Web API, and the nearest TWS-API analog (`ocaGroup`) doesn't even share the field name. This is the least-anchored of the three fields — recommend the story spec note explicitly that IBKR's own docs offer no contract for this field's shape, stability, or semantics; it's wire-only evidence.

---

## P2 — `OrderSubmissionResponse`: `order_id=-1, status=Failed` parses as success-shaped ⭐ behavioral

### RTOS dossier (verbatim)

> submitting a bracket group whose child is deliberately invalid returned a row the conduit surfaced as a normal `OrderSubmitted` with `order_id=-1`, `status=Failed` — the RTOS probe recorded it as `POST outcome: ACCEPTED?! order_id=-1 status=Failed`. A `-1`/`Failed` submission row is a rejection and should surface through the typed error path... never as a success-shaped result the consumer must sniff for sentinel values.
>
> ```
> Response schema mismatch for /v1/api/iserver/account/DUO873728/orders -> OrderSubmissionResponse: Extra fields: [warning_message, text]. Missing fields: [].
> Response schema mismatch for /v1/api/iserver/account/DUO873728/orders -> OrderSubmissionResponse: Extra fields: [messageOptions]. Missing fields: [].
> ```
>
> `messageOptions` appeared on every confirmation-prompt submission response (three captures); `warning_message`/`text` on the failed-child response.

### Doc scouting

**The `order_id=-1`/`status=Failed` shape itself**
- **DOC-01** — the submission endpoint's 200 response is a documented `oneOf` across four distinct shapes: `orderSubmitSuccess`, `orderReplyMessage`, `orderSubmitError` (`{"error": "<string>"}`), `advancedOrderReject` (richer object with `orderId`, `text`, `options`, etc. — example shows a real positive `orderId`, e.g. `123456789`). **No source-documented shape uses `order_id: -1` combined with `status: "Failed"`.** A full-document search for the literal `"-1"` in any order-submission context found no matches; `orderSubmitSuccess.order_status` is an undconstrained free string with no `"Failed"` enum value.
- **DOC-03** — documents the same three response shapes (Standard, "Alternate Response Object" for reply/questions, "Order Reject Object" — `{"error": "<message>"}`, still under a 200). No `-1`/`Failed` convention documented anywhere in Place Order, Reply Confirmation, or Order Status Value sections.
- **DOC-05** — the "Order Rejections" section is a stub: *"Documentation coming soon."* "New Order Example" documents only the success shape (`{"order_id": "987654", "order_status": "Submitted", "encrypt_message": "1"}`); "Order Reply Messages" documents the question/reply shape, explicitly noting *"The receipt of such an 'order reply message' does not indicate that the order is rejected."*

**`warning_message`**
- **DOC-01** — the string exists in the document, but only on a completely different endpoint's schema: `alertCreationResponse` (FYI/price-alert creation), documented as *"Returns 'null'"* with an always-null example. It does not appear on any of the four order-submission response schemas.
- **DOC-03** — same: only found under the unrelated "Create or Modify Alert" section (*"warning_message: String. Returns 'null'"*), never in Place Order.
- **DOC-05** — absent from the page entirely.

**`text`**
- **DOC-01** — **documented**, on `advancedOrderReject.text`: *"Human-readable text of the messages emitted by IB in response to order submission."* This is one of the four order-submission response shapes (see above) — genuinely in-context, not a look-alike from elsewhere.
- **DOC-03** — not documented in the Place Order response context (a `text` field does appear, but on the *request* side of an unrelated "Respond to a Server Prompt" flow).
- **DOC-05** — absent; the only `"text"` occurrences on the page are UI chrome, not a JSON field.

**`messageOptions`**
- **DOC-01** — absent. Closest analogs are differently-named: `advancedOrderReject.options` ("Choices available to the client in response to the rejection message," e.g. `["Use on this order", "Always use", "Do not use"]`) and `orderReplyMessage.messageIds`.
- **DOC-03** — **appears once**, in the "Order Error Details" section's worked example: `"messageOptions":["Yes","No"]` shown as an occasional extension of the Alternate Response Object shape — but it is not in DOC-03's own primary field-list prose for that object, only in the example.
- **DOC-05** — absent from the page.

### Reconciliation

- **`order_id=-1`/`status=Failed`** → **observed, undocumented, and arguably contradicts the documented contract.** Every source that says anything at all describes rejection as a *distinct, differently-shaped* response (`{"error": ...}` or `advancedOrderReject`), not a success-row with sentinel values. This strengthens RTOS's P2 behavioral ask rather than weakening it: the conduit isn't failing to parse a documented rejection shape, it's failing to recognize that the row it received doesn't match *any* documented success shape either — `order_id: -1` has no documented meaning as a submission-response value anywhere (contrast with the *cancel*-response endpoint, P6, where `conid: -1` **is** a documented sentinel — see below; these are different endpoints and the sentinel doesn't transfer). Recommend the story spec treat this as "response didn't match the documented success schema" detection, not sentinel-sniffing.
- **`warning_message`** → **observed (in orders context), undocumented (in orders context) — but a same-named field is documented, always-null, on a different endpoint (FYI alerts).** Worth a one-line callout in the story spec so nobody later "fixes" this by copying the FYI schema's `warning_message: always null` assumption onto orders — the wire showed it non-null/populated on a rejected-child response.
- **`text`** → **documented + observed**, specifically on `advancedOrderReject` (DOC-01). This is the strongest-anchored of the four P2 fields — model it against DOC-01's `advancedOrderReject` shape directly.
- **`messageOptions`** → **documented in an example only (DOC-03), absent from the formal machine schema (DOC-01) and from DOC-05.** A genuine cross-source split: the older narrative doc shows it happening, the newer machine-readable schema doesn't model it. Treat as observed-and-partially-corroborated rather than fully undocumented — RTOS's wire observation (present on all three confirmation-prompt captures) plus DOC-03's one example together are stronger than either alone.

---

## P3 — `Position`: first-read field-sparse rows; ~25 unmodeled fields on enriched reads

### RTOS dossier (verbatim)

> the FIRST read of the session returned rows *missing modeled fields* (`name`, `ticker`) — the first-call sparse behavior applies to **fields on positions**, not just rows on trades. A second read returned the enriched shape.
>
> ```
> Response schema mismatch for .../positions/0 -> Position: Extra fields: [exchs, expiry, putOrCall, strike]. Missing fields: [name, ticker].
> Response schema mismatch for .../positions/0 -> Position: Extra fields: [exchs, expiry, putOrCall, strike, baseMktValue, baseMktPrice, baseAvgCost, baseAvgPrice, baseRealizedPnl, baseUnrealizedPnl, incrementRules, displayRule, time, chineseName, allExchanges, listingExchange, countryCode, lastTradingDay, group, sectorGroup, type, hasOptions, fullName, isEventContract, pageSize]. Missing fields: [].
> ```
>
> Of the extras, `baseMktValue`/`baseMktPrice`/`baseAvgCost`/`baseRealizedPnl`/`baseUnrealizedPnl` and `lastTradingDay`/`expiry`/`putOrCall`/`strike` are the ones RTOS could plausibly consume later.

### Doc scouting

**First-read sparseness (`name`/`ticker` missing, then present)**
- **DOC-01** — `individualPosition` schema documents both `name` and `ticker` as ordinary string properties; the schema has **no `required` array at all**, so nothing is marked required — but nothing is marked as "may be absent on first read" either. No presence caveat of any kind.
- **DOC-03** — documents `name`/`ticker` unconditionally (*"name: String. Returns the comapny name."* [sic], *"ticker: String. Returns the ticker symbol of the traded contract."*) with **no first-call caveat** in the Positions section. Notably, the *exact same pattern* IS documented elsewhere on this page, for a different endpoint (Watchlist): *"The first request may only return the values C, conid, and name values. Subsequent requests will add additional contract information."* — see the cross-cutting observation above.
- **DOC-05** — does not document the Positions listing endpoint at all. Its "Portfolio and Positions" section covers only `/portfolio/accounts`, `/portfolio/subaccounts`, and `/portfolio/{accountId}/ledger` — confirmed via full-text search that the literal string `portfolio/{accountId}/positions` never appears on the page.

**`base*` fields**
- **DOC-01** — all five documented on `individualPosition`: `baseMktValue`, `baseMktPrice`, `baseAvgCost`, `baseRealizedPnl`, `baseUnrealizedPnl` (plus `baseAvgPrice`, not asked but adjacent), each with a one-line description (e.g. *"Market value of the position in the account's base currency."*).
- **DOC-03** — none of the five appear anywhere on the page (confirmed via full-page search).
- **DOC-05** — doesn't cover Positions at all (see above).

**`lastTradingDay`/`expiry`/`putOrCall`/`strike`**
- **DOC-01** — all four documented. Typing/nullability is internally inconsistent: `expiry` and (implicitly, via example) `lastTradingDay`/`putOrCall` behave as nullable in practice but only `expiry` carries an explicit `"nullable": true` flag; `strike` is typed `string` with no nullable flag.
- **DOC-03** — all four also documented, with its own internal inconsistency: the base Positions endpoint's sample shows `"strike": 0.0` (a float) while prose types it `String`, but the sibling "Positions by Conid" endpoint types `strike` as `int` and shows `"strike": "0"` (a string) in its own sample — DOC-03 disagrees with itself across two sibling endpoints.
- **DOC-05** — silent (no Positions coverage).

### Reconciliation

- **First-read sparseness** → **observed, undocumented for Positions specifically — but a documented near-identical pattern exists for Watchlist (DOC-03).** This is the strongest piece of evidence for the cross-cutting "cold cache" observation above; recommend folding P3's sparse-read handling into that broader guarantee rather than a Position-only fix.
- **`base*` fields** → **documented (DOC-01) + observed (RTOS).** Clean, well-anchored — model against DOC-01's schema text directly; DOC-03's silence here is just narrative-doc incompleteness (per the registry's own note that the new/old doc split leaves gaps each way), not a conflict.
- **`lastTradingDay`/`expiry`/`putOrCall`/`strike`** → **documented + observed**, but with a live typing hazard: DOC-01 and DOC-03 each independently show internal type inconsistency on `strike` (string vs. float vs. int across their own examples/sibling endpoints). Recommend the story spec model `strike` defensively (e.g., string-or-numeric-tolerant parsing, or confirm the actual wire type via one probe sample) rather than trusting either source's stated type at face value.

---

## P4 — `LedgerEntry.endofbundle` modeled but absent on wire

### RTOS dossier (verbatim)

> Every ledger capture (4/4 across both runs):
> ```
> Response schema mismatch for /v1/api/portfolio/DUO873728/ledger -> LedgerEntry: Extra fields: []. Missing fields: [endofbundle].
> ```
> Make it optional/nullable (or drop it if it was speculative).

### Doc scouting

All three sources show the **same internal pattern**, independently:

- **DOC-01** — `endofbundle` is **not** among the `ledger` schema's formal properties (checked the full ~28-property list). It **does** appear in the endpoint's own worked response example, but only inside the `"USD"` currency block — the `"AUD"` and `"BASE"` blocks in the same example omit it.
- **DOC-03** — identical pattern: absent from the prose field-by-field description list, but present in the sample JSON's `"USD"` block only (`"BASE"` block in the same sample has no `endofbundle` key).
- **DOC-05** — same again: present only in the `"USD"` block of its "Querying Currency Balances" example, absent from `"BASE"`, with zero prose explaining the field anywhere on the page.

### Reconciliation

- **`endofbundle`** → **documented, absent from formal schema, but consistently present-in-example-for-one-currency-key across all three independent sources.** This cross-source agreement is unusually strong for something none of them formally documents — it suggests `endofbundle` is real (not "speculative" as RTOS's dossier wondered) but is a **per-response sentinel that IBKR attaches to only one entry of a multi-currency ledger** (plausibly the last entry serialized, i.e. literally "end of [this] bundle [of currency entries]"), not a per-currency-entry field. RTOS's wire observation (missing on 4/4 captures) is consistent with this too, *if* the observed account's ledger entries happened not to include the entry IBKR marks — worth a targeted live-probe question at grooming time: does `endofbundle` appear on exactly one entry per multi-currency ledger response, and is it always the same currency, or the last one serialized? That would resolve whether this is "nullable per-entry field" (RTOS's proposed fix) or "response-level marker misplaced inside one entry by IBKR's serializer" (a different, more interesting modeling choice — e.g., surface it as `LedgerResponse.IsComplete` rather than a per-entry nullable bool).

---

## P5 — `ContractSearchResult`: `showPrips`, `legSecType` unmodeled

### RTOS dossier (verbatim)

> ```
> Response schema mismatch for /v1/api/iserver/secdef/search -> ContractSearchResult: Extra fields: [showPrips, legSecType]. Missing fields: [].
> ```

### Doc scouting

- **DOC-01** — neither field appears in `secdefSearchResponse`'s properties (checked GET query params, POST body params, and the full response schema including nested `sections`/`issuers` objects), nor anywhere else in the 864KB document (whole-document grep, zero hits for either literal string).
- **DOC-03** — same result: the "Search Contract by Symbol" section's full documented field list (`conid`, `companyHeader`, `companyName`, `symbol`, `description`, `restricted`, `sections`, `issuers`, plus bond-only fields) does not include either field; whole-page search confirms zero occurrences anywhere on the page.
- **DOC-05** — the "Instrument Discovery" section (where this endpoint would live) is a stub: *"Documentation coming soon."* No content to check.

### Reconciliation

- **`showPrips`, `legSecType`** → **observed, undocumented — clean, total gap across every registered source, no doc even names anything adjacent.** No fork to close here beyond RTOS's own suggestion (nullable/optional-presence); this is the simplest item in the dossier.

---

## P6 — `CancelOrderResponse.account`; cancel-on-inactive/purged 400s

### RTOS dossier (verbatim)

> ```
> Response schema mismatch for /v1/api/iserver/account/DUO873728/order/1464106176 -> CancelOrderResponse: Extra fields: [account]. Missing fields: [].
> ```
>
> **Consider (behavioral, lower priority):** cancelling an `Inactive` order returned `IbkrApiError status=BadRequest … "Order is inactive"`, and a later cancel of the same (by then purged) id returned `BadRequest … "OrderID 1464106177 doesn't exist"`. Both surface as generic API errors; a typed/classified cancel outcome for already-dead orders... would let consumers treat "nothing to cancel" as the benign case it is instead of pattern-matching message text.

### Doc scouting

**`account` field**
- **DOC-01** — documented on `orderCancelSuccess`: *`"account": {"type": "string", "description": "IB account to which the order was originally set to clear.", "nullable": true}`*. The schema-level description is notable: *"Acknowledges IB's acceptance of the request to cancel the order. **Does not report whether the cancellation can or will ultimately be enacted.**"* Example shows `account: null` alongside `conid: -1` for one case.
- **DOC-03** — documents the same field with an explicit sentinel-value rule: *"account: String. Returns the accountId for the requested order to be cancelled. **Returns null for orders that were immediately cancelled on request.**"* (paired with the same rule for `conid` → `-1` in that case).
- **DOC-05** — documents `account` too, but with a **populated** (non-null) example: `{"msg": "Request was submitted", "order_id": 987654, "conid": 265598, "account": "DU123456"}`. It adds a distinct behavioral caveat: *"the above response indicates our request to cancel order 987654 was received, but not that the order ticket itself has been canceled. It is possible that an order working at an exchange or other external venue cannot be canceled, for instance, as a result of auction-related deadlines."*

**Cancel-on-inactive/purged classification**
- **DOC-01** — the cancel endpoint's error response reuses the same generic `orderSubmitError` shape as order-submission errors (`{"error": "<string>"}`), with a cancel-specific example: `"error": "OrderID 123456 doesn't exist"`. No distinct schema, enum, or status-code documentation exists for "already cancelled" vs. "already inactive" vs. "never existed" — one generic shape covers all cancel failures.
- **DOC-03** — same generic `{"error": "<string>"}` shape (example: `"OrderID 1 doesn't exist"`), and explicitly **has no dedicated error/status-code table for Cancel Order** — contrast noted directly against Place Order, which does have a dedicated "Order Error Details" table with status codes.
- **DOC-05** — doesn't address the inactive/filled/nonexistent cases at all; its only related caveat is the external-venue-deadline case quoted above, plus the general point that a cancel "success" ack never guarantees the order actually died.

### Reconciliation

- **`account`** → **documented + observed across all three sources**, with a genuinely useful modeling detail RTOS's dossier didn't have: DOC-01 and DOC-03 both independently document that `account: null` (paired with `conid: -1`) is a **specific documented sentinel** for "order was immediately cancelled on request" — not generic nullability. Recommend the story spec model this as a named case, not just `string?`.
- **Cancel-on-inactive/purged unclassified 400s** → **this is the platform's own documented design, not a conduit gap.** All three sources agree: CP Web API's cancel endpoint has exactly one generic error shape with no status-code table and no case-distinguishing field, for *any* cancel failure — confirmed by DOC-03's explicit contrast against Place Order's richer error table. This meaningfully changes the shape of RTOS's "consider" item: a typed/classified cancel-outcome enum on the conduit side can only be built by pattern-matching the `error` message text — the exact anti-pattern the P2 discussion is trying to move *away* from for submission responses. Also worth carrying into grooming: DOC-01's schema-level note that even a cancel *success* ack "does not report whether the cancellation can or will ultimately be enacted" means "nothing to cancel" and "cancel probably worked" are not cleanly separable from this endpoint's response alone, regardless of what conduit does — that's a genuine upstream design gap worth an ADR-level acknowledgment (a documented consumer obligation: "cancel acks are non-committal; confirm via live-orders/trades polling"), not something a typed wrapper alone can promise away.

---

## NC1 — Non-atomic bracket rejection (explicitly flagged as *not* a conduit issue)

### RTOS dossier (verbatim)

> IBKR accepted an invalid-child bracket group **non-atomically** — parent went live, child rejected — that is broker behavior RTOS is handling on its side.

### Doc scouting

- **DOC-03** (CP Web API-specific) — documents the bracket construction mechanism itself: parent submitted with a `cOID`, each child leg carries that value in its own `parentId` field; *"Bracket orders can be submitted sequentially... OR... using the cOID field for the parent order, and then use this same value in each of the child orders in the parentId field."* This is the entirety of the mechanics described. **Zero mentions of "atomic" or "partial"** anywhere in the Place Order or Bracket Orders sections (confirmed via full-text search — the only "atomic" hit on the page is unrelated CSS class noise).
- **DOC-05** — the "Submitting Bracket Orders" section is a stub: *"Documentation coming soon."* No content.
- **DOC-08** (order-type reference) — **Bracket Orders and OCA/OCA-Types sections have no CP Web API content at all** — no cURL tab exists in any of the three sections, confirmed structurally against 116 cURL-tab occurrences elsewhere on the same page, and corroborated by the page's own "Order Type by API" text distinguishing which language tabs map to which API. What DOC-08 *does* document, TWS-API-only, is a purpose-built mitigation for exactly this failure mode: the `IBApi.Order.Transmit` flag, held `false` on parent/first-child legs and only set `true` on the last leg, so *"the TWS will interpret this as a signal to transmit not only its parent order but also the rest of siblings, removing the risks of an accidental execution."* Quoted directly: *"Since a Bracket consists of three orders, there is always a risk that at least one of the orders gets filled before the entire bracket is sent. To avoid it, make use of the IBApi.Order.Transmit flag."*

### Reconciliation

- **No source documents CP Web API bracket-submission atomicity, in either direction.** RTOS/conduit's judgment call to treat this as broker behavior outside conduit's control is well-supported by the total absence of any contrary claim.
- **The more interesting finding is what DOC-08 reveals by contrast:** TWS API has a *named, documented, purpose-built mechanism* (`Transmit`) specifically to prevent this exact race — and CP Web API's single-HTTP-request bracket model has no analog described anywhere, on any source. That's not just "undocumented," it's a plausible **structural gap**: CP Web API brackets may be inherently more exposed to partial-acceptance races than TWS API brackets, because the platform's own documented mitigation isn't expressible in this API's request shape (there's no way to "hold" a leg client-side the way `Transmit=false` does). This is bigger than a conduit story — flag for the operator as a candidate design-doc/ADR note (not a fix conduit can make unilaterally; at most conduit could document the risk explicitly as a consumer-facing "brackets are not atomic on this API" guarantee-of-absence, mirroring what RTOS already does defensively).

---

## NC2 — `trades` endpoint first-call-empty artifact (explicitly flagged as *not* a conduit issue)

### RTOS dossier (verbatim)

> the `trades` endpoint's first-call-empty artifact — known IBKR quirk, RTOS primes before reading.

### Doc scouting

- **DOC-01** — the `/iserver/account/trades` operation description covers only the `days` parameter (max 7) and result windowing; no mention of first-call timing, caching, or empty-result behavior. The only "empty on first call" language anywhere in this source belongs to a *different* endpoint entirely (`/iserver/account/pnl/partitioned`: *"Initial request will return an empty array in the upnl object"*) — not transferable to trades.
- **DOC-03** — the entire narrative for this endpoint is one sentence: *"Returns a list of trades for the currently selected account for current day and six previous days. **It is advised to call this endpoint once per session.**"* No mechanism or reasoning is given for the advisory.
- **DOC-05** — the "Monitoring Executions" section (where trades/executions documentation would live) is a stub: *"Documentation coming soon."*

### Reconciliation

- **First-call-empty behavior** → **cryptically hinted (DOC-03), not explained anywhere.** DOC-03's "advised to call once per session" line is the only textual trace across all three sources of *anything* resembling a priming requirement — plausibly a euphemism for exactly the quirk RTOS/conduit already work around, but it never states a mechanism, so it can't be verified as the same behavior without a targeted live probe (submit trades call cold at session start N times, compare to the RTOS-observed pattern).
- **This item folds cleanly into the headline cross-cutting observation above** — it's the fourth endpoint in the "cold cache returns thin/empty on first touch" family (Watchlist documented, LiveOrders wire-confirmed, Positions and Trades newly surfaced by this dossier). Recommend it be addressed as part of that one cross-cutting guarantee rather than as an isolated trades-specific note, even though RTOS correctly scoped it out of conduit's fix list for now (RTOS already primes around it on its own side).

---

## Summary — claim taxonomy by field/behavior

| Item | Field/behavior | Taxonomy |
|---|---|---|
| P1 | `order_cancellation_by_system_reason` | documented (DOC-01) + observed |
| P1 | `parentId` (on live-orders *response*) | observed, undocumented (documented only as a request field) |
| P1 | `ocaGroupId` | observed, undocumented (no analog in any source, incl. TWS-side naming) |
| P2 | `order_id=-1`/`status=Failed` shape | observed, undocumented (contradicts documented success/rejection shapes) |
| P2 | `warning_message` (orders context) | observed, undocumented (same name documented elsewhere, always-null, different endpoint) |
| P2 | `text` (on `advancedOrderReject`) | documented (DOC-01) + observed |
| P2 | `messageOptions` | documented in example only (DOC-03), absent from formal schema (DOC-01/05) |
| P3 | first-read sparse `name`/`ticker` | observed, undocumented for Positions; documented analog exists for Watchlist |
| P3 | `base*` PnL/value fields | documented (DOC-01) + observed |
| P3 | `lastTradingDay`/`expiry`/`putOrCall`/`strike` | documented (DOC-01 + DOC-03) + observed; typing unstable across sources |
| P4 | `endofbundle` | documented in example only, absent from formal schema — consistent across all 3 sources |
| P5 | `showPrips`, `legSecType` | absent from both docs and every source (clean gap) |
| P6 | `account` (cancel response) | documented (all 3 sources) + observed; `null`+`conid:-1` is a documented sentinel pair |
| P6 | cancel-on-inactive/purged classification | absent from both — platform design gap, not a conduit gap |
| NC1 | bracket submission atomicity | absent from both (CP Web API); TWS-side mitigation exists but has no CP API analog |
| NC2 | trades first-call-empty | absent from both, with one cryptic non-explanatory hint (DOC-03) |

---

## Suggested next steps

Not a backlog decomposition — that's `draft-backlog`'s job, on the operator's call. Flagging what this scouting pass surfaces as needing attention before/at that stage:

1. **The cross-cutting "cold cache" pattern** (Watchlist/LiveOrders/Positions/Trades) is a strong candidate for one design-doc/ADR note rather than N independent field fixes — extends VCR D4's reasoning (design doc §10.6).
2. **P1's `ocaGroupId` and P2's `order_id=-1`/`Failed` shape** are the two least-anchored findings (zero doc corroboration anywhere) — both are 📦 public-surface and P2 is additionally behavioral/`Risk: high` territory (order classification) per `.claude/rules/backlog-format.md`; likely candidates for a live-probe hypothesis at grooming rather than proceeding on RTOS's wire sample alone.
3. **P4's `endofbundle`** placement (per-entry vs. response-level marker) is worth one targeted probe question before locking in a DTO shape — the three-source agreement on "present only in one currency block" is too consistent to be coincidental.
4. **P6's cancel-classification "consider" item** may need re-scoping at grooming: the platform gives conduit no structured signal to classify on, so a typed outcome enum can only be message-text-driven — worth deciding explicitly whether that's acceptable before speccing it, rather than treating it as a straightforward typed-error story.
5. **NC1's TWS-vs-CP-API atomicity gap** is arguably a finding in its own right beyond "not a conduit issue" — consider whether it belongs in the design doc as a documented absence-of-guarantee, even without a code change attached.
