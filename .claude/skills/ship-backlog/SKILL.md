---
name: ship-backlog
description: >-
  Use when building multiple already-specced, dependency-ordered stories
  autonomously in one session — merging each as it passes review — without a human
  in the loop. Triggers: "build this stream/backlog out overnight," "ship these
  stories while I sleep," unattended/autonomous multi-story build, a loop-ready
  backlog to drain, batch-shipping a milestone's stories, hands-off DAG build-and-merge.
---

# Ship backlog

*Ported from realtest-order-steward @ 758375ab, adapted for a contract-first wrapper library.*

## Overview

Drain a **loop-ready** backlog (`.claude/rules/backlog-format.md`) into merged code, unattended, while the operator is away. The loop's value is in two disciplines the ambient project rules do **not** supply:

1. **Build by DAG waves with an independent reviewer per story** — never serial-by-listing, never all-at-once, never author-grades-own-work.
2. **Sweep, don't interrupt** — a single story that won't converge is swept and the loop continues; you wake the operator ONLY for a *systemic* blocker.

**Foundational rule:** violating the letter of these is violating the spirit. "It's faster," "it's basically done," and "just this once" are exactly the pressures this skill exists to resist. When tempted, sweep — don't shortcut.

## When to use

- A loop-ready backlog of 2+ specced, dependency-ordered stories to ship in one unattended session.
- "Build this stream out / merge as you go / while I sleep."

**Not** for: a single story (use `superpowers:subagent-driven-development`); a backlog that isn't loop-ready (run `groom-backlog` first — pre-flight bounces it); anything the operator wants to review story-by-story (that's not unattended).

## The offline boundary (load-bearing)

