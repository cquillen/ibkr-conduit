# ADR-0005 — Subscription-scoped streaming delivery: full-topic-identity routing

**Status:** Accepted · **Date:** 2026-07-07
**Relates to:** ADR-0002 (extends its delivery guarantee — does not supersede it); design doc §12.5/§12.8; findings PRB-1.1, PRB-1.2, PRB-3.1 (high), PRB-1.3 (`docs/findings/2026-07-07-multi-agent-code-review.md`). **Implemented by:** PVR-01 (`docs/backlog.md`, Stream PVR).

## Context

IBKR echoes target-qualified wire topics on solicited per-target streams — `"smd+265598"`, `"ssd+DU1234567"` — but the client registers every such subscription under the bare topic prefix (`"smd"`, `"ssd"`, `"sld"`) and dispatch truncates the incoming topic at `+`. Two concurrent subscriptions for **different** conids or accounts therefore each receive **both** targets' frames, and nothing in the public surface says so: the facade docs promise data "for the specified account/contract." For a trading system, cross-delivered market data or account-money rows are silently wrong data (the 2026-07-07 review's highest-severity cluster). ADR-0002 recorded what the surface promises about *loss* (at-most-once, loss-is-observable) but is silent on *scoping* — which frames a subscription is promised in the first place. Additionally, consumer-supplied conid/accountId/field strings are interpolated into subscribe wire messages with no escaping or validation (PRB-1.3), so a malformed target can corrupt the wire protocol or another topic's semantics.

## Decision

1. **Delivery is subscription-scoped.** A solicited per-target subscription (`smd`, `ssd`, `sld` — any topic whose subscribe message carries a target segment) is registered and dispatched by its **full wire-topic identity** (prefix + target segment, e.g. `smd+265598`). A subscription receives exactly its target's frames.
2. **Target-less topics keep prefix routing.** Solicited target-less topics (`sor`, `spl`, `str`) and unsolicited topics (`sts`, `system`, `act`, `blt`, `ntf`) have no target segment; prefix routing remains their identity.
3. **Same-target duplicates fan out.** Two subscriptions for the same full topic each receive every frame, consistent with the existing cancel refcounting (§12.5.1).
4. **Unmatched target-qualified frames drop observably.** A frame whose full topic matches no live subscription (late frame after unsubscribe, server-initiated stream) is never cross-delivered; it is counted under a distinct cause in the ADR-0002 drop taxonomy, not silently discarded.
5. **The facade validates subscribe inputs.** Consumer-supplied target segments are rejected when malformed (`+`, whitespace, empty), and args objects are serializer-built (escaped), so no unvalidated consumer string reaches the wire protocol.

## Alternatives considered

- **Facade-side filtering** (keep prefix routing; wrap each mapper to skip frames whose `ConId`/`AccountId` doesn't match): same consumer-visible result, but every subscription deserializes every same-prefix frame — per-subscription CPU scales with total same-prefix traffic — and the transport layer keeps misrouting internally, leaving a trap for any future code that consumes readers directly. Rejected.
- **Documented consumer obligation** (record the cross-delivery; consumers filter by `ConId`/`AccountId`): cheapest, but makes silently-wrong-data the default experience and contradicts the facade's existing per-target method shapes. Rejected.
- **Status quo** (undocumented cross-delivery): the review's highest-severity cluster; wrong market/account data delivered to a money consumer with no signal. Rejected.

## Consequences

- 📦 **Breaking-behavioral candidate** (semver reviewed at grooming): a consumer that relied on one subscription receiving *other* targets' frames — unlikely, and contrary to the documented intent — breaks. RTOS consumes `str`/`sor` (target-less) and is unaffected.
- Multi-conid market data and multi-account summary/ledger streaming become correct by default; consumers need no filtering knowledge.
- The drop taxonomy gains an unmatched-frame cause — one more observable, one more counter tag to document.
- Cost: routing carries per-target registry entries instead of one per prefix; the full-topic match must fall back cleanly for target-less topics (two-tier lookup).

## Relationships

Extends [ADR-0002](0002-streaming-delivery-guarantee.md) (loss observability) with the scoping half of the delivery guarantee; design doc §12.8 (amended) and §12.5 (topic reference); findings PRB-1.1/1.2/3.1/1.3; implemented by PVR-01, whose lane follows PVR-15/PVR-16 (same files).
