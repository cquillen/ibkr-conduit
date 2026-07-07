# Order-type enum & trailing parameters — IBKR doc evidence

**Question:** What do IBKR's live docs claim as the admissible `orderType` values for `POST /iserver/account/{accountId}/orders`, the `trailingAmt`/`trailingType` semantics and requiredness, and the `price`/`auxPrice` requirements per order type? (Re-grooms PVR-05's claim side and the VCR-11 enum pin, previously cited from the deprecated mirror at :4507.)
**Date:** 2026-07-07 · **Sources consulted:** DOC-01, DOC-03, DOC-05 · **Amended same day:** DOC-08 (registered mid-re-groom) added below — it changes the enum reconciliation.

> ⚠️ Doc sections record what IBKR's documentation CLAIMS as of the date above — not wire-verified.
> Presence claims are never per-message guarantees: WS is confirmed sparse; REST sparseness unconfirmed.
> Wire sections cite recordings/ paths and sample counts.

## Per-source findings

### DOC-03 (retrieved 2026-07-07, `<section id="place-order">` under Endpoints → Orders)

The **only** source enumerating the submission enum, exact:

> "orderType: String. Required — … Available Order Types: LMT, MKT, STP, STOP_LIMIT, MIDPRICE, TRAIL, TRAILLMT"

`STP_LMT`, `MOC`, `LOC`: **zero occurrences anywhere on the page.** Trailing fields, exact:

> "trailingType: String. Required for TRAIL and TRAILLMT order — … You must specify both trailingType and trailingAmt for TRAIL and TRAILLMT order. Valid Values: "amt" or "%""
> "trailingAmt: float. Required for TRAIL and TRAILLMT order — optional if order is TRAIL, or TRAILLMT. When trailingType is amt, this is the trailing amount. When trailingType is %, it means percentage."

