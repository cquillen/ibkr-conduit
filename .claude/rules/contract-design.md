# Contract design — the model layer of a wrapper library

IbkrConduit is a **contract-first wrapper library** over an external API. Its "domain model" is the **contract**: the public surface the library promises, the guarantees behind that surface, and the upstream IBKR behavior those guarantees rest on. This rule defines where the contract is recorded, the grammar a design pass walks, and what counts as a **contract gap** — the pipeline stages (`draft-backlog`, `groom-backlog`, `ship-backlog`) all lean on these definitions.

*Ported from realtest-order-steward @ 758375ab (where the equivalent layer is DDD/event-storming), adapted for a contract-first wrapper library.*

## Where the contract is recorded

| Layer | Location | Authority |
|---|---|---|
| **Living design doc** | `docs/ibkr_conduit_design.md` | Canonical. The durable model of the library's contract; cites ADRs; updated *before* the spec that implements a change to it. |
| **ADRs** | `docs/adr/` (index: `docs/adr/README.md`) | Cross-cutting decisions + roads not taken. Adopted going forward, lean — no historical backfill. See the `writing-adrs` skill. |
| **Specs** | `docs/superpowers/specs/` | Point-in-time design for one story/milestone. Specs *implement* a recorded contract; they never author one. |
| **External ground truth** | `docs/ibkr-web-api-spec.md` + `recordings/` (captured via `tools/ApiCapture/`) + attended live probes | Upstream IBKR behavior. **Never decided, only verified** — an ADR may record our *interpretation or response* to upstream behavior, never override what the wire actually does. |

## The contract grammar

A design pass — whether a full milestone design or a 15-minute check before one story — walks this order; each step constrains the next:

1. **Upstream behavior** — what does IBKR actually do? Captured spec + recording + (when unpinned) a live probe against the paper account. Documented ≠ verified: IBKR's own docs are a claim, not evidence.
2. **Public surface** — the types, methods, and semantics the library exposes for it (DTO shapes, nullability-as-presence, method signatures, options).
3. **Guarantees** — what the surface promises, across the five recurring categories:
   - **error classification** — which `IbkrError` subtype / exception when, hidden-error detection, transient vs permanent;
   - **delivery & backpressure** — streaming completeness, buffer/drop policy, observability of loss;
   - **session lifecycle** — 401 recovery, tickle, re-auth, competing-session behavior;
   - **thread safety** — what may be called concurrently, from where;
   - **disposal** — teardown ordering, cancellation, what survives a dispose.
4. **Consumer obligations** — what the consumer must do for the guarantees to hold (dedup keys, treat `null` as "absent from this frame", dispose subscriptions, single live session, …).
5. **Verification tier** — how each guarantee is pinned: unit / WireMock integration / mock-WS / attended live-paper probe (`.claude/rules/testing.md`).

## Contract gap

A **contract gap** is any of:

- a **public-surface shape** the record doesn't define (a new/changed DTO, method, option, return shape);
- a **guarantee's semantics** the record doesn't state (one of the five categories above);
- an **upstream-behavior interpretation with no recorded answer** (no captured spec entry, no recording, no probe).

**The core discipline: a story spec is never the first place a contract decision is written.** A contract gap routes to a design-doc and/or ADR update **before grooming** — never closed by a story's own spec, never left for the build loop.

## Design pass or straight to spec?

| The change… | → |
|---|---|
| adds/changes a **public-surface shape**, changes a **guarantee's semantics**, reinterprets **upstream behavior**, or **reopens a recorded decision** | **design pass first** (update `docs/ibkr_conduit_design.md` + any ADR, get it reviewed, *then* spec the stories) |
| is a bugfix, refactor, test addition, or internal change that leaves the published surface and guarantees unchanged | **straight to a story spec** — no design pass needed |

When unsure, ask: *"would this change a line in the design doc, or change what a consumer may rely on?"* If yes, it's a design pass — even a small one. Reopened decisions get a *superseding* ADR, never an in-place edit.

## See also

- `.claude/skills/writing-adrs/` — recording the cross-cutting forks the grammar surfaces.
- `.claude/skills/draft-backlog/` (classifies contract gaps), `.claude/skills/groom-backlog/` (closes them or defers), `.claude/skills/ship-backlog/` (builds only after they're closed).
- `.claude/rules/architecture.md` — the standing architectural constraints every design pass inherits.
- `docs/findings/` — adversarial reviews of the contract; findings that reopen decisions feed ADRs.
