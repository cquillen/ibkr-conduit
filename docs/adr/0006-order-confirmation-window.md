# ADR-0006 — Order confirmation window: reply-immediately obligation, typed invalidated-confirmation refusal

**Status:** Accepted · **Date:** 2026-07-07
**Relates to:** ADR-0003 (order-outcome trichotomy — sibling decision); design doc §9.1/§9.9/§9.10; finding ORD-3 (`docs/findings/2026-07-07-multi-agent-code-review.md`); captured spec `docs/ibkr-web-api-spec.md` (reply invalidation, ~4559). **Implemented by:** PVR-06 (`docs/backlog.md`, Stream PVR).

## Context

IBKR's question/reply flow leaves a window: after a placement returns a confirmation question and before the reply is sent, **any subsequent order submission on the session invalidates the pending confirmation** — the reply then fails server-side (the captured spec pins a 503). The library's per-account order semaphore serializes individual calls but is released between the placement and the reply, so a concurrent placement (another thread, another component of the same consumer) can invalidate a confirmation mid-round. Today that surfaces as a generic 503 — transient-looking, inviting a retry of a dead `replyId` — and the record documents neither the window nor what a consumer should do about it. Separately (ORD-1), a 2xx reply body that is empty/whitespace/non-JSON escapes classification as a raw `InvalidOperationException`, violating the §9.9 rule that every 2xx order-path response classifies.

## Decision

1. **Reply-immediately is a documented consumer obligation.** A pending confirmation must be resolved (replied to) before the next order submission on the same account. The obligation is documented on `PlaceOrderAsync`, `ReplyAsync`, and the confirmation-required surface. The library does **not** hold the per-account order lock across the question/reply round — concurrent placement remains possible, and its consequence is surfaced, not prevented.
2. **An invalidated-confirmation reply classifies as a typed, definitive refusal** — a distinct error identifying the invalidated confirmation, whose recorded consumer response is *re-place from scratch*. It is never surfaced as a generic or transient failure.
3. **Every 2xx reply shape classifies.** Empty, whitespace, or non-JSON 2xx reply bodies surface as classified errors carrying the raw body (extending ADR-0003's classification-net rule to the reply path's non-JSON shapes), never as raw exceptions.

## Alternatives considered

- **Hold the per-account lock across the round** (auto-reply option or a scoped confirmation handle): makes in-process invalidation structurally impossible, but blocks all concurrent placements on the account for the duration of a human/async confirmation decision, adds public surface, and needs timeout/deadlock semantics. Rejected for now — may be revisited later as an opt-in mode in a superseding/extending ADR if a consumer needs it; nothing in this decision precludes it.
- **Status quo** (undocumented window, generic 503): a consumer retries a dead `replyId` or misreads a definitive refusal as transient — an order stalls on the money path with a misleading signal. Rejected.

## Consequences

- Consumers get a deterministic signal (typed refusal → re-place) instead of a misleading transient error; the window itself remains — by design, the library surfaces it rather than serializing all order flow around it.
- The obligation is documentation + a new error type (additive-leaning; semver reviewed at grooming per the 📦 rules).
- Cross-process invalidation (a second session/tool submitting orders) is inherently out of the library's control — the typed refusal covers it identically.
- Defers: an opt-in held-lock/auto-reply mode, if ever needed (named here so it isn't silently dropped).

## Relationships

Sibling of [ADR-0003](0003-order-post-replay-gate.md) (both shape the order path's failure semantics); design doc §9.10 (new); finding ORD-3 (+ ORD-1's classification-net gap folded into the same story); implemented by PVR-06.
