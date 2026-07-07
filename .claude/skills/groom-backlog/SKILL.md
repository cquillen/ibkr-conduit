---
name: groom-backlog
description: >-
  Use when a drafted or rough backlog of stories must be made ready to build —
  entries exist but carry open questions, `Spec: pending`, or no `Risk`, and the
  stream must reach a loop-ready state before an autonomous build. Triggers:
  "make this backlog ready to build," "groom these stories," "get this stream
  loop-ready," refining a drafted backlog, closing out a stream's open questions,
  prepping stories for ship-backlog / an unattended loop.
---

# Groom backlog

*Ported from realtest-order-steward @ 758375ab, adapted for a contract-first wrapper library.*

## Overview

Turn a **drafted** backlog (`.claude/rules/backlog-format.md`: `Spec: pending`, no `Risk`, open questions) into a **loop-ready** one — every story specced (or `trivial-skip`), `Risk` set, **every open question closed**, **every upstream-behavior dependency verified** — so the unattended `ship-backlog` loop never meets an unresolved fork. Grooming is the **attended** stage: the operator is in the loop *here*, on purpose, so the build stage can be hands-off.

Two disciplines the ambient project rules do **not** supply:

1. **Classify every open question, then close it the right way.** Three kinds, three closures: **rule-settled** → apply the rule, don't ask · **operator-decision** → ask the operator *now*, don't decide it yourself or defer it · **empirical blocker** → get the evidence (live-probe the paper account) or `Deferred` the story, don't guess.
2. **Close every fork before handoff.** A spec you produce has **no** "implementer's call," no `TBD`, no unresolved option, no "operator will confirm at review." Grooming is where the fork dies — not the spec, not the build.

**Foundational rule:** violating the letter of these is violating the spirit. "Recommend it and let review confirm," "the implementer can decide," "asking is slower than guessing" are exactly the pressures this skill exists to resist. When tempted to leave a fork open — classify it and close it.

## When to use

- A drafted/rough backlog of stories that must be readied before an unattended build (`ship-backlog`).
- "Make this backlog ready to build / groom this stream / close these open questions / get it loop-ready."