(`trailingAmt`'s entry self-contradicts — "Required … optional if" — reported as-is; the paired "must specify both" sentence on `trailingType` is unambiguous.) Price fields, exact:

> "price: float. Required for LMT or STOP_LIMIT — This is typically the limit price. For STP|TRAIL this is the stop price. For MIDPRICE this is the option price cap."
> "auxPrice: float. Required for STOP_LIMIT and TRAILLMT orders. … You must specify both price and auxPrice for STOP_LIMIT|TRAILLMT orders."

Gap: the section defers advanced order-type detail to a **separate page** — `https://www.interactivebrokers.com/campus/ibkr-api-page/order-types/` — not in the registry.

### DOC-01 (retrieved 2026-07-07, `components.schemas.singleOrderSubmissionRequest`; live `info.version` 2.35.0, matches registry)

**No `orderType` enum on the submission schema** — `{"type": "string", "description": "IB order type identifier."}`, bare. Schema `required` = `["conid", "orderType", "quantity", "side", "tif"]`; no `oneOf`/discriminator, so no conditional per-order-type requirements are expressible or expressed. Trailing fields:

> `"trailingType": {"type": "string", "description": "Specifies the type of trailing used with a Trailing order.", "enum": ["amt", "%"]}`
> `"trailingAmt": {"type": "number", "description": "Offset used with Trailing orders."}`

`price` = "Price of the order ticket, where applicable."; `auxPrice` = "Additional price value used in certain order types, such as stop orders." — no joint-requirement statement. Caution for future readers: the unrelated `contractRules.orderTypes` enum (`limit, midprice, market, stop, stop_limit, mit, lit, trailing_stop, trailing_stop_limit, relative, marketonclose, limitonclose`) is a lower_snake_case *contract-permitted-types* list for `/iserver/contract/{conid}/info-and-rules` — a different namespace; the source never maps it to the submission codes.

### DOC-05 (retrieved 2026-07-07, h3 "New Order Example" `#new-order-example-25`)

**Covers none of it.** Only `"orderType":"LMT"` appears (worked example); `trailing*`/`auxPrice`/`STOP_LIMIT` etc.: zero occurrences. Explicitly defers: "More information on the construction of order tickets can be found on our Order Types page" and "consult our Reference Material for a list of all JSON keys". "Submitting Bracket Orders" is a "Documentation coming soon" stub.

### DOC-08 (retrieved 2026-07-07 during registration; single page, per-order-type sections with tabbed examples — the cURL tab is the CP Web API form)

The page both narrative sources defer to. Its CP API cURL examples (`POST /iserver/account/{accountId}/orders`, JSON order tickets) use `orderType` values **beyond DOC-03's seven-value list**: observed `"orderType"` JSON values across the page are `LMT` (13), `MKT` (2), `TRAIL` (2), and one each of `LIT`, `LOC`, `MIT`, `MOC`, `MIDPRICE`, `REL`, `STP`, `TRAILLMT`. E.g. the Limit On Close section's cURL tab posts `{"orders": [{"conid": conid, "orderType": "LOC", "price": price, …}]}` to the CP API endpoint. No prose on this page states the admissible submission enum either — these are worked examples, IBKR's claim-by-example that the CP API accepts them.

## Wire observations

- TRAIL acceptance (paper account, 2026-07-07, `recordings/order-probe-2026-07-07.log`, 1 sample): raw order with `trailingAmt: 50, trailingType: "amt"` → question `o10331` → reply → `order_id 261920143`, `PreSubmitted`, then cancelled. The wire accepts exactly the PVR-05 surface.
- The enum itself has no direct wire pin (you cannot enumerate an enum by probing); the claim tier is DOC-03's list above, which matches the deprecated mirror's pinned list verbatim — no drift between mirror-era and live docs on this point.

## Reconciliation

- **Agreed:** `trailingType ∈ {"amt", "%"}` (DOC-01 enum + DOC-03 prose — the only claim both sources state). `STP_LMT` appears in **no** source as a submission value.
- **Conflicts:** **the admissible-enum claim is cross-source contradicted.** DOC-03's place-order section states a closed seven-value list ("Available Order Types: LMT, MKT, STP, STOP_LIMIT, MIDPRICE, TRAIL, TRAILLMT"), but DOC-08 — the page DOC-03 itself defers to for advanced order types — shows CP API worked examples submitting `LOC`, `MOC`, `MIT`, `LIT`, `REL`. **Unresolved by docs; wire-unverified** (no probe has placed a MOC/LOC/MIT/LIT/REL order). Consequence tracked: VCR-11's shipped XML docs pinned the seven-value list — if the DOC-08 examples reflect the wire, those docs are under-inclusive; a follow-up probe + doc-fix story is the candidate remedy (no PVR design depends on the enum being closed — PVR-05's fail-fast keys on TRAIL/TRAILLMT presence only). One **intra-source** contradiction: DOC-03's `trailingAmt` "Required … optional if" wording; its unambiguous "must specify both" sentence and the wire acceptance carry the decision.
- **Gaps:** DOC-01 (the machine schema) documents no submission enum and no conditional requirements at all — the closed-list claim rests on DOC-03 alone, with DOC-08's examples claiming a wider set.
- **Presence claims:**
  - Seven-value submission enum: **documented (DOC-03 only), contradicted-by-example (DOC-08)** + TRAIL observed accepted on the wire (1 sample). LMT/MKT/STP/STOP_LIMIT usage is additionally exercised throughout this repo's recordings; MIDPRICE/TRAILLMT have no wire sample; the DOC-08 extras (LOC/MOC/MIT/LIT/REL) are documented-by-example, absent from samples.
  - `trailingAmt`/`trailingType` required-for-TRAIL/TRAILLMT: **documented (DOC-03)**; wire shows presence-accepted (1 sample); absence-rejection (the fail-fast case PVR-05 implements client-side) is deliberately NOT wire-probed — PVR-05 validates before wire activity precisely so the upstream behavior needn't be pinned.
  - STOP_LIMIT dual `price`+`auxPrice`: **documented (DOC-03, stated twice)**; consistent with the VCR-11-shipped XML docs.

**Answer for the consuming decisions (PVR-05 §9.7; VCR-11 historical):** the groomed design stands unchanged — add `TrailingAmt`/`TrailingType` with client-side fail-fast when `TRAIL`/`TRAILLMT` lacks them, values `"amt"`/`"%"`. The enum pin migrates from the deprecated mirror citation to DOC-03 (live, 2026-07-07) with wire support for TRAIL — **now qualified by the DOC-08 conflict above**: treat DOC-03's list as the documented core, not a proven-closed set. Follow-up candidate (outside Stream PVR): probe LOC/MOC acceptance on the paper account and, if accepted, widen the VCR-11-shipped XML docs.
