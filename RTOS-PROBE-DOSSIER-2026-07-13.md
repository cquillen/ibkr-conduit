# RTOS live-paper probe dossier — conduit fix items (2026-07-13)

**Source:** RTOS's IBV-P live-paper probe suite (`realtest-order-steward/tests/RealTestOrderSteward.LivePaperTests`, story IBV-P) run 2026-07-13 ~16:43–16:55 UTC against paper account `DUO873728` through **IbkrConduit 0.9.0** (nuget.org). Run cOID prefix `RTOSP5815f0fe9` (plus `RTOSPe6ea1f680` for the connectivity-only run). Every schema warning below was emitted by the conduit's own `ResponseSchemaValidationHandler` against live CPAPI responses — this dossier is the consumer-side handoff so each becomes a conduit-side fix (RTOS never works around conduit schema drift; its dependency-lifecycle rule pins that discipline).

**How to verify fixes:** the conduit's recording-validation approach — each item lists the endpoint, DTO, and the verbatim live evidence. Items are ordered by priority for RTOS.

**Explicitly NOT conduit issues** (recorded here only so nobody chases them): (a) IBKR accepted an invalid-child bracket group **non-atomically** — parent went live, child rejected — that is broker behavior RTOS is handling on its side; (b) the `trades` endpoint's first-call-empty artifact — known IBKR quirk, RTOS primes before reading.

---

## P1 — `LiveOrder` rows: promote `parentId`, `ocaGroupId`, `order_cancellation_by_system_reason` to typed fields ⭐ load-bearing

**Endpoint:** `GET /v1/api/iserver/account/orders` → `OrdersResponse` (order rows).
**Why it matters to RTOS:** the venue's bracket child-id census resolves child→parent linkage from these fields — today they surface only via `[JsonExtensionData]` (`AdditionalData`), which the probe had to spelunk (`parent-linkage keys=[parentId]`). `order_cancellation_by_system_reason` is classification-grade data for cancel handling. These are contract-bearing, not cosmetic.

Verbatim (union of observed extras across the run):

```
Response schema mismatch for /v1/api/iserver/account/orders -> OrdersResponse: Extra fields: [acct, cashCcy, sizeAndFills, description1, outsideRTH, origOrderType, supportsTaxOpt, lastExecutionTime, bgColor, fgColor, isEventTrading, lastExecutionTime_r, parentId, ocaGroupId, order_cancellation_by_system_reason]. Missing fields: [].
```

Notes: `parentId`/`ocaGroupId`/`order_cancellation_by_system_reason` appear only on responses that actually contain bracket/cancelled rows (three of the six captures). `outsideRTH` was absent from one early capture — model all of these as nullable/optional presence. Suggested split: type the load-bearing three first; the cosmetic display fields (`bgColor`, `fgColor`, …) can be typed or deliberately left unmodeled — if left, consider excluding known-cosmetic fields from the mismatch warning to cut log noise.

## P2 — `OrderSubmissionResponse`: a `order_id=-1, status=Failed` row parses as success-shaped ⭐ behavioral