**Not** for: decomposing a milestone into stories in the first place (that is the upstream `draft-backlog`); **building** the stories (that is `ship-backlog`); a single story you are hand-building now (just follow the repo's per-story brainstorm→spec workflow directly).

## The open-question sweep (load-bearing)

**Sweep the whole stream first, not story-by-story.** Surface *every* open question across all stories before resolving any — a fork in one story often settles or reshapes another, and you want one batched operator interaction, not a drip of them. Then classify each question and close it:

```
open question
     │
     ├─ A `.claude/rules/` file / ADR / design doc / captured spec / merged code already decides it?
     │        └─ YES ─▶ RULE-SETTLED: apply the decision, record it in the entry/spec.
     │                  Do NOT ask the operator.
     │
     ├─ A genuine product/design fork — no rule, ≥2 legitimate answers, operator-visible?
     │        └─ YES ─▶ OPERATOR-DECISION: ask NOW via AskUserQuestion (batch all of them,
     │                  each WITH your recommendation). Record the chosen answer as decided.
     │                  Do NOT pick it silently. Do NOT defer it to a downstream review.
     │
     └─ Rests on upstream IBKR behavior no recording or live capture confirms?
              └─ YES ─▶ EMPIRICAL BLOCKER: live-probe the paper account NOW (attended,
                        same-day) and commit the evidence, or mark the story
                        `Deferred — <reason>` with the exact unblock written out.
                        Do NOT guess. Do NOT build on documented-but-unverified behavior.
```

**Rule-settled — apply, don't ask.** If the record already decides it, the answer is made; asking the operator a settled question wastes their attention and invites drift. Examples that recur as fake "open questions" in this repo:

- **How errors surface to consumers** — the `Result<T>` + `IbkrError` taxonomy (`src/IbkrConduit/Errors/IbkrError.cs`, `ResultFactory.cs`; pattern-match the subtypes); IBKR's 200-OK-error and 500-misuse patterns are catalogued in `docs/ibkr-error-patterns-report.md`.
- **DTO shape** — immutable positional records, co-located `I{InterfaceName}Models.cs` (`.claude/rules/design-patterns.md`).
- **Tolerant numeric/string wire parsing** — the registered converters (`src/IbkrConduit/Serialization/EmptyTolerantNumberConverters.cs`, `FlexibleStringJsonConverter.cs`, wired in `IbkrRefitSettings.cs`).
- **Rate limiting** — wait-not-fail token buckets, per-tenant lifecycle, 429 adaptation (`docs/ibkr_conduit_design.md` §8).
- **Test tiering** — Shouldly, WireMock via the full DI stack, the **mandatory 401-recovery test per integration-tested endpoint**, `[EnvironmentFact]` E2E through the DI pipeline, `[Collection("IBKR E2E")]` (`.claude/rules/testing.md`).
- **Recording validation** — cassettes from real sessions as living documentation (`docs/ibkr_conduit_design.md` §14.2; `recordings/` captured via `tools/ApiCapture/`).
- **Session lifecycle** — init/tickle/re-auth/question-suppression (`docs/ibkr_conduit_design.md` §7).
- **No global/static mutable state; storage-agnostic credentials** (`.claude/rules/architecture.md`).
- **Versioning** — SemVer, pre-1.0 release-please mapping (`docs/ibkr_conduit_design.md` §17.4, `release-please-config.json`).

Resolve and record; move on.

**Operator-decision — ask now, close now.** A genuine fork with no governing rule is the **one** thing you escalate during grooming — that is what the attended stage is *for*. Batch every such fork into one `AskUserQuestion` call, each option with your recommendation and its trade-off. **Every 📦 story's breaking-vs-additive semver call is made here** (it shapes the release train and RTOS's re-pin; record it in the spec and the planned commit subject — `feat!:` vs `feat:`). Record the operator's answer as a **decided** fact and bake it into the spec. **Do not** substitute your own pick + "the operator will rubber-stamp it at PR review" — that leaves the fork open in the handed-off backlog, defeating the loop-ready contract. **Do not** silently guess to save a round-trip.

If the operator is **unavailable** (asleep, mid-deadline, unreachable), an operator-fork still cannot be closed by you — so mark that story `Deferred — operator decision required`, pre-stage the exact batched ask for when they return, and **defer its dependents**. An unavailable operator does **not** downgrade a fork to "I'll decide it" or "the implementer will judge it at build time" — it just means that story waits. Never push a story carrying an unclosed operator-fork into the unattended loop, and never freeze a guess to hit a clock.

**Empirical blocker — evidence or defer (this repo's discipline).** **Documented ≠ verified.** IBKR's docs — live (via the `scout-ibkr-docs` skill against `docs/ibkr-doc-sources.md`, snapshotted into `docs/ibkr-doc-evidence/`) or the deprecated local mirrors — record *claims about* upstream behavior; a story that **builds on** an upstream behavior no recording or live observation confirms is **not loop-ready**. Close it during grooming, while attended (scout the live docs first — `scout-ibkr-docs` reconciles sources and stages probe hypotheses; its evidence files are what the spec cites for the claim side):

- **Live-probe the paper account, same-day:** run `tools/ApiCapture/` for the endpoint(s) in question (recordings land in `recordings/`), dispatch the `ibkr-live-probe` agent (serial; mutations need your per-probe ack), or an `[EnvironmentFact]` E2E / example app against the paper credentials. Commit the recording/observation as the evidence the spec cites.
- **Can't probe it** (market closed for the behavior in question, endpoint needs state you can't create, behavior is timing-dependent)? Mark the story `Deferred — needs live capture of <exact behavior>`, write the precise unblock, and **leave its dependents deferred too**.