**The merge gate is the OFFLINE suite — the live-paper tier is NEVER an unattended gate.** The offline suite is everything `dotnet test` runs without credentials: unit tests, WireMock-integration tests, mock-WebSocket tests (`[EnvironmentFact]` E2E tests auto-skip without `IBKR_CONSUMER_KEY`). Live-paper verification is **attended, operator-run**: it needs credentials, IBKR allows one brokerage session per user (a loop-opened session competes with the operator's), and behavior is market-hours-dependent. `groom-backlog` pre-verifies every empirical dependency precisely so this loop **never needs the live account**. No lane, agent, or "quick check" in this loop ever sets `IBKR_CONSUMER_KEY` or touches `.ibkr-credentials/`.

## Pre-flight

1. **Verify the loop-ready contract per story** (§ `backlog-format.md`): merged `Spec` (path or `trivial-skip`, not `pending`), `Risk` set, `Done when` refined, no open fork, upstream-behavior dependencies verified (a recording or probe the spec cites). Any story that isn't loop-ready → **bounce it**: mark `Deferred — not loop-ready`, defer its dependents, flag for grooming. Never guess a missing spec/decision.
2. **Build the dependency DAG** from each `Depends on:` id-list. Group into **waves** (a story is buildable once all its deps are *merged*).
3. **Init the run-state ledger** — a **durable** file, one row per story: `id → {wave, phase, PR, risk, verdict}`. This is the orchestrator's **external memory**, so your context stays lean and the run is **resumable**: after a compaction/restart, re-read the ledger + `git log main` + `gh pr list` and continue from the first incomplete row. Update it after **every** state transition. **Keep it outside any disposable worktree/sandbox** — a wiped worktree or a tree-moving `git pull` must not take the ledger (and thus the run state) with it; durability + git/gh as the source of truth is what makes a mid-run reset recoverable.

## The wave loop (until the DAG is drained)

Launch every story whose deps are **merged**, concurrently, **up to a cap of 3**, **each in its own git worktree (see Worktree isolation)**. Each story runs the per-story pipeline. When a story merges, re-evaluate the DAG — its dependents may now be buildable. Repeat until no story remains buildable (all done or deferred).

> **Why 3 (re-derived for this repo):** (a) agent-API rate limits — more concurrent impl/review agents than ~3 stalls on throttling; (b) each lane's genuine-run gate is a full offline suite (~1200 tests spinning WireMock servers, mock WebSocket servers, and real-timer resilience tests — the `Category=Slow` set); several concurrent suites on one box starve CPU and surface timing flakes in the rate-limiter/tickle-timer tests that pass in isolation; (c) merges serialize anyway — extra lanes mostly wait. **≤3, never 4; on a small box, dial to 2.** And regardless of lane count, the offline boundary holds: no lane ever opens a live IBKR session.

## Worktree isolation (load-bearing — one story, one branch, one worktree)

**Every story builds in its own git worktree + branch — REGARDLESS of parallelism.** This is not a parallelism optimization; it is a hard isolation rule that holds even for a single sequential story.

- **Concurrent stories in one checkout stomp each other** — the wave loop runs up to 3 at once, and their `git checkout` / branch / file-edit operations collide in a shared working folder. (A real RTOS `ship-backlog` run had multiple agents colliding in the same folder — this rule is the fix.)
- **Sequential reuse of one checkout is also wrong** — a successive story inherits the prior story's dirty tree / checked-out branch / leftover artifacts. So a fresh worktree per story, even when building one at a time.

The mechanism: spawn each story's impl/fix/quality agent with **`isolation: "worktree"`** (the Agent/Workflow tool creates a fresh worktree off `main` and auto-cleans it), or `git worktree add` a per-story directory off `main`. The **branch** (`feat/<id>-…`) and the **worktree** are per-story and 1:1.

- **The run-state ledger lives in the MAIN checkout, never a worktree** (already required in Pre-flight) — a `git worktree remove` or a tree-moving `git pull` must never take the ledger (and thus the run state) with it.
- **Cleanup on merge:** `git worktree remove --force <dir>`, then `git branch -D <branch>`, then `git pull --ff-only` on `main` in the orchestrator's checkout. A swept/deferred story's worktree is removed too (its draft branch stays on the remote).

## Per-story pipeline

```
implement ──▶ quality (independent) ──▶ CLEAN? ──▶ merge ──▶ ledger:done, unblock dependents
   │                  ▲                    │
   │ large story      │ ISSUES             └─ still ISSUES after 2 rounds
   └▶ subagent-       └─ fix agent ◀────────┐         │
      driven-dev          (≤ 2 rounds)──────┘         ▼
                                              SWEEP + defer (PR stays DRAFT)
```

1. **Implement — adaptive grain.** Default: **one impl agent**, full standard workflow (TDD red/green/refactor per `.claude/rules/tdd-workflow.md`, self-review, the full check clean — build, test, `dotnet format --verify-no-changes` as **separate commands** per `.claude/rules/bash-usage.md` — **in its own per-story worktree + branch off `main`** — never the shared checkout, see Worktree isolation — draft PR, flip the story's backlog `Status` + add the `Completes:` trailer per `backlog-status.md`). A pre-flight-flagged **large/multi-subsystem** story → delegate to `superpowers:subagent-driven-development` (which still runs inside that story's one worktree).
2. **Quality — an INDEPENDENT agent** (never the impl agent grading its own work). It re-runs the full offline suite, **verifies the genuine-run guard** (below), checks `Done when` spec-compliance + false-green guards, and adversarially probes the impl's self-flagged risks. **Risk-scaled** off the entry's `Risk` field: `high` (or an impl self-flag, or a large diff, or a 📦 public-surface story) → a **2–3 agent adversarial panel** (distinct lenses — e.g. correctness, wire-fidelity-vs-recordings, consumer-impact); `standard` → one agent. **Panel adjudication is by finding, not by vote:** the merge requires *every* panelist `CLEAN`. Any non-CLEAN verdict carrying a **correctness, security, hermeticity, or rule-mandated finding blocks** (e.g. a missing 401-recovery test on a new integration-tested endpoint — `testing.md` mandates it) → fix-loop; a panelist's pure **nit** (style, or coverage of a genuinely non-required path) is noted and does **not** block. A split (one CLEAN, one `CHANGES-REQUESTED`) is **not** a tie to majority-vote — the substantive finding governs. (A real RTOS run caught exactly this: one reviewer CLEAN, one flagging an untested rule-mandated path — the path won, correctly.)
3. **Fix loop — bounded to 2 rounds.** On ISSUES, a fix agent addresses them; re-quality. Still ISSUES after the 2nd round → **sweep** (Escalation, below). Do not start a 3rd round.
4. **Merge — on CLEAN + CI green:** squash-merge with a curated Conventional-Commits subject (this is release-please input — see Release discipline), update the ledger to `done`, unblock dependents.

### The genuine-run guard (this repo's exact mechanism — verified empirically 2026-07-06)

The quality agent MUST confirm the local suite **genuinely ran** — a real, non-zero test count, with the new tests confirmed to have executed. Never assume the suite ran because it printed green. This repo is xUnit v3 + Microsoft Testing Platform (`global.json`); MTP flags per `.claude/rules/test-filtering.md`. What's verified here:

- **A zero-test run fails loudly by default**: exit code 8, summary `Zero tests ran` (unlike VSTest-era runners whose 0-test run exits green). So a completely-empty run cannot masquerade as success.
- **The residual false-greens are under-counts, not zero-counts** — guard them explicitly:
  - **Stale binaries:** `--no-build` runs whatever was last compiled; new tests that never built "pass" by absence. Always `dotnet build --configuration Release` first, then test.
  - **Over-narrow filters:** a `--filter-class` typo that matches *some* tests passes the zero-check while silently skipping the new ones. Enforce a floor with **`--minimum-expected-tests <N>`** — an under-count fails with exit code 9 and prints the real count (`tests ran X, minimum expected N`).
  - **CI ≠ local scope:** CI excludes `--filter-not-trait "Category=Slow"`; the local full run includes them. The quality agent's authoritative run is the **full** local suite.
  - **Skips are not runs:** `[EnvironmentFact]` E2E and `[Fact(Explicit = true)]` tests report `skipped` — they never count toward the executed total.
- **The canonical quality-agent sequence** (separate commands per `.claude/rules/bash-usage.md`):
  1. `dotnet build --configuration Release`
  2. `dotnet test --no-build --configuration Release` — read the summary's aggregate `total:` (the full offline suite prints a four-digit count, e.g. `total: 1180` at the time of porting) and confirm it is ≥ the pre-change baseline plus the story's new tests
  3. `dotnet test --project <test project> --no-build --configuration Release --filter-class "*<NewTestClass>*" --minimum-expected-tests <N>` — pin that the story's own tests executed
  4. `dotnet format --verify-no-changes`
- The quality agent **reports the actual numbers it observed** (aggregate total + the story's scoped count). This independent confirmation is the only reason per-story local-green is authoritative.

### Agent contracts (terse structured returns)

| Agent | Returns |
|---|---|
| **Impl** | `{branch, pr, files-by-area, test-count+result, guards-confirmed, format-clean, deviations+why, risks-for-reviewer}` (≤ ~30 lines) |
| **Quality** | `VERDICT: CLEAN \| ISSUES`. CLEAN → verified items + **genuine-run confirmation (real counts: aggregate + scoped)** + nits. ISSUES → numbered `area · what's wrong · severity · specific fix` (actionable without re-investigation). |

The orchestrator retains only these verdicts — never the implementation churn.

## Escalation — sweep, don't interrupt

```
per-story trouble (fix-loop non-convergence after 2 rounds; blocker the fix agent can't resolve)
        │
        ├─ systemic? (shared infra down · destructive/ambiguous · security/secret or credential exposure)
        │        └─ YES ─▶ INTERRUPT (wake the operator) — the only wake reason
        │
        └─ single story didn't converge ─▶ SWEEP: backlog follow-on + Status `Deferred — <reason>`,
                                            leave its PR a DRAFT (never merge broken),
                                            defer its dependents, CONTINUE the loop.
```

**Default on any per-story trouble is SWEEP.** Keep the loop alive: the unresolved issue becomes a backlog follow-on, the story's PR stays a **draft** (a non-converged story is never merged and never marked ready-for-review), the story + its dependents are marked `Deferred`, and the loop continues with whatever is still buildable. **Interrupt only for a systemic blocker** — never for a single story that didn't converge. The finalize wrap-up lists every deferral and why.

## CI cadence — the merge gate

- **Per-story gate:** the repo CI workflow's **`build-and-test`** job green on the PR (format + build + single-file example-app builds + the offline test run) **+** two genuine full-suite local runs (impl + quality, with the guard above) **+** quality CLEAN. That job **is** the offline suite — there is no heavier unattended tier to wait for.
- **What CI does NOT cover** (the local runs do): the `Category=Slow` tests (CI excludes them) and the `[Fact(Explicit = true)]` KeyGenerator tests — if a story touches `tools/IbkrConduit.Setup/`, the impl agent must run the explicit KeyGenerator suite per `.claude/rules/explicit-tests.md` and the quality agent must confirm it.
- **Live-paper verification is NOT a gate here, per-story or at finalize** — see The offline boundary. A story whose `Done when` genuinely requires live confirmation should never have passed grooming into this loop; if one slipped through, bounce it (`Deferred — needs attended live verification`), don't improvise a live session.

## Release discipline

Squash-merge subjects are **release-please input** (`release-please-config.json`, pre-1.0: `feat`/`feat!` → minor, `fix` → patch, `docs`/`chore`/`test`/`refactor` → no release):

- Use the story's grooming-decided commit type. A 📦 story's **breaking-vs-additive decision was made at grooming** — carry it: `feat!:` (with a `BREAKING CHANGE:` footer naming what consumers must change) vs `feat:`. Never decide breaking-ness in the loop.
- Release-please will open/refresh its release PR as merges land; **leave it alone** — the operator cuts the release. Never merge the release PR from the loop.

## Finalize

Run the full check once on post-merge `main` (build, full test suite, format — separate commands). **Sweep** all deferred/follow-on items into the backlog. Post a wrap-up: shipped (PR numbers) vs deferred (+ why) vs blocked, including the observed final test total.

## Project adapter (IbkrConduit) — references, not inlined

- **Backlog entry shape + hygiene:** `.claude/rules/backlog-format.md` (entry shape, `Risk`, `Deferred`, parseable `Depends on`, 📦 marker); `.claude/rules/backlog-status.md` (the `Status`-flip + `Completes:` trailer — convention-only today; required when a PR completes a backlog story).
- **Merge gate:** `.github/workflows/ci.yml` — the `build-and-test` job (docs-only diffs skip it via the paths filter; `Category=Slow` excluded in CI only).
- **Test invocation + filtering:** `.claude/rules/test-filtering.md` (MTP flags: `--project`, `--filter-class`, `--filter-method`, `--minimum-expected-tests`); `.claude/rules/bash-usage.md` (no `&&` chaining — separate Bash calls); `.claude/rules/explicit-tests.md` (the opt-in KeyGenerator suite).
- **Hermeticity the quality agent probes:** `.claude/rules/testing.md` — unit tests make no network calls and no file I/O; integration tests are WireMock-only through the full DI stack (`AddIbkrClient` + `BaseUrl`); every integration-tested endpoint has its **401-recovery test**; E2E classes carry `[Collection("IBKR E2E")]`; fixtures use synthetic credentials only (`.claude/rules/security.md` — and a leaked credential in a fixture is a **systemic interrupt**, not a sweep).
- **Coverage discipline:** `.claude/rules/testing.md` `[ExcludeFromCodeCoverage]` rules — the quality agent checks a story didn't hide new branching logic behind an exclusion.
- **Release train:** `release-please-config.json` + `docs/ibkr_conduit_design.md` §17.4.

## Red flags — STOP

- About to **merge** a PR with any known or flaky failure.
- About to **wake the operator** for a single story that didn't converge.
- Trusting a **green** suite without confirming the real counts (aggregate total + the story's scoped count).
- About to **set `IBKR_CONSUMER_KEY`, read `.ibkr-credentials/`, or open any live IBKR session** from the loop.
- Building a story whose deps **aren't merged**, or launching **more than 3** concurrently.
- Building a story in the **shared main checkout** instead of its own per-story worktree (concurrent OR sequential).
- The **impl agent grading its own** work (no independent quality agent).
- Deciding a 📦 story's **breaking-vs-additive** call in the loop, or merging the **release-please PR**.
- Marking a parked / non-converged story's PR **"ready"** instead of leaving it a draft.
- Starting a **3rd** fix round on the same story.

## Rationalizations — and the reality

| Rationalization | Reality |
|---|---|
| "4 of 5 merged, the 5th has 1 flaky failure, re-running is slow — just merge it." | A flake is **stop-and-fix, never merge-past**. Sweep the story (`Deferred` + follow-on), leave its PR a draft, finalize the rest. A merged flake poisons trust in the whole suite. |
| "It's 3am and this one story is stuck — just wake the operator to decide." | One non-converged story is **never** a wake reason. Sweep it and continue. Wake **only** for a systemic blocker (shared infra down, destructive/ambiguous, security/credential exposure). |
| "Local green is enough — the suite obviously ran." | Confirm the real counts. Zero-test runs fail loudly here (MTP exit 8), but **under-counts don't** — stale `--no-build` binaries and over-narrow filters pass the zero-check while skipping the new tests. `--minimum-expected-tests` + reported counts are **mandatory**, assigned to the independent quality agent, not assumed. |
| "The spec says IBKR behaves this way — I can code to it and skip the recording check." | The loop builds only what grooming verified. If the spec cites no recording/probe for a load-bearing upstream behavior, the story is **not loop-ready** — bounce it. Documented ≠ verified. |
| "A quick live tickle against the paper account would settle this ambiguity." | The live tier is **attended-only**: credentials, a competing brokerage session, market-hours variance. The loop never opens one — sweep the story with the exact live question written out for the operator. |
| "These stories are basically independent — build them all at once / all serially." | Build by the **DAG**: launch only stories whose deps are *merged*, cap 3. All-at-once races merges and saturates the box; all-serial wastes the night. |
| "They run one-after-another, so one working folder is fine." | **Per-story worktree regardless of parallelism.** Sequential reuse inherits the prior story's dirty tree/branch state; concurrent reuse has agents stomping each other's checkout. One story = one branch = one worktree, always. |
| "I'll just self-review the impl I wrote." | The reviewer must be an **independent agent**. Author-grades-own-work misses the false-greens an independent re-run catches. `high` risk / 📦 → an adversarial panel. |
| "The spec looks fine, the story's basically ready — start building." | Pre-flight **verifies** the loop-ready contract per story. `Spec: pending` / open fork / missing `Risk` / unverified empirics → **bounce** to grooming. Don't guess. |
| "It's additive-ish — I'll soften the subject to `feat:` so the release looks calmer." | Breaking-ness was **decided at grooming** and RTOS re-pins against it. Carry the decided `feat!:`/`feat:` exactly; a mislabeled breaking change ships a corrupted contract to a live consumer. |
| "Leave the stuck high-risk PR ready-for-review so the operator just merges it." | A non-converged story's PR stays a **draft**. Marking it ready invites an unreviewed merge of broken — possibly irreversible — work. |
| "Just keep retrying the fix until it's green." | Bounded to **2** rounds, then sweep + defer. Unbounded retry is how the loop hangs till morning with nothing shipped. |

## See also

- `.claude/rules/backlog-format.md` — the loop-ready entry schema this parses; `.claude/rules/backlog-status.md` — `Status`/`Completes:` hygiene the impl agent performs.
- `.claude/skills/groom-backlog/` — the upstream producer of loop-ready backlogs (run it if pre-flight bounces stories); `.claude/skills/draft-backlog/` — the (further upstream) decomposition stage.
- `superpowers:subagent-driven-development` — delegated per-*story* execution for a large story (this skill is per-*stream*; they nest).
- `superpowers:test-driven-development` + `.claude/rules/tdd-workflow.md` — the impl agents' inner discipline; `.claude/rules/testing.md` — the hermeticity standard the genuine-run guard and quality probes serve.
