---
name: writing-adrs
description: >-
  Use when recording a cross-cutting architecture decision that outlives a
  single story — an error-classification policy, a delivery/backpressure
  guarantee, a session-lifecycle behavior, a public-surface shape convention,
  or any durable "why we do it this way" the next instance would otherwise
  re-derive or re-litigate. Also use when superseding, revisiting, or weighing
  alternatives for an existing decision, or when a design pass / brainstorm
  reaches a fork worth recording permanently.
---

# Writing ADRs

*Ported from realtest-order-steward @ 758375ab, adapted for a contract-first wrapper library.*

## Overview

An **Architecture Decision Record** captures **one** cross-cutting decision, the alternatives weighed, and its consequences — the durable "why" that would otherwise be re-derived or re-argued. ADRs are **append-only history**: you *supersede* a decision with a new ADR, you never rewrite an accepted one.

They live in `docs/adr/NNNN-short-kebab-title.md`, indexed in `docs/adr/README.md`.

**Adoption posture (this repo): going forward, lean.** ADRs start with the pipeline port — do **not** backfill records for decisions already settled in `docs/ibkr_conduit_design.md`. An ADR is written when a decision is *made or reopened* from now on; the first ADRs are expected to come from decisions the RTOS venue-consumer review (`docs/findings/2026-07-04-rtos-venue-consumer-review.md`) reopened.

## Is it an ADR? (vs. design doc / spec / rule)

| Write a… | when the content is… |
|---|---|
| **ADR** (`docs/adr/`) | a single *cross-cutting decision* + the roads not taken, outliving one story |
| **Design doc** (`docs/ibkr_conduit_design.md`) | the durable *model of the library's contract* — surface, guarantees, upstream behavior (it cites ADRs; see `.claude/rules/contract-design.md`) |
| **Spec** (`docs/superpowers/specs/`) | point-in-time design for one *story / milestone* being built now |
| **Rule** (`.claude/rules/`) | a cross-instance *process / coding convention* |
| **Nothing to decide** (`docs/ibkr-web-api-spec.md` + `recordings/`) | *upstream IBKR behavior* — ground truth is verified, never decided; an ADR may record our interpretation or response to it, never override it |

If the decision only affects one story's implementation, it belongs in that story's spec, not an ADR.

## Procedure

1. **Confirm it's an ADR** using the table above. Story-local → spec instead.
2. **Pick the next number.** Scan `docs/adr/` for the highest `NNNN`; use the next, zero-padded to 4. File: `NNNN-short-kebab-title.md`.
3. **Draft from the template** below. Status starts `Proposed` and moves to `Accepted` once the operator agrees — but if you're only *recording* a decision that's already been made, you may start at `Accepted`. A `Status` may carry a short parenthetical for context, e.g. `Accepted (0.9.0 hardening)`.
4. **Fill "Alternatives considered" honestly** — at least the leading rejected option and *why* it lost. An ADR with no alternatives is usually a spec in disguise.
5. **Add a line to the index** in `docs/adr/README.md` (one line per ADR, mirroring the index's format).
6. **To revisit a decision:** never edit the accepted ADR's Decision/Consequences. Write a *new* ADR, set the old one's Status to `Superseded by ADR-NNNN`, and reference it.

## Template

```markdown
# ADR-NNNN — <Decision title>

**Status:** Proposed | Accepted | Superseded by ADR-NNNN · **Date:** YYYY-MM-DD
**Supersedes:** ADR-NNNN  *(only when this replaces an earlier decision — omit otherwise)*
**Relates to:** <design doc §N / findings ID / captured-spec section>. **Implemented by:** <backlog stream/story or spec>.

## Context
The forces at play: the problem, constraints, and what triggered the decision. Enough that a
reader who wasn't there understands *why a decision was needed* without re-discovering it.
Cite the evidence (recording, findings entry, live probe) when upstream behavior is a force.

## Decision
The choice, stated plainly and imperatively. Number the sub-points if it's multi-part. This is
the load-bearing section — be specific about what is now true.

## Alternatives considered
Each real option that was on the table, and **why it lost**. Include the "do nothing / status quo"
option when relevant. This is what separates an ADR from a spec — record the roads not taken so
they aren't re-walked.

## Consequences
What becomes easier and what becomes harder. Follow-on work this enables or defers (name it, so it
isn't silently dropped). For 📦 public-surface decisions: the semver consequence (breaking vs
additive) and what consumers (RTOS) must change. Honest about the costs the chosen option carries.

## Relationships
Links: the design-doc section(s) this reshapes, the spec(s)/backlog stream(s) that implement it,
the findings entry that triggered it, related ADRs.
```

## Header fields

The pointer fields are best-effort, not required ceremony. **Relates to** links the decision to what it interprets or reshapes — a design-doc section, a findings ID, a captured-spec section; write `n/a` rather than inventing a reference you can't verify. **Implemented by** points at the backlog stream/story or spec that builds it. **Supersedes** appears only when this ADR replaces an earlier one — and when it does, also flip the old ADR's `Status` to `Superseded by ADR-NNNN`.

## Status lifecycle

`Proposed` → `Accepted` → (later) `Superseded by ADR-NNNN`. Append-only: a superseded ADR stays in the tree as history; the superseding ADR explains what changed and why. Recording an already-agreed decision rather than proposing one? It's fine to be born `Accepted`.

## Common mistakes

- **Story-local decision as an ADR.** If it only shapes one story's build, it's spec content. ADRs are *cross-cutting* and *durable*.
- **Rewriting instead of superseding.** Editing an accepted ADR's Decision erases history. Write a new one; flip the old Status.
- **Omitting alternatives.** No "Alternatives considered" → it reads as a fait accompli and gets re-litigated. Record at least one rejected option and why.
- **"Deciding" upstream behavior.** What IBKR sends is ground truth — verify it (recording / live probe), don't vote on it. The ADR records *our response* to it.
- **Backfilling history.** Settled decisions already live in the design doc; retro-ADRs add ceremony without new information. Going forward only.
- **Restating volatile detail.** Link to the implementing spec/backlog/code; don't embed file lists or APIs that will rot.

## See also

- `docs/adr/README.md` — the ADR index + the short conventions statement.
- `.claude/rules/contract-design.md` — where ADRs sit in the contract layer, and the contract-gap routing that produces them.
- `.claude/skills/groom-backlog/` — grooming composes this skill for cross-cutting forks.
