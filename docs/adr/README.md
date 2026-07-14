# Architecture Decision Records

Cross-cutting decisions for IbkrConduit — each ADR records **one** decision, the alternatives weighed, and its consequences. Authored via the `writing-adrs` skill (`.claude/skills/writing-adrs/SKILL.md`); their place in the contract layer is defined in `.claude/rules/contract-design.md`.

## Conventions (short form)

- **Going forward, lean — no backfill.** ADRs start with the backlog-pipeline adoption (2026-07-06). Decisions already settled in `docs/ibkr_conduit_design.md` stay there; an ADR is written only when a decision is **made or reopened** from now on. The first ADRs are expected from decisions the [RTOS venue-consumer review](../findings/2026-07-04-rtos-venue-consumer-review.md) reopened (e.g. presence-preserving DTO semantics, the ambiguous-order-outcome error shape, competing-session policy).
- **Append-only.** Never rewrite an accepted ADR — supersede it with a new one and flip the old `Status` to `Superseded by ADR-NNNN`.
- **Upstream behavior is never decided.** What IBKR sends is ground truth (captured spec + `recordings/`); an ADR records our *interpretation or response*, never a vote on the wire.
- Files: `NNNN-short-kebab-title.md`, numbered from `0001`, one line per ADR in the index below.

## Index

| ADR | Title | Status | Date |
|---|---|---|---|
| [0001](0001-nullable-as-presence-wire-fidelity.md) | Nullable-as-presence on wire-optional DTO fields | Accepted | 2026-07-07 |
| [0002](0002-streaming-delivery-guarantee.md) | Streaming delivery guarantee: observable DropOldest, single-observer streams | Accepted | 2026-07-07 |
| [0003](0003-order-post-replay-gate.md) | No automatic 401 replay for order-mutating POSTs; ambiguous-outcome error | Accepted | 2026-07-07 |
| [0004](0004-competing-session-truth-and-health-evidence.md) | Truthful competing-session signaling and health evidence | Accepted | 2026-07-07 |
| [0005](0005-subscription-scoped-streaming-delivery.md) | Subscription-scoped streaming delivery: full-topic-identity routing | Accepted | 2026-07-07 |
| [0006](0006-order-confirmation-window.md) | Order confirmation window: reply-immediately obligation, typed invalidated-confirmation refusal | Accepted | 2026-07-07 |
| [0007](0007-session-path-error-classification.md) | Session-path error classification: uniform HTTP-status taxonomy | Accepted | 2026-07-09 |
| [0008](0008-bracket-per-leg-outcome-classification.md) | Bracket/OCA group submission: per-leg outcome classification | Accepted | 2026-07-13 |
| [0009](0009-positions-trades-cold-read-retry.md) | Positions/Trades cold-read: heuristic auto-retry-once (2026-07-14: Positions only; Trades documented, no retry) | Accepted | 2026-07-13 |
| [0010](0010-bracket-atomicity-no-emulation.md) | Bracket/OCA order atomicity: no client-side emulation | Accepted | 2026-07-13 |
| [0011](0011-cancel-order-no-dead-order-classification.md) | Cancel-order outcome: no typed dead-order classification | Accepted | 2026-07-13 |