**Endpoint:** `POST /v1/api/iserver/account/{acct}/orders` → `OrderSubmissionResponse`.
**Behavioral half (the important one):** submitting a bracket group whose child is deliberately invalid returned a row the conduit surfaced as a normal `OrderSubmitted` with `order_id=-1`, `status=Failed` — the RTOS probe recorded it as `POST outcome: ACCEPTED?! order_id=-1 status=Failed`. A `-1`/`Failed` submission row is a rejection and should surface through the typed error path (`IbkrOrderRejectedError` or equivalent), never as a success-shaped result the consumer must sniff for sentinel values. (Context: in the same exchange IBKR **partially placed** the group — parent live, child dead — so the consumer's classification of this row is safety-relevant.)

**Schema half:** two unmodeled response fields carry the rejection/warning detail — exactly the data the typed rejection needs:

```
Response schema mismatch for /v1/api/iserver/account/DUO873728/orders -> OrderSubmissionResponse: Extra fields: [warning_message, text]. Missing fields: [].
Response schema mismatch for /v1/api/iserver/account/DUO873728/orders -> OrderSubmissionResponse: Extra fields: [messageOptions]. Missing fields: [].
```

`messageOptions` appeared on every confirmation-prompt submission response (three captures); `warning_message`/`text` on the failed-child response.

## P3 — `Position`: first-read rows are FIELD-sparse (`name`/`ticker` absent); enriched rows carry ~25 unmodeled fields

**Endpoint:** `GET /v1/api/portfolio/{acct}/positions/{page}` → `Position`.
**The subtle part:** the FIRST read of the session returned rows *missing modeled fields* (`name`, `ticker`) — the first-call sparse behavior applies to **fields on positions**, not just rows on trades. A second read returned the enriched shape. So: `name`/`ticker` (and anything similar) must be nullable/optional-presence, and consumers warned that first-read position rows can be skeletal.

```
Response schema mismatch for /v1/api/portfolio/DUO873728/positions/0 -> Position: Extra fields: [exchs, expiry, putOrCall, strike]. Missing fields: [name, ticker].
Response schema mismatch for /v1/api/portfolio/DUO873728/positions/0 -> Position: Extra fields: [exchs, expiry, putOrCall, strike, baseMktValue, baseMktPrice, baseAvgCost, baseAvgPrice, baseRealizedPnl, baseUnrealizedPnl, incrementRules, displayRule, time, chineseName, allExchanges, listingExchange, countryCode, lastTradingDay, group, sectorGroup, type, hasOptions, fullName, isEventContract, pageSize]. Missing fields: [].
```

Of the extras, `baseMktValue`/`baseMktPrice`/`baseAvgCost`/`baseRealizedPnl`/`baseUnrealizedPnl` and `lastTradingDay`/`expiry`/`putOrCall`/`strike` are the ones RTOS could plausibly consume later (reporting marks, options/futures); the rest are display/meta.

## P4 — `LedgerEntry`: `endofbundle` modeled but absent on wire

**Endpoint:** `GET /v1/api/portfolio/{acct}/ledger` → `LedgerEntry`. Every ledger capture (4/4 across both runs):

```
Response schema mismatch for /v1/api/portfolio/DUO873728/ledger -> LedgerEntry: Extra fields: []. Missing fields: [endofbundle].
```

Make it optional/nullable (or drop it if it was speculative).

## P5 — `ContractSearchResult`: two unmodeled fields

**Endpoint:** `GET /v1/api/iserver/secdef/search`:

```
Response schema mismatch for /v1/api/iserver/secdef/search -> ContractSearchResult: Extra fields: [showPrips, legSecType]. Missing fields: [].
```

## P6 — `CancelOrderResponse`: unmodeled `account`; and cancel-on-inactive/purged orders return unclassified 400s

**Endpoint:** `DELETE /v1/api/iserver/account/{acct}/order/{orderId}`.

```
Response schema mismatch for /v1/api/iserver/account/DUO873728/order/1464106176 -> CancelOrderResponse: Extra fields: [account]. Missing fields: [].
```

**Consider (behavioral, lower priority):** cancelling an `Inactive` order returned `IbkrApiError status=BadRequest … "Order is inactive"`, and a later cancel of the same (by then purged) id returned `BadRequest … "OrderID 1464106177 doesn't exist"`. Both surface as generic API errors; a typed/classified cancel outcome for already-dead orders (inactive / not-found) would let consumers treat "nothing to cancel" as the benign case it is instead of pattern-matching message text.

---

## Raw evidence

Full findings doc + complete run log are in the RTOS workspace (`ibkr-paper-probe-findings-full.md`, `live-paper-full-run.log` — ask the RTOS side if needed); every warning above is quoted verbatim and each fires deterministically on the listed endpoint, so a live re-run (or a fresh recording) reproduces them.
