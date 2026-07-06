# IbkrConduit backlog

The pipeline-managed story tracker: `draft-backlog` inserts drafted streams here, `groom-backlog` makes them loop-ready, `ship-backlog` drains them. Format authority: `.claude/rules/backlog-format.md` (entry schema) + `.claude/rules/backlog-status.md` (status hygiene).

> **Scope note:** this tracker starts empty. The **inaugural input** is the [RTOS venue-consumer review](findings/2026-07-04-rtos-venue-consumer-review.md) findings doc — written backlog-workable (stable finding IDs, suggested regression tests) and awaiting a `draft-backlog` pass. The pre-pipeline [money-boundary hardening backlog](money-boundary-hardening-backlog.md) (Stream MBH, id prefix reserved) predates this pipeline and remains its own tracker — it is not migrated here.

## How to read this document

Stories have a stable ID (`<PREFIX>-NN`), a **Status** line, a **Stream**, **dependencies**, a description, a **Done when**, and **TDD notes** — the schema in `.claude/rules/backlog-format.md`.

**Marker in the story heading:**

- **📦 PUBLIC SURFACE** — the story changes the **published API surface or wire-mapping contract** (public types/methods/options, DTO nullability semantics, `[JsonPropertyName]` mappings, streaming frame semantics). A 📦 story gets a semver review at grooming (breaking vs additive → `feat!:` vs `feat:`), lands **before** every story that consumes the new surface, and is reviewed knowing **RTOS (`realtest-order-steward`) is a live consumer** of this library.

**Status values** (updated *in the story's own PR* — the source of truth; see `.claude/rules/backlog-status.md`):

- `Not started` · `In progress — <owner/instance> #<PR>` · `✅ Done — #<PR>` · `Deferred — <reason>`

**Working the backlog (the autonomous loop):**

- One story = one branch = one PR, TDD per `.claude/rules/tdd-workflow.md`.
- Pick any story whose dependencies are all `✅ Done`. Build order per the stream's build-order map.
- Only **loop-ready** entries are buildable (`Spec:` a path or `trivial-skip`, `Risk` set, no open fork, empirics verified); `ship-backlog` bounces the rest to grooming.
- The committed backlog is the single source of truth for status; a PR completing a story flips its Status and carries a `Completes: <id>` trailer.

**Conventions every story inherits** (don't restate per story — read the rules): the contract layer + gap routing (`.claude/rules/contract-design.md`); error surfacing via `Result<T>` + the `IbkrError` taxonomy (`src/IbkrConduit/Errors/`); immutable positional-record DTOs + companion models files (`.claude/rules/design-patterns.md`); rate limiting per `docs/ibkr_conduit_design.md` §8; test tiering incl. the mandatory 401-recovery test and env-gated E2E (`.claude/rules/testing.md`); no global/static mutable state (`.claude/rules/architecture.md`); zero warnings + central package versions (`.claude/rules/build-quality.md`); semver via release-please (`docs/ibkr_conduit_design.md` §17.4).

## Key references (link, don't re-decide)

- **Contract:** `docs/ibkr_conduit_design.md` (the living design doc — canonical) · `docs/adr/` (decisions going forward) · `docs/ibkr-web-api-spec.md` + `recordings/` (upstream ground truth — verified, never decided).
- **Findings:** `docs/findings/` — adversarial reviews of the contract; the evidence trail behind fix streams.
- **Process:** `.claude/skills/draft-backlog/`, `.claude/skills/groom-backlog/`, `.claude/skills/ship-backlog/`, `.claude/skills/writing-adrs/`.

---

## Stories

*(none yet — the first stream lands via `draft-backlog`)*

## Deferred

*(none)*
