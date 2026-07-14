# ADR-0010 — Bracket/OCA order atomicity: no client-side emulation

**Status:** Accepted · **Date:** 2026-07-13
**Relates to:** findings NC1 (`docs/findings/2026-07-13-rtos-probe-dossier-doc-scouting.md`); design doc §9.8 (sibling — the existing combo-leg-risk note), new §9.12.

## Context

RTOS's 2026-07-13 dossier explicitly flagged that IBKR accepted an invalid-child bracket group **non-atomically** — the parent went live while the child was rejected — as broker behavior outside conduit's control, not a defect to fix on RTOS's side. Doc scouting confirmed no CP Web API source documents an atomicity guarantee for bracket/OCA submission in either direction: DOC-03's Place Order / Bracket Orders sections describe only the request-side linkage mechanism (`cOID` on the parent, `parentId` on each child) with zero mentions of "atomic" or "partial" acceptance; DOC-05's "Submitting Bracket Orders" section is an undocumented stub ("Documentation coming soon").

DOC-08 (the per-order-type reference) additionally showed that **TWS API** has a purpose-built mitigation for exactly this race: the `IBApi.Order.Transmit` flag, held `false` on the parent and earlier child legs and set `true` only on the last leg, so the TWS client library withholds transmission of every leg until the last one is sent — *"there is always a risk that at least one of the orders gets filled before the entire bracket is sent. To avoid it, make use of the IBApi.Order.Transmit flag."* Critically, **this mechanism has no CP Web API analog on any source**: the Bracket Orders and OCA sections on DOC-08 carry no CP Web API (cURL) content at all — confirmed structurally (no `tab-curl` in any of the three relevant sections, versus 116 cURL-tab occurrences elsewhere on the same page) — and neither DOC-03 nor DOC-05 describe any equivalent client-side hold mechanism for the CP Web API's single-HTTP-request bracket model.

## Decision

IbkrConduit does **not** attempt to emulate TWS API's `Transmit`-flag atomicity guarantee for bracket/OCA groups submitted via the CP Web API — no client-side staged transmission, no rollback-on-partial-failure, no synthetic cancel-remaining-legs-on-first-rejection behavior. The absence of an atomicity guarantee is recorded here as a documented fact of the CP Web API surface, not remedied. `PlaceOrdersAsync`'s documentation states plainly that legs may partially transmit and directs consumers to reconcile via the per-leg classification (ADR-0008, design doc §9.11) or `GetLiveOrdersAsync`.

## Alternatives considered

- **Client-side staged transmission** (submit the parent held back, then children, only "committing" all legs after every response is known good — mimicking `Transmit=false` semantics purely in conduit's HTTP client): rejected. The CP Web API's `POST /iserver/account/{accountId}/orders` is a single-request, single-response operation — there is no documented mechanism to submit an order to IBKR without it going live, so conduit cannot hold a leg back the way TWS API's client-side `Transmit` flag does. Any emulation would have to submit all legs and then attempt to cancel back out on partial failure, which introduces its own race (the cancel might itself fail, or arrive after a fill) and does not actually deliver an atomicity guarantee — it would misrepresent a capability the library does not have.
- **Synthetic rollback** (auto-cancel surviving legs when a sibling leg is rejected): rejected for the same reason — a best-effort rollback is not atomicity, and presenting it as such would be more dangerous than the current honest gap, since a consumer could reasonably assume the rollback is reliable when it cannot be, given the CP Web API provides no compensating-transaction primitive.
- **Status quo, undocumented**: leaves the absence of a guarantee implicit, discoverable only by consumers hitting the failure mode directly (as RTOS did). Rejected — recording it costs nothing and prevents every future consumer from re-discovering RTOS's finding independently.

## Consequences

- No code change. Documentation-only: `docs/ibkr_conduit_design.md` §9.12 states the absence of guarantee explicitly; `PlaceOrdersAsync`'s XML doc gets a corresponding note.
- Consumers building bracket/OCA order flows must independently handle partial-leg outcomes — ADR-0008 gives them the tools (per-leg classification) but not a guarantee that partial acceptance cannot happen in the first place.
- No semver consequence — a documentation clarification, not a surface or behavior change.

## Relationships

Design doc §9.8 (sibling — leg-risk is a recurring theme across combo and bracket/OCA multi-order submission), new §9.12; findings doc NC1 (`docs/findings/2026-07-13-rtos-probe-dossier-doc-scouting.md`); ADR-0008 (gives consumers the per-leg visibility this ADR says they need, since atomicity itself is not offered).
