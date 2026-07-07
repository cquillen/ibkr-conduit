# ADR-0001 — Nullable-as-presence on wire-optional DTO fields

**Status:** Accepted · **Date:** 2026-07-07
**Relates to:** findings WIR-1, WIR-3, FIL-6, GAP2-2, GAP2-3 (`docs/findings/2026-07-04-rtos-venue-consumer-review.md`); design doc §6.5. **Implemented by:** VCR-01 (spec `docs/superpowers/specs/2026-07-07-vcr-01-presence-preserving-dtos.md`), shapes VCR-07.

## Context

IBKR's wire shapes are sparse and inconsistently typed: `sor` frames are deltas that omit fields wholesale (live-captured sparse frame in `OrderUpdateMapperTests`), REST rows send money fields as empty strings (`"price": ""` on a filled market order — live capture 2026-07-07, `recordings/priming/003-GET-iserver-account-orders.json` (local; carried into fixtures by VCR-01)), and status frames may omit boolean verdicts entirely. Several public DTOs erase that presence information: non-nullable `decimal`/`int`/`bool`/`string.Empty` fields turn "IBKR sent nothing" into `0` / `false` / `""` — values indistinguishable from real data. The venue-consumer review rated this the money-corruption path (WIR-1 critical: a sparse-delta merge regresses a partially-filled order to unfilled). The library already uses the correct pattern in places (`OrderUpdate.Price`, `SystemEvent.IsPaper` are nullable) — the contract was simply never recorded, so new DTOs kept guessing.

## Decision

1. **On every public DTO, a wire-optional field is nullable, and `null` means exactly "not present in (or not parseable from) this frame/row."** This covers streaming DTOs (`OrderUpdate`, `SessionStatusEvent`, `AccountStatusEvent`, `TradeExecution`) and REST DTOs (`Trade`, `LiveOrder`, …) alike.
2. **The empty-string→`0` coercion for non-nullable numerics is reserved for genuinely non-optional counters.** Money, quantity, and status/verdict fields are never non-nullable-with-default when the wire can omit them.
3. **No DTO fabricates a verdict from absence** — an absent `authenticated`/`isPaper` maps to `null`, never to `false`.
4. This is the standing rule for all future DTO additions, not just the VCR-01 retrofit.

## Alternatives considered

- **Presence-set sidecar** (keep field types, add a set of present field names): non-breaking-ish, but a novel pattern no .NET consumer expects, clumsy at every call site, and it still changes the records. Rejected.
- **Raw `JsonElement` access** (consumer checks presence): pushes the wire format onto every consumer and leaves the corrupt defaults in place. Rejected.
- **Document-only / status quo:** keeps WIR-1 unfixable consumer-side — a genuine `RemainingQuantity=0` (fully filled) is indistinguishable from an omitted field, so a correct sparse-delta merge is impossible over the surface. Rejected.

## Consequences

- 📦 **Breaking** (`feat!:`): consumers must handle `null` on fields that previously lied with defaults. RTOS's sparse-merge rule ("non-null frame fields overwrite; null never erases") becomes implementable exactly as specified.
- Consumers that treated `0`/`""` as absent (the workaround heuristic in the OrderMonitor example) can drop the heuristic.
- Cost: nullable ceremony on consumer code paths that read these fields; a one-time RTOS re-pin against the breaking release.
- Follow-on named: codify the rule as `.claude/rules/` material (the wire-mapping rules follow-up from PR #235).

## Relationships

Design doc §6.5 (new — records this contract); findings WIR-1/WIR-3/FIL-6/GAP2-2/GAP2-3; live evidence `recordings/priming/`; implemented by VCR-01; `SessionStatusEvent` additions in VCR-07 follow this rule.
