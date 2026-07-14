---
name: ship-backlog
description: >-
  Use when building multiple already-specced, dependency-ordered stories
  autonomously in one session — merging each as it passes review — without a human
  in the loop. Triggers: "build this stream/backlog out overnight," "ship these
  stories while I sleep," unattended/autonomous multi-story build, a loop-ready
  backlog to drain, batch-shipping a milestone's stories, hands-off DAG build-and-merge.
---

# Ship backlog (driver)

*Ported from realtest-order-steward @ bfec311a, adapted for a contract-first C#/.NET wrapper library.*

Drain a **loop-ready** backlog (`.claude/rules/backlog-format.md`) into merged code, unattended, and **survive all night** — across compactions, rate-limits, API errors, and a hard-dead session. Full design + rationale: **`.claude/workflows/ship-backlog.workflow.md`** (D1–D13).

## Two artifacts — you are the DRIVER

Orchestration is split by execution-fit:

- **`.claude/workflows/ship-story.mjs`** (Workflow tool) — a deterministic script that builds ONE story to review-CLEAN (approach-check → impl → content-assigned lens panel → bounded fix). It sheds all impl/review/fix churn into discardable agents and returns a terse verdict. It does **NOT** gate or merge.
- **This skill (the driver)** — the thin main-loop that walks the DAG, invokes `ship-story.mjs` per story, runs the **serialized local heavy gate** (background bash — the one place a multi-minute job runs reliably), judges flake-vs-real, merges, sweeps, and keeps the ledger/observability. Run the driver on **Opus 4.8 or newer** (Fable 5 included).

## THE CARDINAL RULE — the ledger is your ONLY working memory (D5)

`ship-run-ledger.md` (in the main checkout) is the single source of truth. **Re-derive full state from it — plus `git log main`, `gh pr list`, backlog `Status:` lines — at the top of EVERY iteration. Never act on remembered conversation history without a ledger re-read.** This is what makes auto-compaction, a restart, a lease takeover, or resume-tomorrow a **non-event** instead of "the orchestrator forgot." Update the ledger row + heartbeat after **every** state transition.

## THE OFFLINE BOUNDARY — IbkrConduit-specific, non-negotiable

