# ADR-0002 — Streaming delivery guarantee: observable DropOldest, single-observer streams

**Status:** Accepted · **Date:** 2026-07-07
**Relates to:** findings FIL-1, FIL-3, FIL-4, FIL-5, GAP2-4 (`docs/findings/2026-07-04-rtos-venue-consumer-review.md`); design doc §12.8. **Implemented by:** VCR-02 (spec `docs/superpowers/specs/2026-07-07-vcr-02-streaming-delivery-observability.md`).

## Context

The library never recorded what its streaming surface promises about completeness. In practice: per-subscriber channels use `BoundedChannelFullMode.DropOldest` (capacity 256) with **no observable signal on eviction** (FIL-1, critical — a silent lost fill), reconnects replay subscriptions with no consumer-visible gap marker (FIL-4), a consumer `OnNext` failure is mislabeled as a malformed frame (FIL-3), and a second `Subscribe` on one subscription's `Stream` silently splits deliveries (FIL-5). A money consumer cannot reconcile losses it cannot see.

## Decision

1. **The delivery guarantee is: at-most-once per subscriber, loss-is-observable.** The library may drop frames under overflow, but **never silently**: every eviction emits a Warning log and increments a dropped-frames counter tagged with tenant + wire topic (mapper-drop and observer-failure paths increment the same counter family, labeled by cause, with the *wire topic* — not the DTO type name).
2. **Overflow policy stays `DropOldest`** — a stalled consumer must not back-pressure the single socket receive loop (heartbeats, other topics, and reconnect logic ride it).
3. **The default `StreamingBufferSize` rises 256 → 2048** (operator decision: 256 is too tight for burst replay).
4. **Reconnect/gap transitions are consumer-observable** — the surface exposes connection-lifecycle events (disconnected/reconnected with replayed topics) so a consumer can bound a gap and trigger REST reconciliation immediately instead of inferring from staleness.
5. **`IIbkrSubscription<T>.Stream` is single-observer:** a second concurrent `Subscribe` throws `InvalidOperationException`, and the constraint is documented on the member. An observer exception must not masquerade as a malformed wire frame, and an `OperationCanceledException` from the observer must not read as graceful completion.

## Alternatives considered

- **`BoundedChannelFullMode.Wait` on money topics (`str`/`sor`):** zero loss, but a stalled/deadlocked consumer wedges the shared receive loop — heartbeat, tickle-driven reconnect, and every other topic stall behind it. A silent wedge is worse than an observable drop. Rejected.
- **Per-topic configurable `FullMode`:** ships the wedge risk as a consumer-reachable configuration and doubles the test matrix. Rejected.
- **True multicast `Stream`** (per-observer buffers): matches `IObservable` expectations but adds per-observer backpressure semantics for a pattern no known consumer uses. Rejected in favor of the honest guard.
- **Status quo (silent DropOldest):** the review's single worst finding; unobservable money loss. Rejected.

## Consequences

- 📦 **Breaking-behavioral** (`feat!:`): a second `Subscribe` now throws where it previously half-worked; the buffer default changes; new observability surface (counter, lifecycle events) is additive.
- Consumers get a deterministic trigger for REST reconciliation (drop signal / reconnect event) instead of polling staleness.
- Cost: drops remain possible by design — consumers on hot topics must size buffers and wire the reconciliation trigger; the guarantee is honest, not lossless.

## Relationships

Design doc §12.8 (new); findings FIL-1/FIL-3/FIL-4/FIL-5/GAP2-4; implemented by VCR-02 (observable surface) with mapper-level robustness in VCR-03; ADR-0001 governs the DTO shapes the frames deserialize into.
