# ADR-0003 — No automatic 401 replay for order-mutating POSTs; ambiguous-outcome error

**Status:** Accepted · **Date:** 2026-07-07
**Relates to:** findings AMB-2, AMB-3, AMB-4, WIR-4 (`docs/findings/2026-07-04-rtos-venue-consumer-review.md`); design doc §9.9. **Implemented by:** VCR-04 (spec `docs/superpowers/specs/2026-07-07-vcr-04-order-outcome-replay-gate.md`).

## Context

`TokenRefreshHandler` buffers every request body and, after re-authenticating on a 401, unconditionally re-sends it — including `POST /iserver/account/{id}/orders`, order modify, and `POST /iserver/reply` (AMB-2). Whether IBKR can *process* an order POST and then return 401 is **unpinned in either direction** — no recording exists and the condition cannot be induced on demand (it may also arise from a middlebox). If it can happen, the library itself double-submits an order: the worst outcome for a money consumer. A venue consumer needs the order-outcome trichotomy — definitively transmitted / definitively refused / ambiguous — but the library records no such classification and today collapses the ambiguous leg into either a silent replay or a context-free exception.

## Decision

1. **Order-mutating POSTs (place, modify, reply) are excluded from automatic 401 replay.** Re-authentication still happens, but the original call is not re-sent.
2. **The excluded call surfaces a dedicated ambiguous-outcome error** — a new `IbkrError` shape whose meaning is "the request was sent; the outcome is unknown; reconcile before resubmitting." It carries what the handler knows (endpoint, status, whether re-auth succeeded).
3. **Idempotent requests (GET, DELETE cancel) keep today's replay behavior.**
4. **The ambiguity is tolerated, not resolved:** this design is safe under *both* answers to the unpinned process-then-401 question, so the stream does not block on pinning it. A future live observation may settle it; nothing in this decision depends on the answer.
5. Every 2xx order-path response classifies through the same machinery: reply responses route through `ResultFactory.FromResponse` hidden-error detection like every other order path (AMB-3), and unrecognized-but-plausible 200 shapes (array-wrapped reject, empty array) classify as refusals carrying the raw body instead of throwing context-free (AMB-4). A 2xx body that fails typed deserialization surfaces as a classified error type, never as a raw exception (WIR-4's converter fix rides the existing wire-mapping convention).

## Alternatives considered

- **Opt-in replay flag** (re-enable order-POST replay for consumers with client-side order-ID discipline): cOID dedup makes replay *safer*, not safe — and it's a footgun when enabled without that discipline. Rejected; can be revisited by a superseding ADR if a consumer demonstrates the need.
- **Keep replay + document (status quo):** leaves the library as a potential duplicate-order source; a doc warning does not help the consumer distinguish the replayed case. Rejected.
- **Block until the empirical question is pinned:** the condition can't be induced; waiting gates a critical fix on evidence that may never arrive. Rejected in favor of a design safe under both answers.

## Consequences

- 📦 **Breaking-behavioral** (`feat!:`): consumers that silently benefited from order-POST replay now receive the ambiguous error and must reconcile (the safe direction). New public error shape to map.
- The order-outcome trichotomy becomes a recorded, implementable contract; refusal reasons survive to the consumer.
- Cost: a 401'd order call that IBKR in fact did *not* process now needs one consumer-side reconcile round-trip that the old replay skipped.

## Relationships

Design doc §9.9 (new); findings AMB-2/AMB-3/AMB-4/WIR-4; implemented by VCR-04; error taxonomy `src/IbkrConduit/Errors/IbkrError.cs`; ADR-0004 covers the session-error side of the same taxonomy.