**The merge gate is the OFFLINE suite. The live-paper tier is NEVER an unattended gate.** The offline suite is everything `dotnet test`/the built MTP exe runs without credentials: unit tests, WireMock-integration tests, mock-WebSocket tests (`[EnvironmentFact]` E2E tests auto-skip without `IBKR_CONSUMER_KEY` — that skip is correct, never chase it). Live-paper verification is **attended, operator-run only**: it needs credentials, IBKR allows one brokerage session per user (a loop-opened session competes with the operator's), and behavior is market-hours-dependent. `groom-backlog` pre-verifies every empirical dependency (a recording or live probe the spec cites) precisely so this loop **never needs the live account**. **No lane, agent, or gate step in this entire loop — impl, fix, reviewer, driver, or gate — ever sets `IBKR_CONSUMER_KEY`, reads `.ibkr-credentials/`, or opens a live IBKR session.** A story whose `Done when` genuinely requires live confirmation should never have passed grooming; if one slipped through, bounce it (`deferred — needs attended live verification`), never improvise a live session. Touching the live account from this loop is a **systemic interrupt**, not a sweep.

## Entry (every invocation — interactive OR a restart re-invocation)

1. **Read `ship-run-ledger.md`.** None → fresh run: run **Pre-flight**. Exists → **claim-or-yield the lease**: the `lease` block holds `{session_id, phase, phase_started_at, heartbeat_at}`. **Staleness is HEARTBEAT AGE, never phase duration:** stale ⇔ `now − heartbeat_at` exceeds the current phase's ceiling — `building` 60m (one phase covers the whole ship-story run; the workflow is opaque to the driver), `gating` 25m, idle 25m. `phase_started_at` is informational — a 3-hour build phase with fresh heartbeats is **LIVE** (a legitimately long story must never invite a takeover). Fresh heartbeat → **another driver is live → exit immediately** (no double-drive). Stale → claim it (write your session id, `heartbeat_at=now`) and continue.
2. **Emit the status block** (see Observability) so this session shows current state.

## Pre-flight (fresh run only)

1. **Verify the loop-ready contract per story** (`backlog-format.md`): merged `Spec` (not `pending`), `Risk` set, `Done when` refined, no open fork, upstream-behavior dependencies verified (a recording or probe the spec cites — `.claude/rules/contract-design.md`). Not loop-ready → **bounce**: ledger `phase: deferred`, note `not loop-ready`, defer dependents. Never guess a missing spec/decision.
2. **Build the ledger** (one row per story; parse `deps`, read `risk`, note the 📦 marker; `phase: pending`, `attempts: 0`, and a **deterministic `branch`** — `feat/<id>-<short-slug>` computed once HERE, so every retry of the story reuses one branch/PR instead of orphaning drafts).
3. **Arm the in-session heartbeat** (a `ScheduleWakeup` re-entry cadence — see Fault recovery). There is **no** external cron watchdog (`CronCreate` is session-only).
4. **Claim the lease.**

## The loop (until the DAG is drained)

Each iteration:

1. **Re-derive** from the ledger. **Buildable** = `pending`/`retry-pending` stories whose every `dep` is `merged`. None buildable and none in flight → **Finalize**.
2. **Pick the next buildable story** (DAG order; **fresh `pending` ahead of `retry-pending`** — don't grind a retry while fresh work waits; ties by backlog order). Set `phase: building`, `phase_started_at=now`, heartbeat. **A `retry-pending` row that already carries `review: CLEAN` + branch/PR (a gate-time infra retry) skips the build — go straight to Gate.**
3. **Invoke** `Workflow({ scriptPath: ".claude/workflows/ship-story.mjs", args: {id,title,risk,deps,specPath,doneWhen,backlogFile,frozenMarker,branchName} })` (run in background; await the task-notification). `branchName` is the row's deterministic branch — the SAME name on every attempt, so a retry reuses the prior attempt's branch/PR. Record the returned **runId** in the row (`wf_run`). On its return:
   - `status: 'INFRA'` (or the workflow died / returned null) → **infra failure** → `attempts += 1`. Under the cap → `phase: retry-pending`, note the cause, heartbeat, **continue** (retried a later pass — NEVER swept as a code failure). **At `attempts = 3` → `phase: deferred`, note `infra cap`** — the livelock guard: a story failing the same way every pass must not spin the loop all night; it lands in the Brief for the operator. **Same-session retry: prefer `Workflow({scriptPath, resumeFromRunId: <wf_run>, args: <same>})`** — completed stages replay from the journal cache; only the dead stage re-runs. (Resume is same-session only — after a restart, invoke fresh with the same `branchName`.)
   - `status: 'DEFERRED'` → the story itself didn't converge (2 fix rounds, an unfixable finding, or impl blocked on the story) → **code sweep**: `phase: deferred`, PR stays a **draft**, defer dependents.
   - `status: 'CLEAN'` (`{branch, pr, verdict}`) → **Gate** (below). Carry the verdict's nits/author-deviations into the row's `notes`.
4. **Gate + merge.** Update `phase` per outcome.
5. **Update ledger + heartbeat + emit the status block.** Then run the between-stories cleanup: `dotnet build-server shutdown` (reclaims MSBuild/compiler server nodes a build agent left, so RAM doesn't creep) and `git worktree prune` + `git worktree remove --force` any leftover `.claude/worktrees/wf_*` (fix/review agent worktrees that wrote commits don't always auto-clean and accumulate across a long run). Loop.

## The gate (serialized local heavy lane — the ONE heavy run at a time)

On a CLEAN story:

> **Driver immunity (load-bearing).** Do ALL gate work in a **dedicated worktree**; NEVER `git checkout`/rebase in the orchestrator's main checkout. A build agent may have moved the main checkout's HEAD, so the driver must **not depend on its own HEAD being stable** — the main checkout's branch is irrelevant to gating and merging, which go via worktrees + `gh`.

1. `git fetch origin`. Create the gate worktree **on the story branch**: `git worktree add <dir> origin/<branch>`. **Inside it** (operate via `git -C <dir> …`, never `cd` the main checkout): rebase on current `origin/main` (near-trivial — serial build means nothing else is in flight; resolve the routine backlog-file line-flip keep-both if any), then `git -C <dir> push --force-with-lease origin HEAD:<branch>`.
2. **In that same gate worktree**, via a **main-loop background bash** (it reliably notifies on completion, unlike a stalling subagent):
   1. `dotnet build --configuration Release -nodeReuse:false` (the `-nodeReuse:false` stops persistent MSBuild server nodes leaking — they accumulate across a long run → memory pressure/OOM).
   2. **Run the built MTP test executable directly** (NOT `dotnet test` — it discovers 0 and exits green per `.claude/rules/test-filtering.md`) with **`--minimum-expected-tests <N>`** (genuine-run guard). Set `N` from the **previous green gate's reported total** (first gate of the run: the last known suite count) — a ratchet, so a silently-shrunk discovery fails loudly; a story that legitimately deletes tests lowers the floor (note it in the ledger).
   3. `dotnet format --verify-no-changes`.
   4. A story touching `tools/IbkrConduit.Setup/` also runs the explicit KeyGenerator suite (`.claude/rules/explicit-tests.md`) in this same serial lane.
3. **STRICT SERIAL HEAVY — cardinal:** the gate and any impl/fix scoped test run must **never overlap** (box-starvation/timing-flake class — this repo's own memory records exactly this failure mode: leaked dispose loops + real-timer parallelism, fixed by the `DisableParallelization` collection). Serial build makes this hold by construction; still, before a gate, confirm nothing else heavy runs (`ps` for `testhost`).
4. **Outcomes:**
   - **green** → **`gh pr ready <pr>`** (the impl opened it as a *draft*; a draft can't be merged — mark it ready ONLY now, on green, never for a swept story) → confirm **`build-and-test`** CI green → squash-merge via `gh` (curated Conventional-Commits subject) → `phase: merged`, `git fetch origin` (the driver works via worktrees + `gh` — it does NOT need local `main` checked out, so no `git pull`/`checkout` in the main checkout), remove the gate + story worktrees (`git worktree remove --force`, `git branch -D`).
   - **fail** → spawn a **flake-vs-real judgment agent** (Opus/high — it reads the failure so the output never enters your context): `flake` → re-run the gate **SOLO** locally; still ambiguous → one `gh workflow run ci.yml --ref <branch>` clean-runner disambiguation. **Real / reproduces on the clean runner** → `phase: deferred` (real regression — never fix-looped, never merged). Rate-limit/infra during the gate → `attempts += 1`, `phase: retry-pending` **keeping `review: CLEAN` + branch/PR in the row** — the re-pass goes straight back to Gate, never a rebuild.

**Local-first CI discipline:** the local serial gate is authoritative. Reserve GitHub CI for flake-disambiguation + the single Finalize pass (CI-minute conservation). CI's `build-and-test` job excludes `Category=Slow` — the local gate's full run includes it.

## Sweep, don't interrupt

- **Default on per-story trouble is SWEEP** — the story becomes a backlog follow-on, its PR stays a **draft**, it + its dependents go `deferred`, the loop continues.
- **INTERRUPT (wake the operator) ONLY for a systemic blocker**: shared infra genuinely down (a rate-limit is NOT this — that's `retry-pending`), destructive/ambiguous state, security/secret/**credential** exposure, or any lane about to touch a live IBKR session (the offline boundary, above). **Never** for one non-converged story.

## Fault recovery (never waste the night)

- **In-session heartbeat + ledger-based restart — NOT an external cron (`CronCreate` is session-only).** ⚠️ `CronCreate` and `ScheduleWakeup` are **in-memory, session-scoped** — they die WITH the session — so there is **no in-tool mechanism to auto-resurrect a hard-dead session**. Realistic fault recovery is two-legged: **(a) while the session LIVES**, use `ScheduleWakeup` to re-enter the driver on a heartbeat cadence and to **back off on rate-limits** — the session survives a rate-limit and resumes from the ledger; **(b) a hard-dead/killed session must be RE-INVOKED externally** (the operator, or a `/goal` Stop-hook keeping the session alive). Because *all* state lives in the ledger (D5), that restart **resumes cleanly** — re-derive from ledger + `git log main` + `gh pr list`, continue from the first incomplete row. The lease/heartbeat still earns its keep: it stops a restart from **double-driving** a session that is in fact still alive.
- **Phase-aware heartbeat + lease.** Write `heartbeat_at` every loop iteration + transition **and on every `ScheduleWakeup` wake — including while a background ship-story or gate is still running.** The wakeup cadence must sit comfortably inside the tightest ceiling (fire every ~10–15m vs the 25m idle/gating ceiling), or a long quiet phase looks dead. Staleness is **heartbeat age vs the phase ceiling** (see Entry), never phase duration.
- **Infra-vs-code (load-bearing).** Rate-limit / API error / terminal `agent()` null / infra-blocked impl / gate-time transient → `phase: retry-pending`, **retried a later pass, NEVER swept as a code failure** — but bounded: **3 INFRA attempts → `deferred (infra cap)`**, the livelock guard. Back off FIRST on a rate-limit so one rate-limit window doesn't burn the cap. Only 2-round non-convergence, an impl blocked on the story itself, or a real gate regression → permanent `deferred`.
- **Exponential backoff.** On a rate-limit, the live driver `ScheduleWakeup`s an increasing delay and resumes from the ledger when it clears.

## Observability — the in-session status block

`ship-run-ledger.md` is BOTH the machine source AND the human board. Emit this block (rendered from it) on **every transition and each wake**, and on demand ("status?"):

```
SHIP RUN — <label> · started HH:MM · ❤️ updated Nm ago
  ✅ X merged · 🔧 Y deferred · 🔁 Z retry-pending · ⏳ W remaining · of TOTAL
▶ NOW: <id> · phase <PHASE> (~Nm) │ NEXT: <ids>
DEFERRED: <id> (<reason>) …
RETRY-PENDING: <id> (<reason> → next pass) …
```

The heartbeat age surfaces a stall; the retry/deferred lists surface trouble. The **file is the anchor** (any session — the live one or a restart — writes it, so it's never stale). An Artifact dashboard is an optional best-effort mirror only, never the source of truth.

## Finalize

DAG drained → one `workflow_dispatch` CI pass on `main` (the single sanctioned heavy dispatch — confirms the integrated whole on the clean runner; red → fix-forward). **Release the lease.** Post the **Brief**: shipped (PR#s) · deferred (+why — code vs `infra cap`) · retry-pending-that-never-cleared (+why) · blocked, including the observed final test total. Sweep all follow-ons into the backlog.

## Release discipline

Squash-merge subjects are **release-please input** (`release-please-config.json`, pre-1.0: `feat`/`feat!` → minor, `fix` → patch, `docs`/`chore`/`test`/`refactor` → no release): use the story's grooming-decided commit type. A 📦 story's **breaking-vs-additive decision was made at grooming** — carry it: `feat!:` (with a `BREAKING CHANGE:` footer naming what consumers must change) vs `feat:`. **Never decide breaking-ness in the loop.** Release-please will open/refresh its release PR as merges land; **leave it alone** — the operator cuts the release. Never merge the release PR from the loop.

## Red flags — STOP

- Acting on remembered state **without re-reading the ledger** (the compaction-forgetting bug this design exists to kill).
- Treating a **rate-limit / API error as a permanent sweep** (it's `retry-pending`) — or waking the operator for it.
- **Merging past a red gate** without confirming real-vs-flake on the clean runner; dismissing a clean-runner-reproducing failure as "just a flake" (that's a REAL regression → sweep).
- **Overlapping the serial gate** with any impl/fix scoped test run (or a zombie agent still testing) — strict serial heavy.
- Trusting a **green** suite without a real **non-zero** count (the `--minimum-expected-tests` floor); `dotnet test` (discovers 0, exits green — run the built MTP exe).
- Any agent operating in the **shared main checkout** instead of its own worktree.
- Marking a non-converged story's PR **ready** instead of leaving it a draft; starting a **3rd** fix round.
- Building a story whose deps **aren't merged**.
- Retrying an INFRA story past the **3-attempt cap**.
- Taking over a lease whose **heartbeat is fresh** because the phase has merely run long — staleness is heartbeat age, never phase duration.
- **Rebuilding a gate-time infra retry from scratch** — a row with `review: CLEAN` + branch/PR re-enters at Gate, not at ship-story.
- **Setting `IBKR_CONSUMER_KEY`, reading `.ibkr-credentials/`, or opening any live IBKR session** from any lane in this loop — the offline boundary is absolute.
- Deciding a 📦 story's **breaking-vs-additive** call in the loop, or merging the **release-please PR**.

## Rationalizations — and the reality

| Rationalization | Reality |
|---|---|
| "It hit a rate-limit at 2am — mark the story failed and move on." | Rate-limit = **`retry-pending`**, retried when the API clears. In-session `ScheduleWakeup` backoff resumes the live session; a *killed* session resumes cleanly from the ledger on restart. Nothing is swept for infra. |
| "I remember I already merged that one." | **Re-read the ledger.** Memory is disposable; the ledger + git are truth. |
| "The gate failed on unrelated classes — box flake, just merge." | Confirm: re-run SOLO, then the clean runner. Reproduces on the clean runner = REAL regression → **sweep, don't merge**. |
| "A quick live tickle against the paper account would settle this ambiguity." | The live tier is **attended-only** — the offline boundary is non-negotiable. Sweep the story with the exact live question written out for the operator. |
| "One story is stuck — wake the operator." | Never for one story. **Sweep** and continue. Wake only for a systemic blocker. |
| "I'll gate two ready branches in parallel to save time." | **One heavy run at a time, ever.** Concurrent WireMock/mock-WebSocket suites starve the box. |
| "It'll probably work on the 4th retry." | Three INFRA attempts is the cap. A story failing the same way every pass is a **livelock**, not bad luck — park it `deferred (infra cap)`, spend the run on stories that CAN ship. |
| "That build phase has run 90 minutes — the driver must be dead, take the lease." | Staleness is **heartbeat age**, not phase duration. A meaty story legitimately runs hours; if heartbeats are fresh, the driver is live. |
| "I'll just self-review the impl I wrote." | The reviewer must be an **independent agent**. `high` risk / 📦 → the full lens panel is unconditional anyway. |
| "It's additive-ish — I'll soften the subject to `feat:` so the release looks calmer." | Breaking-ness was **decided at grooming**. A mislabeled breaking change ships a corrupted contract to a live consumer (RTOS). |
| "Leave the stuck high-risk PR ready-for-review so the operator just merges it." | A non-converged story's PR stays a **draft**. |
| "Just keep retrying the fix until it's green." | Bounded to **2** rounds, then sweep + defer. |

## See also

- `.claude/workflows/ship-backlog.workflow.md` — the full design + D1–D13 rationale; `.claude/workflows/ship-story.mjs` — the per-story builder.
- `.claude/rules/backlog-format.md` / `backlog-status.md` — the loop-ready entry schema + `Status`/`Completes:` hygiene the impl agent performs.
- `.claude/rules/testing.md` — the hermeticity discipline the lenses probe and the serial gate serves; `.claude/rules/test-filtering.md` — MTP invocation.
- `.claude/rules/contract-design.md` — the design-pass/spec routing the lenses assume already happened (a story never authors a contract decision).
- `.claude/skills/groom-backlog/` — the upstream producer of loop-ready backlogs (run it if pre-flight bounces stories).