This arm exists because the unattended loop **never touches the live account** (`ship-backlog`'s merge gate is offline-only) — grooming is the *last* chance to pin empirics before the loop runs. Do not paper a blocker over by `trivial-skip` or by building on an unverified assumption.

## Right-size while grooming

A drafted story that is secretly **multi-subsystem** (touches a 📦 public surface AND session lifecycle AND an example app AND docs) is too big — **split it**, one story per PR (`.claude/rules/workflow.md`). Group by infra pattern, not per-endpoint. Correct over-broad `Depends on` while you split (a child often needs fewer deps than the bundle claimed). A story too small to stand alone folds into its neighbor.

## Spec-or-not — and bake the decisions in

Per unit, decide what it needs and author it, **carrying the closed decisions in** so the spec states each answer as decided:

| Unit needs | Use | Output |
|---|---|---|
| A design worth review | `superpowers:brainstorming` → a spec | `Spec:` path under `docs/superpowers/specs/` |
| A cross-cutting decision (surface/guarantee/upstream-interpretation) | `writing-adrs` | ADR (+ spec cites it) |
| A change to the library's recorded contract | a design pass per `.claude/rules/contract-design.md` | updated `docs/ibkr_conduit_design.md` (+ ADRs) |
| A pattern-following change too small for ceremony | `trivial-skip` | `Spec: trivial-skip` (the `Done when` is the contract) |

Grooming is the **stream-level wrapper** over the repo's per-story brainstorm→spec→approve workflow (CLAUDE.md + `.claude/rules/workflow.md`), with the cross-story open-question sweep added. **No spec you write may contain an unresolved fork** — if the brainstorm surfaces a new fork, classify and close it (above) before the spec is done.

## Set Risk

From the spec, flag each story `standard` or `high` — `high` when it touches **order placement/modification**, **auth/signing**, **credential handling**, or **delivery semantics** (streaming completeness/backpressure/fill delivery) (`.claude/rules/backlog-format.md`). Decided when the design is known, not guessed post-hoc; `ship-backlog` reads it to scale review rigor.

## Shape & hand off

Fill each entry to **loop-ready** (`.claude/rules/backlog-format.md`): `Spec:` a path or `trivial-skip` (never `pending`), `Risk` set, `Done when` refined to observable criteria, decisions recorded, empirics verified (or the story `Deferred — <reason>` with its dependents deferred). Then: **operator approves, and you merge the spec bundle.** The stream now satisfies the loop-ready contract (`backlog-format.md` §Loop-ready vs drafted) and is ready for `ship-backlog`, whose pre-flight verifies it.

A handed-off stream where one story still says `Spec: pending` (and isn't `Deferred`), or carries an unclosed fork, or has no `Risk`, or rests on unverified upstream behavior, is **not** groomed — `ship-backlog` will bounce it back here.

## Project adapter (IbkrConduit) — references, not inlined

- **Entry shape + loop-ready contract:** `.claude/rules/backlog-format.md` (fields, `Risk`, `Spec ∈ path|trivial-skip|pending`, the `Deferred` status, 📦 marker, loop-ready vs drafted). Status/`Completes:` hygiene: `.claude/rules/backlog-status.md`.
- **The contract layer + gap routing:** `.claude/rules/contract-design.md` (design doc canonical; upstream behavior verified, never decided; the contract grammar).
- **Per-story spec workflow this wraps:** `CLAUDE.md` + `.claude/rules/workflow.md` (brainstorm → spec → plan with TDD steps → one story per PR).
- **Rule-settled exemplars** (apply, don't ask): the list in the sweep section above.
- **Live-probe mechanics:** `tools/ApiCapture/` → `recordings/`; `[EnvironmentFact("IBKR_CONSUMER_KEY")]` E2E via the DI pipeline (`.claude/rules/testing.md`); example apps under `examples/` with the paper credentials.
- **Composed skills:** `superpowers:brainstorming`, `writing-adrs`.

## Red flags — STOP

- About to **ask the operator** a question a `.claude/rules/` file / ADR / design doc / captured spec already answers.
- About to **decide a genuine product fork yourself** (or recommend it + "the operator confirms at review") instead of asking via `AskUserQuestion` now.
- The operator is **unavailable**, so you're tempted to decide an operator-fork yourself, punt it to the build, or push its story into the loop anyway — instead of `Deferred — operator decision required`.
- Pushing an **unapproved high-risk spec** into the unattended loop to hit a deadline (the operator-approval + spec-merge handoff hasn't happened).
- Writing a spec that contains **"implementer's call," `TBD`, "either approach is fine," or an unresolved option.**
- **Treating IBKR's documentation as evidence** — building on a documented-but-uncaptured behavior instead of probing the paper account or marking the story `Deferred`.
- A 📦 story handed off with **no breaking-vs-additive decision** recorded.
- Handing off a story still `Spec: pending` (not `Deferred`), or with **no `Risk`** set.
- Leaving a **multi-subsystem** story un-split, or a blocked story's **dependents** un-deferred.

## Rationalizations — and the reality

| Rationalization | Reality |
|---|---|
| "I'll recommend an answer and let the operator confirm it at PR review." | That leaves the fork **open** in the handed-off backlog — the opposite of loop-ready. Grooming is the **attended** stage: ask now via `AskUserQuestion`, record the answer, bake it in. The fork dies here. |
| "Short on time — just write the specs and let the implementer decide the open questions." | A spec with an open fork is **not done**. The implementer (an unattended loop) cannot decide a product fork or an empirical unknown. Close every fork before handoff: rule-settled → apply, operator → ask, empirical → probe or defer. "Implementer's call" is a forbidden phrase. |
| "The operator's asleep and the build window is now — I'll decide the fork (or let the implementer) so the loop isn't wasted." | An operator-fork can't be closed without the operator. Unavailable → `Deferred — operator decision required` + a pre-staged ask; defer its dependents. A wasted loop is cheaper than a guessed order-path behavior or a broken published surface RTOS is pinned to. You cannot compress an *attended* stage into an operator-absent window. |
| "It's high-risk but the spec's written and the clock's running — push it into the loop, the operator reviews the PR later." | The loop-ready handoff requires **operator approval + a merged spec**. An unapproved high-risk order-path/auth spec is `Spec: pending` ⇒ **not loop-ready**. Don't launder it into the unattended loop to hit a deadline. |
| "Asking the operator is slower — I'll just pick the sensible option." | For a **rule-settled** question, applying the rule *is* the answer — don't ask. For a genuine **operator fork**, picking silently is guessing on an operator-visible design choice; one batched `AskUserQuestion` is cheap and correct. |
| "This question feels open, I'll surface it to the operator." | First check whether a `.claude/rules/` file / ADR / design doc / captured spec already decides it. Asking a **settled** question wastes operator attention and invites drift. |
| "IBKR's docs describe the behavior, so the empirical question is answered." | **Documented ≠ verified.** The captured spec pins what the wire *actually did*; IBKR's docs regularly diverge (see `docs/ibkr-error-patterns-report.md`). If no recording or live observation confirms it, probe the paper account now or `Deferred` with the exact unblock. Building on an unconfirmed prior is the trap that burns a build round. |
| "The paper account probe can wait — the loop can run it tonight." | The unattended loop's gate is **offline-only** (`ship-backlog`); it never opens a live session. Grooming is the last attended stop — probe now or defer the story. |
| "It's one big story but it's cohesive — leave it whole." | Multi-subsystem (📦 surface + session lifecycle + example app + docs) violates one-story-per-PR. **Split it** (`workflow.md`); group by infra pattern. |
| "I'll set `Risk` later / the implementer can judge risk." | `Risk` is set **from the spec, during grooming**, when the design is known — not guessed post-hoc. `ship-backlog` reads it to scale review; a missing `Risk` bounces the story. |

## See also

- `.claude/rules/backlog-format.md` — the loop-ready entry schema this produces; `.claude/rules/backlog-status.md` — `Status`/`Completes:` hygiene.
- `.claude/rules/contract-design.md` — the contract layer, gap routing, and verification tiers.
- `.claude/skills/ship-backlog/` — the downstream consumer (builds the loop-ready backlog this hands off); `.claude/skills/draft-backlog/` — the upstream producer (decomposes a design/findings doc into the drafted backlog this grooms).
- `superpowers:brainstorming`, `writing-adrs` — composed to spec each unit; `.claude/rules/workflow.md` — the one-story-per-PR sizing this enforces.
