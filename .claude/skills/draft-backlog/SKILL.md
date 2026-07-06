---
name: draft-backlog
description: >-
  Use when a finished design must become a drafted backlog stream — a design-doc
  update + ADRs (or a backlog-workable findings doc) needs decomposing into
  story entries that grooming can then make loop-ready. Triggers: "turn this design
  into stories," "draft the backlog for X," "decompose this milestone," promoting a
  Deferred item into a buildable stream, a merged ADR with no stories yet, a
  findings review that needs a fix stream.
---

# Draft backlog

*Ported from realtest-order-steward @ 758375ab, adapted for a contract-first wrapper library.*

## Overview

Turn a **recorded contract** (design doc + ADRs + captured spec/recordings — `.claude/rules/contract-design.md`) into a **drafted** backlog stream (`.claude/rules/backlog-format.md`) — the bridge between the contract record and the attended grooming stage (`groom-backlog`). Drafting is the **cheap, faithful** stage: it decomposes and orders; it decides nothing. Two disciplines the ambient rules do not supply:

1. **Decompose the recorded contract only — and stay in your stage.** Every claim in an entry traces to the design doc, an ADR, the captured spec/recordings, or merged code. A gap in the record is *flagged*, never filled; a field grooming owns is *left empty*, never "provisionally" set.
2. **Insert a stream, not a snippet.** A drafted stream lands with its whole backlog kept true: the Deferred section reconciled, the build-order map updated, ids collision-checked, cross-stream `Blocks:` lines maintained.

**Foundational rule:** violating the letter of these is violating the spirit. "Provisional — grooming can confirm," "the story's spec will close it," and "refined now saves grooming time" are exactly the pressures this skill exists to resist.

## When to use

- A design artifact set is finished (design-doc update + ADRs merged) and needs stories.
- A backlog-workable findings doc (`docs/findings/`) needs decomposing into a fix stream.
- A `Deferred` backlog item is being promoted into a buildable stream.

