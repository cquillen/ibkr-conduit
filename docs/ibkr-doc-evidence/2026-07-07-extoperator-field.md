# `extOperator` order field — IBKR doc evidence

**Question:** Do IBKR's live docs document an `extOperator` field on CP Web API order submission, and is it required for futures compliance? (Closes the unverified claim behind finding WIR-6's ExtOperator suggestion — backlog item VCR-12's recorded unblock.)
**Date:** 2026-07-07 · **Sources consulted:** DOC-01, DOC-03, DOC-05 (skipped DOC-06/08/09/10 — off topic: different product / per-order-type reference / changelog / entitlements)

> ⚠️ Doc sections record what IBKR's documentation CLAIMS as of the date above — not wire-verified.
> Presence claims are never per-message guarantees: WS is confirmed sparse; REST sparseness unconfirmed.
> Wire sections cite recordings/ paths and sample counts.

## Per-source findings

### DOC-03 (retrieved 2026-07-07, `<section id="place-order">`, `<section id="cancel-order">`, `<section id="modify-order">`)

**Documented in all three order-mutation sections**, identically (body param on place/modify; query param on cancel), exact:

> "**extOperator:** string. Required* — **IMPORTANT** This field is required when trading Futures and Futures Options contracts to remain in compliance with [CME Group Rule 536-B](https://www.cmegroup.com/rulebook/files/cme-group-Rule-536-B-Tag1028.pdf). The External Operator field should contain information regarding the submitting user in charge of the API operation at the time of request submission."

Worked examples carry `"extOperator":"person1234"` (place/modify bodies) and `?manualIndicator=true&extOperator=person1234` (cancel query). The sibling `manualIndicator` (boolean) carries the **verbatim same** CME 536-B conditional sentence — a paired obligation (pairing inferred from identical phrasing; not stated as a dependency). No tie to HKFE/OSE/institutional contexts anywhere; no statement of omission consequences on non-futures orders.

### DOC-01 (retrieved 2026-07-07, `components.schemas.singleOrderSubmissionRequest.properties.extOperator` + cancel-order query param; live `info.version` 2.35.0, matches registry)

Documented, minimal: `{"type": "string", "description": "ExtOperator is used to identify external operator"}` on the submission schema (reaches `POST …/orders`, `POST …/orders/whatif`, `POST …/order/{orderId}` modify) and as `"required": false` query param on `DELETE …/order/{orderId}`. **Not** in the schema's `required` array (`["conid", "orderType", "quantity", "side", "tif"]`) — but this source expresses no conditional requirements for any field (no `oneOf`/discriminator; established in `2026-07-07-ordertype-enum-trailing-params.md`). Its futures-compliance prose attaches to **`manualIndicator`** instead, exact:

> "For all orders for US Futures products, clients must submit this flag… Orders for USFUT products that do not include this field will be rejected."

Not present on `orderPreview`, `orderStatus`, or any response schema.

### DOC-05 (retrieved 2026-07-07, full Orders h2 + Getting Started sweep)

**Zero coverage** — no `extOperator`/`ext_operator`/"external operator" anywhere; its minimum-fields list is the same five (`conid, orderType, side, tif, quantity`); its compliance prose is vendor-onboarding only. Several Orders h3s remain "Documentation coming soon" stubs, so silence is weak evidence. Possible-new-doc-sources flags: the Order Types page (= DOC-08) and the Web API reference (= DOC-02/DOC-01) — both already registered.

## Wire observations

None for this field. The paper account has sent `extOperator` only as the **cancel query param** the library already wires (`IIbkrOrderApi.cs:31`) — no capture exercises it on a submission body, and the enforcement claim (futures order rejected without it) would need a USFUT order probe, not run. Library state at evidence time: `ManualIndicator` already exists on the body models (`IIbkrOrderApiModels.cs:48,:120`); `ExtOperator` exists **only** as the cancel/modify query parameter — the body field is the gap.

## Reconciliation

- **Agreed (DOC-01 + DOC-03):** `extOperator` is a real, documented CP API order-submission field — camelCase string on the order body (place/modify/whatif) and a cancel query param.
- **Conflicts:** none substantive. DOC-01's schema marks it optional with a tautological description; DOC-03 marks it "Required*" conditionally (futures/futures-options, CME 536-B) — consistent, since DOC-01 cannot express conditional requirements at all. Nuance: DOC-01 hangs its own futures-compliance language (USFUT rejection) on `manualIndicator`, DOC-03 hangs the identical CME 536-B sentence on **both** fields — the two fields are companion obligations under the same rule.
- **Gaps:** no source states omission consequences for `extOperator` specifically (vs `manualIndicator`'s documented rejection), valid value format, or non-CME exchange applicability. DOC-05 documents nothing.
- **Presence claims:** `extOperator` on the submission body — **documented (DOC-01 + DOC-03), no wire sample**. The conditional requirement — **documented (DOC-03 only), not wire-verified** (no USFUT probe).

**Answer for the consuming decision (VCR-12):** WIR-6's suggestion is **claim-tier confirmed** — `extOperator` is a documented order-body field with a documented futures/futures-options compliance condition (CME Group Rule 536-B), and the library's gap is precisely the body field (`OrderRequest`/`OrderWireModel`), since `ManualIndicator` and the cancel-side query params already exist. The addition is additive (nullable string, omitted when null — the PVR-05 trailing-params pattern). Enforcement (rejection without it) stays documented-not-verified; a design that merely passes the field through (no client-side gating) is safe under both answers.
