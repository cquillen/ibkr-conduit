# ADR-0006 — Order confirmation window: serialized confirmation round, ambiguous invalidated-reply outcome

**Status:** Accepted · **Date:** 2026-07-07 *(revised same day, pre-merge, after the grooming live probe below — the original "reply-immediately obligation + typed definitive refusal" decision was falsified by the probe before it ever merged)*
**Relates to:** ADR-0003 (order-outcome trichotomy — sibling decision); design doc §9.1/§9.9/§9.10; finding ORD-3 (`docs/findings/2026-07-07-multi-agent-code-review.md`); captured spec `docs/ibkr-web-api-spec.md` (reply invalidation, ~4559); live probe `recordings/order-probe-2026-07-07.log`. **Implemented by:** PVR-06 (`docs/backlog.md`, Stream PVR).

## Context

IBKR's question/reply flow leaves a window: after a placement returns a confirmation question and before the reply is sent, a subsequent order submission on the session invalidates the pending confirmation. The library's per-account order semaphore serializes individual calls but is released between the placement and the reply, so a concurrent placement can hit the window. Separately (ORD-1), a 2xx reply body that is empty/whitespace/non-JSON escapes classification as a raw `InvalidOperationException`.

**Live probe evidence (paper account, 2026-07-07):** with two same-type confirmations pending in parallel (orders A then B, both question `o354`): replying to A returned **503 with the fully generic body `{"error":"Service Unavailable","statusCode":503}`** — no invalidation marker — and yet **order A later became a live Submitted order anyway** (released, evidently, by confirming B's same-type question). Two consequences: (1) the invalidated-reply outcome is *not* a definitive refusal — treating it as "refused → re-place" can **double-place**; (2) overlapping confirmation windows entangle outcomes across orders — a reply's effect is not attributable to its own order. The originally-drafted decision (documented reply-immediately obligation + typed definitive refusal) was falsified by (1) and revised to the below.

## Decision

1. **The confirmation round is serialized in-process.** The per-account order lock is held from a placement that returns a confirmation until that confirmation resolves — a reply (confirm or reject), an explicit dismiss, or the confirmation timeout. Overlapping same-account confirmation windows are structurally impossible for in-process consumers.
2. **A confirmation timeout bounds the lock.** A consumer that never replies cannot wedge the account's order flow: after a configurable confirmation window expires, the lock is released and that order's outcome is **ambiguous** (reconcile before resubmitting). An explicit reject/dismiss path (reply `false`) resolves the round cleanly as a definitive refusal.
3. **A failed reply on an invalidated confirmation classifies as an ambiguous order outcome** — ADR-0003's "sent, outcome unknown — reconcile before resubmitting" semantics (reuse or extend the same error family), **never** a definitive refusal and never a generic/transient 503. Recognition is contextual (reply endpoint + 503): the probe pinned that the body carries no marker. This arm still matters after serialization: cross-process invalidation (another session/tool submitting) and post-timeout replies remain reachable.
4. **Every 2xx reply shape classifies.** Empty, whitespace, or non-JSON 2xx reply bodies surface as classified errors carrying the raw body (extending ADR-0003's classification-net rule), never as raw exceptions.

## Alternatives considered

- **Reply-immediately obligation + typed *definitive refusal*, no lock** (the original form of this ADR, pre-probe): falsified by the probe — the "refused" order can still go live, so the recorded consumer response (re-place) double-places; and unserialized windows entangle outcomes across orders. Rejected on evidence.
- **Serialize only, keep the generic 503:** leaves the cross-process/timeout invalidation case surfacing as a transient-looking 503 that invites retrying a dead replyId. Rejected.
- **Ambiguous classification only, no lock:** keeps the entangled-outcome hazard reachable for any concurrent in-process consumer — the probe showed even two sequential placements from different components produce it. Rejected.
- **Status quo** (no lock, generic 503): both hazards. Rejected.

## Consequences

- 📦 **Breaking-behavioral (`feat!:`, operator-decided 2026-07-07):** a second same-account placement now waits (or times out) while a confirmation is pending, where it previously proceeded and silently invalidated it; reply failures surface as ambiguous outcomes instead of generic 503s.
- Outcomes stay attributable: a reply's result is always about its own order; double-place via "refused → re-place" is designed out.
- Cost: a slow confirmation decision now delays subsequent same-account placements up to the confirmation timeout; consumers wanting parallelism must resolve confirmations promptly (suppression via `SuppressMessageIds` remains the throughput path for automated flows).
- The probe also showed question issuance is **non-deterministic** (an identical order got no question in one run, `o354` in the next) — tests must not assume a question always arrives.

## Relationships

Sibling of [ADR-0003](0003-order-post-replay-gate.md) (shares the ambiguous-outcome semantics); design doc §9.10; findings ORD-3 + ORD-1; probe evidence `recordings/order-probe-2026-07-07.log` (local, per the recordings convention); implemented by PVR-06.