**Not** for: making entries loop-ready (that is `groom-backlog` — specs, `Risk`, closing forks); designing the contract itself (that is a design pass per `.claude/rules/contract-design.md` — run it *first* if the contract isn't recorded); building anything (`ship-backlog`).

## The stage contract — what a drafted entry owns (and what it must not touch)

| Field | Drafted state | Owner |
|---|---|---|
| `#### <id> — <title>` + 📦 marker | set | **drafting** |
| `Status` | `Not started` | drafting |
| `Depends on` | parseable id list (or `none`), deps on **merged** work by real id | **drafting** |
| `Blocks` | convenience list, consistent with others' `Depends on` | drafting |
| Prose description | scoped to the recorded contract; cites its sources | **drafting** |
| `Done when` | **rough** — the observable outcome in 1–2 sentences | drafting (grooming refines) |
| `Spec` | the literal `**Spec:** pending` line on **every entry** — a stream-banner claim ("all entries pending") does not satisfy the field; grooming and ship-backlog parse per-entry fields, not prose | drafting sets `pending`; grooming replaces |
| `Risk` | **absent** — do not set, not even "provisionally" | **grooming** (from the spec) |
| `TDD notes` | absent (or a one-line hint at most) | grooming |
| Open questions | flagged inline, classified (below) | drafting flags; grooming closes |

**Why `Risk` stays empty:** it is decided *from the spec*, which doesn't exist yet. A provisional value anchors the groomer (they inherit the guess instead of judging), and it makes a drafted entry masquerade as half-loop-ready. The same logic bans loop-ready-grade `Done when` criteria: refined acceptance encodes decisions, and drafting has no authority to make them.

## Classify every gap before writing it down

While decomposing, every "the record doesn't say" moment gets classified — this is the load-bearing judgment of the stage:

```
gap in the record
     │
     ├─ A CONTRACT gap? (a public-surface shape, a guarantee's semantics —
     │  error classification, delivery/backpressure, session lifecycle,
     │  thread-safety, disposal — or an upstream-behavior interpretation
     │  with no recorded answer; see contract-design.md)
     │        └─ YES ─▶ ROUTE TO DESIGN: name it in the stream preamble as
     │                  "needs a design-doc/ADR update BEFORE grooming" and mark the
     │                  affected stories' dependence on it. NEVER write "the story's
     │                  spec will close it" — a story spec must never be the first
     │                  place a contract decision is written (contract-design.md).
     │
     ├─ A product/priority/scope fork, or an empirical unknown, below contract level?
     │        └─ YES ─▶ FLAG FOR GROOMING: an inline open-question on the entry.
     │                  Do not recommend an answer; do not resolve it.
     │
     └─ Already answered by a rule / ADR / design doc / captured spec / merged code?
              └─ YES ─▶ CITE IT — the entry states the answer with its source.
                        Not a gap; never re-open it.
```

**Never silently supplement the record.** An invented specific ("nullable with a presence set", "throttled to first-drop-per-topic") reads as authoritative to every downstream stage. If the record doesn't say it, either don't say it or flag it.

## Decomposition disciplines

- **Slice by infra pattern, fewer-larger**, sized **one story = one PR** (`.claude/rules/workflow.md`). A story spanning a 📦 public-surface change AND a session-lifecycle behavior AND an example app AND a docs overhaul is too big — split at drafting so grooming doesn't have to.
- **📦-first ordering:** a story that lands/changes a **published API surface or wire-mapping contract** is marked 📦 and ordered **before** every story that consumes that surface. 📦 stories carry the semver stakes (breaking vs additive — decided at grooming) and are reviewed knowing RTOS is a live consumer.
- **DAG honesty:** `Depends on` names only real, merged story ids (verify against the backlog's Status lines and `git log`, not memory) plus in-stream predecessors. Check the claimed reuse actually exists in `main` before depending on it.
- **Id hygiene:** pick a stream prefix that collides with nothing in any `docs/*-backlog.md` (grep first — `MBH` is taken); number from 01.

## Whole-backlog insertion (the part everyone forgets)

A stream insertion is complete only when the **surrounding backlog is still true**:

1. The stream section, with a short preamble: what design/findings it decomposes (cited), the drafted-not-groomed banner, the build-order sketch, and any **route-to-design items** (above).
2. **Reconcile the Deferred section** — if the stream promotes a Deferred bullet, flip that bullet to point at the stream and restate what *remains* deferred out of it. An inserted stream beside an untouched "this is deferred" bullet is a self-contradiction.
3. **Update the build-order map** (the backlog's parallelism note) with the new stream's ordering, superseding-not-deleting the prior version (keep the old map marked historical).
4. Cross-stream `Blocks:` lines on other entries the new stories block, if any.
5. Land it per the repo's PR conventions (a `docs:` PR; drafting produces no `Completes:` trailer — it completes nothing).

## Hand off

The output satisfies `backlog-format.md` §"drafted": every entry `Spec: pending`, no `Risk`, rough `Done when`, gaps classified and flagged. Tell the operator what needs a **design-doc/ADR update before grooming** (if anything) and hand the stream to `groom-backlog`. A drafted stream is *not* buildable — `ship-backlog`'s pre-flight will (correctly) bounce it until groomed.

## Red flags — STOP

- Writing **`Risk:`** on a drafted entry — even "provisional," even with a "grooming should confirm" note.
- Writing **"the story's spec will close this"** about a *contract-level* gap (public-surface shape, guarantee semantics, upstream-behavior interpretation) instead of routing it to a design-doc/ADR update.
- `Done when` that reads like acceptance criteria (invariants, "byte-for-byte", named test expectations) instead of a rough observable outcome.
- A specific mechanism in the prose that **no ADR/design doc/recording states** and no flag marks as open.
- Inserting the stream while the **Deferred section still claims the work is deferred**, or without touching the build-order map.
- `Depends on` naming a story you didn't verify as merged, or a mechanism you didn't verify exists on `main`.
- An entry missing its `**Spec:** pending` line — declaring it once in the stream banner instead of on each entry. *(Observed in RTOS baseline testing.)*
- Closing any fork yourself, or asking the operator questions mid-draft — drafting neither decides nor escalates; it flags (grooming is the attended stage).

## Rationalizations — and the reality

| Rationalization | Reality |
|---|---|
| "I'll set `Risk` provisionally — grooming can confirm." | Grooming sets `Risk` **from the spec**. A provisional value anchors the groomer and dresses a drafted entry as half-loop-ready. Leave it absent. *(Observed verbatim in RTOS baseline testing.)* |
| "This fork is genuinely open — the story's own spec must close it." | If it's a **contract** gap (surface shape / guarantee semantics / upstream interpretation), a story spec is the *wrong place* to close it — route it to a design-doc/ADR update in the stream preamble. Specs implement a recorded contract; they never author one. |
| "A refined `Done when` now saves grooming effort later." | It spends drafting effort encoding decisions drafting has no authority to make, and grooming redoes it anyway once the spec exists. Rough is the contract. |
| "The record doesn't say, but the obvious mechanism is X — I'll just write X." | An invented specific reads as authoritative downstream. Cite it, flag it, or omit it. |
| "The Deferred bullet / build-order map is someone else's cleanup." | The insertion isn't done until the backlog is self-consistent. A stream beside a stale "deferred" claim ships a contradiction. |
| "I remember CDT-05 shipped the mechanism this reuses." | Verify against the backlog Status + `main` before writing the dependency. Memory drifts; `Depends on` is parsed by machines. |

## See also

- `.claude/rules/backlog-format.md` — the drafted-entry schema this produces (and the loop-ready contract it deliberately does **not** reach).
- `.claude/rules/contract-design.md` — the contract layer this decomposes, and where routed contract gaps go (design doc + `writing-adrs`).
- `.claude/skills/groom-backlog/` — the downstream attended stage (closes what this flags); `.claude/skills/ship-backlog/` — the build loop two stages down.
- `.claude/rules/workflow.md` — the one-story-per-PR sizing discipline.
