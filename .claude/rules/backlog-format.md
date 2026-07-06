# Backlog entry format — the loop-ready story schema

The canonical shape of a backlog story entry. This rule owns the **entry shape**; its companion `.claude/rules/backlog-status.md` owns the **status hygiene** (the `Status`-flip + `Completes:` trailer convention). Together they let humans *and* the autonomous build skills (`groom-backlog` / `ship-backlog`) produce and parse the backlog (`docs/backlog.md` and any future `docs/*-backlog.md`) identically — no drift.

*Ported from realtest-order-steward @ 758375ab, adapted for a contract-first wrapper library. This format is adopted with the pipeline — the pre-pipeline `docs/money-boundary-hardening-backlog.md` predates it and is not migrated.*

## The entry

```markdown
#### CDT-03 — 📦 Presence-preserving streaming order DTO
**Status:** Not started · **Stream:** CDT · **Depends on:** CDT-01 · **Blocks:** CDT-07
**Risk:** high
**Spec:** docs/superpowers/specs/2026-07-10-cdt-03-presence-preserving-dto-design.md
Prose description of the story…
**Done when:** <observable acceptance criteria>.
**TDD notes:** <test-shaping hints>.
```

- **Heading** — `#### <id> — <title>`. Optional marker: **📦** (the story changes the **published API surface or wire-mapping contract** — a public type/method/option/return shape, DTO nullability semantics, `[JsonPropertyName]` mapping, streaming frame semantics). A 📦 story requires a **semver review** at grooming (breaking vs additive — the squash-merge subject carries it: `feat!:` vs `feat:`, see `docs/ibkr_conduit_design.md` §17.4), must land **before** every story that consumes the new surface, and is reviewed knowing **RTOS (`realtest-order-steward`) is a live consumer** of this library.
- The `**Status:** … · **Depends on:** …` line, then `**Risk:**`, `**Spec:**`, the description, `**Done when:**`, `**TDD notes:**`.

## Fields

| Field | Required | Values / form | Set by |
|---|---|---|---|
| `id` (in heading) | yes | stable unique, e.g. `CDT-03` | drafting |
| **`Status`** | yes | one of the **closed set** below | drafting → build PR / sweep |
| **`Depends on`** | yes | comma-separated **id list**, or `none` | drafting |
| **`Risk`** | for loop-ready | `standard` (default if absent) \| `high` | grooming (from the spec) |
| **`Spec`** | for loop-ready | a path \| `trivial-skip` \| `pending` | drafting (`pending`) → grooming (path/`trivial-skip`) |
| **`Done when`** | yes | observable acceptance criteria | drafting (sketch) → grooming (refined) |
| `Stream` / `Blocks` | optional | grouping label / convenience id list (derivable from others' `Depends on`) | drafting |
| `TDD notes` | optional | test-shaping hints | grooming |

### `Status` — closed set (the only admissible values)

- `Not started`
- `In progress — <owner/instance> #<PR>`
- `✅ Done — #<PR>` *(set in the story's own PR — `backlog-status.md`)*
- `Deferred — <reason>` *(set by `ship-backlog`'s sweep when a story can't converge, or by `groom-backlog` when a fork can't be closed; carries the follow-on reference)*

### `Depends on` parsing

A comma-separated list of story ids the story needs **merged** before it can build (the DAG edges), or the literal `none`. The slash shorthand `CDT-10/11` means `CDT-10, CDT-11` (a shared-prefix group) — expand it when parsing. Examples: `none` · `CDT-01` · `CDT-01, CDT-03` · `CDT-03, CDT-05, CDT-07`.

### `Spec`

- `pending` — drafted but not yet specced (a *drafted-backlog* state; **not** loop-ready).
- a repo-relative path to a merged spec under `docs/superpowers/specs/`.
- `trivial-skip` — deliberately spec-less (a change small enough to skip the ceremony; the entry's `Done when` is the contract).

### `Risk`

- `standard` (the default — omit the line or write `standard`).
- `high` — the story touches **order placement/modification**, **auth/signing**, **credential handling**, or **delivery semantics** (streaming completeness/backpressure/fill delivery) — the surfaces where a defect loses money or corrupts a consumer's session, and where the RTOS venue-consumer review found the critical defects. **Set by grooming from the spec** (decided when the design is known, not guessed post-hoc); `ship-backlog` reads it to scale review rigor.

## Loop-ready vs drafted

- A **drafted** entry has `id` · `Status: Not started` · `Depends on` · a rough `Done when` · `Spec: pending`. (`draft-backlog`'s output.)
- A **loop-ready** entry additionally has a **merged `Spec`** (path or `trivial-skip`, not `pending`) · `Risk` set · `Done when` refined · all open questions closed in the spec · **every upstream-IBKR behavior it builds on verified** (a recording or attended live probe — `.claude/rules/contract-design.md`; documented-but-unverified is not loop-ready). (`groom-backlog`'s output; `ship-backlog`'s pre-flight requires it.)

`ship-backlog` **bounces** any entry that isn't loop-ready (defers it, flags for grooming) rather than guessing.

## See also

- `.claude/rules/backlog-status.md` — the `Status`-flip + `Completes:` trailer convention (the companion).
- `.claude/rules/contract-design.md` — the contract layer + contract-gap definition drafting and grooming route against.
- `.claude/skills/groom-backlog/` (produces loop-ready entries), `.claude/skills/ship-backlog/` (parses + builds them), `.claude/skills/draft-backlog/` (produces drafted entries).
- `docs/backlog.md` "How to read this document" — the in-tracker statement of these same conventions.
