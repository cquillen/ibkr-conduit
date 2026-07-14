# Workflow spec — ship a backlog (compaction-proof, fault-tolerant)

**Status:** Ported from realtest-order-steward @ bfec311a (2026-07-14), adapted for a contract-first C#/.NET wrapper library. Source design: RTOS's `/loop-me` grilling session (D1–D13), hardened post-#650 (§11 as-built corrections).
**Derives from:** the legacy `.claude/skills/ship-backlog/SKILL.md` (wave-based, ≤3 concurrent, single-session orchestrator) — this supersedes its topology while porting its hard-won mechanics verbatim (§9).
**Supersedes:** the pre-2026-07-14 IbkrConduit `ship-backlog` skill.

---

## 1. The loop this delegates

Draining a **loop-ready backlog** (`.claude/rules/backlog-format.md`) into merged code, unattended — DAG-ordered, one story at a time through implement → review → fix → gate → merge, sweeping (not interrupting) any story that won't converge, and **surviving all night** across compactions, rate-limits, API errors, and a hard-dead session.

**Trigger:** on-demand — the operator points it at a loop-ready backlog/stream ("drain Stream RPD," "ship these while I sleep"). Not scheduled.

**The problem it fixes:** the legacy skill orchestrated in the **main LLM session**. Over a long backlog its context fills, hits auto-compact, and it "forgets what it was doing." This design moves orchestration off any accumulating context — the same problem the legacy skill's ledger-less wave loop was exposed to, worse, since IbkrConduit's genuine-run gate (a full `dotnet test` pass, ~hundreds of WireMock/mock-WebSocket tests) is itself a multi-minute foreground cost that used to run *inside* the orchestrating session's turn budget.

---

## 2. Architecture at a glance

Two artifacts + two run-scoped resources, split by execution-fit:

| Piece | Kind | Owns |
|---|---|---|
| **`ship-story.mjs`** | Workflow-tool JS script (deterministic; no LLM context) | build ONE story to review-CLEAN: approach-check → impl → lens panel → fix. Sheds all churn; returns a terse verdict. |
| **`ship-backlog` driver-skill** | main-loop instructions (thin; ledger-sole-memory) | DAG walk, invoke `ship-story.mjs` per story, the **local serialized gate** (background bash), flake-vs-real judgment, merge, sweep, observability, lease/heartbeat. |
| **`ship-run-ledger.md`** | durable file (per run, main checkout) | the single source of truth AND the human-glanceable board. |
| ~~**cron watchdog**~~ | ~~`CronCreate` routine~~ | ❌ not adopted — `CronCreate` is session-only (RTOS bug #9, §6); fault recovery is in-session `ScheduleWakeup` heartbeat + ledger-based external restart. |

**Why two artifacts, not one workflow:** the local heavy gate (a full offline `dotnet test` pass — hundreds of WireMock/mock-WebSocket tests, several minutes) needs main-loop background bash — a Workflow *script* has no bash, and a subagent stalls on a multi-minute foreground run. So orchestration is a deterministic script (kills context accumulation) but gate+merge stays a thin skill-driven main-loop (the only place the heavy local gate runs).

### End-to-end flow

```
DRIVER (main-loop, ledger-sole-memory)                  ship-story.mjs (deterministic)
──────────────────────────────────────                 ──────────────────────────────
re-derive state from ledger ──▶ pick next buildable ──▶ workflow(story):
   ▲                                                       [std-risk] Opus approach-check (1 revision)
   │                                                          ↓
   │                                                       impl agent (TDD, scoped test, own worktree)
   │                                                          ↓
   │                                                       assign lenses (marker floor + globs + greps)
   │                                                          ↓
   │                                                       PARALLEL lens panel (L1..L7, per-lens model)
   │                                                          ↓
   │                                                       adjudicate → fix ≤2 (full-panel re-review)
   │              terse {branch,pr,CLEAN|DEFERRED,verdict} ◀──┘   (all churn discarded inside)
   ▼
rebase on main ──▶ LOCAL serial gate (bg bash) ──▶ green: squash-merge, ledger:done, fetch
                          │                          flake: re-run solo
                          └▶ fail → judgment agent ──┤ real:  sweep (permanent Deferred)
update ledger + heartbeat + emit status block ──▶ loop while buildable remain ──▶ finalize
```

---

## 3. The ledger — `ship-run-ledger.md` (durable board + machine source)

One file per run, in the **main checkout** (never a worktree). It is BOTH the driver's parse source (D5: the driver's *sole* working memory) AND the human-glanceable board (D13). Markdown so it serves both.

### 3.1 Run metadata + summary header (top of file)

```markdown
# SHIP RUN — <backlog label>
run_id: ship-2026-07-14-rpd
backlog_files: docs/backlog.md
started_at: 2026-07-14T20:00:00Z
lease: { session_id: <id>, phase: building, phase_started_at: <ts>, heartbeat_at: <ts> }

## ✅ 2 merged · 🔧 0 deferred · 🔁 1 retry-pending · ⏳ 4 remaining · of 7   ❤️ 2m ago
▶ NOW: RPD-03 · GATE (~6m) │ NEXT: RPD-05, RPD-06
DEFERRED: (none yet)
RETRY-PENDING: RPD-02 (rate-limit @20:41 → next pass)
```

### 3.2 Per-story table

| Column | Values / meaning |
|---|---|
| `id` | story id (e.g. `RPD-03`) |
| `deps` | comma id-list from the backlog `Depends on:` (drives the DAG) |
| `risk` | `standard` \| `high` (from the entry's `Risk:`) |
| `phase` | `pending` · `building` (ONE phase covers the whole ship-story run — the workflow is opaque to the driver, so there are no observable review/fixing/built sub-phases) · `gating` · `merged` · `deferred` · `retry-pending` |
| `branch` | `feat/<id>-<short-slug>` — **computed by the driver at row creation** (deterministic) and passed to every ship-story attempt as `branchName`, so retries reuse one branch/PR |
| `pr` | PR number/url |
| `review` | `CLEAN` \| `<n> findings` \| `—` — a `retry-pending` row that kept `CLEAN` (a gate-time infra retry) re-enters at Gate, no rebuild |
| `gate` | `pending` · `green` · `flake-recovered` · `real-fail` |
| `attempts` | INFRA attempt count for this story; **at 3 → `deferred (infra cap)`** (the livelock guard) |
| `wf_run` | the last ship-story Workflow runId — enables same-session `resumeFromRunId` retry (completed stages replay from the journal cache) |
| `updated` | ISO-8601 |
| `notes` | sweep/defer/retry reason; CLEAN nits/author-deviations; free text |

The driver reconstructs the entire run from this table + `git log main` + `gh pr list` + backlog `Status:` lines. **Every state transition updates the row AND the heartbeat.**

---

## 4. The driver-skill (main-loop procedure)

Full procedure lives in `.claude/skills/ship-backlog/SKILL.md` (this doc is the rationale; that skill is the operative instructions — where they disagree, the skill wins). Summary:

### 4.1 Entry (every invocation — interactive or a restart re-invocation)

1. **Read the ledger.** None → fresh run: run **Pre-flight**. Exists → **claim-or-yield the lease** (§6.2): fresh heartbeat ⇒ another driver is live ⇒ **exit immediately**; stale ⇒ claim it, continue.
2. **Emit the status block** (§7).

### 4.2 Pre-flight (fresh run only)

1. **Verify the loop-ready contract per story** (`backlog-format.md`): merged `Spec` (path or `trivial-skip`, not `pending`), `Risk` set, `Done when` refined, no open fork, upstream-behavior dependencies verified (a recording or probe the spec cites — `.claude/rules/contract-design.md`). Not loop-ready → **bounce**: `phase: deferred`, note `not loop-ready`, defer dependents. Never guess a missing spec/decision.
2. **Build the ledger** from the backlog: one row per story, `deps` parsed, `risk` read, `📦` marker noted (feeds `frozenMarker` — see §8), `phase: pending`, a deterministic `branch`.
3. **Arm the in-session heartbeat** (`ScheduleWakeup` re-entry cadence — §6.2).
4. **Claim the lease.**

### 4.3 The loop (until the DAG is drained)

1. **Re-derive** from the ledger. Buildable = `pending`/`retry-pending` stories whose every `dep` is `merged`. None buildable and none in flight → **Finalize** (§4.5).
2. **Pick the next buildable story** (DAG order; fresh `pending` ahead of `retry-pending`; ties by backlog order). Set `phase: building`, heartbeat.
3. **`Workflow({ scriptPath: ".claude/workflows/ship-story.mjs", args: {…} })`** (background; await the notification). On return: `INFRA`/died → retry-pending (attempts cap 3 → `deferred (infra cap)`); `DEFERRED` → code sweep; `CLEAN` → Gate.
4. **Gate + merge** (§5).
5. **Update ledger + heartbeat + emit status.** Loop.

### 4.4 Sweep vs interrupt (ported from legacy, unchanged)

- **Default on per-story trouble is SWEEP** — backlog follow-on, PR stays a **draft**, story + dependents `deferred`, loop continues.
- **INTERRUPT ONLY for a systemic blocker**: shared infra genuinely down (not a rate-limit — that's `retry-pending`), destructive/ambiguous state, security/secret/**credential** exposure, or **any lane about to touch a live IBKR session** (the offline boundary — see §5, IbkrConduit-specific and non-negotiable). Never for one non-converged story.

### 4.5 Finalize

DAG drained → one `workflow_dispatch` CI pass on `main` (the single sanctioned heavy CI dispatch). Release the lease. Post the **Brief**: shipped (PR#s) · deferred (+why) · retry-pending-that-never-cleared (+why) · blocked. Sweep all follow-ons into the backlog.

---

## 5. The gate (driver-side, strictly serial — the one heavy lane)

All gate work happens in a **dedicated worktree** via `git -C` — the driver never runs `git checkout`/`rebase`/`pull` in the main checkout (a build agent can move the main checkout's HEAD out from under it; RTOS's "driver immunity" finding, ported verbatim as a hard rule here too).

- **On workflow CLEAN:** create the gate worktree (`git worktree add <dir> origin/<branch>`), rebase on current `origin/main` **inside it** (near-trivial — serial build means nothing else is in flight; resolve a routine backlog-file line-flip keep-both if any), `git -C <dir> push --force-with-lease origin HEAD:<branch>`.
- **Run the gate via a main-loop background bash** (reliably notifies on completion, unlike a stalling subagent), **in that worktree**:
  1. `dotnet build --configuration Release -nodeReuse:false` (the `-nodeReuse:false` stops persistent MSBuild server nodes leaking across a long run).
  2. **Run the built MTP test executable directly** (NOT `dotnet test` — it discovers 0 and exits green, per `.claude/rules/test-filtering.md`) with **`--minimum-expected-tests <N>`** — the genuine-run guard. `N` = the previous green gate's reported total (first gate of the run: the last known suite count from the ledger's prior run or a fresh full-suite baseline read) — a ratchet, so a silently-shrunk discovery fails loudly; a story that legitimately removes tests lowers the floor (note it in the ledger).
  3. `dotnet format --verify-no-changes`.
  4. If the story touched `tools/IbkrConduit.Setup/` (`KeyGenerator.cs`, `CredentialFile.cs`, or a command invoking key/DH generation): also run the explicit KeyGenerator suite (`.claude/rules/explicit-tests.md`) in this same serial lane.
- **STRICT SERIAL HEAVY — cardinal rule:** the gate and any impl/fix scoped test run must **never overlap** (box-starvation/timing-flake class — this repo's own memory records a full-run flake from exactly this: leaked dispose loops + real-timer parallelism contention, PVR-13/15/12, fixed by the `DisableParallelization` collection in #263). With serial build this holds by construction; still, before a gate confirm nothing else heavy is running.
- **The offline boundary — IbkrConduit-specific, non-negotiable, ported from the legacy skill:** the merge gate is the **offline** suite only. No lane, agent, or gate step in this entire loop ever sets `IBKR_CONSUMER_KEY`, reads `.ibkr-credentials/`, or opens a live IBKR session. `[EnvironmentFact]` E2E tests auto-skip without credentials — that skip is correct and expected, never a failure to chase. Live-paper verification is **attended-only** (grooming's job, before a story ever reaches this loop); a story whose `Done when` genuinely needs live confirmation should never have passed grooming — if one slipped through, bounce it (`deferred — needs attended live verification`), never improvise a live session from the loop.
- **Outcomes:**
  - **green** → `gh pr ready <pr>` (the impl opened it as a *draft*; mark ready ONLY now, on green) → confirm `build-and-test` CI green → squash-merge (curated Conventional-Commits subject — see Release discipline) → `phase: merged`, `git fetch origin` (worktrees + `gh` — no `git pull`/`checkout` in the main checkout), remove the gate + story worktrees.
  - **fail** → flake-vs-real judgment agent (Opus, high — reads the failure so it never enters the driver's context): `flake` → re-run the gate **SOLO** locally; still ambiguous → one `gh workflow run ci.yml --ref <branch>` clean-runner disambiguation. **Real / reproduces on the clean runner** → `phase: deferred` (real regression — never fix-looped, never merged). Rate-limit/infra during the gate → `attempts += 1`, `phase: retry-pending` **keeping `review: CLEAN` + branch/PR** — the re-pass goes straight back to Gate, never a rebuild.
- **Local-first CI discipline:** the local serial gate is authoritative; reserve GitHub CI for flake-disambiguation + the single Finalize pass (CI-minute conservation). CI's `build-and-test` job already excludes `Category=Slow` — the local gate's full run is the one that includes them.

---

## 6. Fault recovery (D12)

⚠️ **`CronCreate`/`ScheduleWakeup` are session-only** (in-memory, die with the session — RTOS's empirically-verified bug #9) — there is **no** in-tool mechanism to auto-resurrect a hard-dead session. Realistic fault recovery is two-legged:

### 6.1 In-session heartbeat + backoff (while the session lives)

`ScheduleWakeup` re-enters the driver on a heartbeat cadence and backs off exponentially on a rate-limit — the session survives a rate-limit and resumes from the ledger.

### 6.2 Lease + phase-aware heartbeat (prevents double-drive; bounds resurrection lag)

- The ledger `lease` holds `{session_id, phase, phase_started_at, heartbeat_at}`.
- The driver **writes `heartbeat_at`** at every loop-iteration boundary and every state transition, **and on every `ScheduleWakeup` wake — including while a background ship-story or gate is still running.**
- **Staleness is HEARTBEAT AGE, never phase duration**: stale ⇔ `now − heartbeat_at` exceeds the current phase's ceiling — `building` 60m (one phase spans the whole `ship-story.mjs` run; heartbeats continue via wakes during it, cadence ~10–15m), `gating` 25m, `pending`/idle 25m. `phase_started_at` is informational only — a legitimately long build with fresh heartbeats is **LIVE**, never a takeover target.
- Worst-case resurrection lag = one phase ceiling past the last heartbeat — vs losing the whole run.

### 6.3 A hard-dead session must be re-invoked externally

The operator (or a `/goal` Stop-hook) re-invokes the skill. Because *all* state lives in the ledger, that restart **resumes cleanly** — re-derive from ledger + `git log main` + `gh pr list`, continue from the first incomplete row.

### 6.4 Infra-vs-code failure distinction (load-bearing)

- **Infra** (rate-limit, API error, terminal `agent()` null, an impl self-blocked on an *environmental* cause — `blockedKind: 'infra'`, gate-time transient) → `phase: retry-pending`, retried a later pass, **never** swept as a code failure. **Bounded at 3 attempts → `deferred (infra cap)`** (the livelock guard — a story failing identically every pass must not spin the loop indefinitely). Same-session retries prefer `resumeFromRunId` (only the dead stage re-runs).
- **Code** (2 fix rounds non-convergence, a fix agent that could not push, an impl blocked on the story itself, a real gate regression) → `phase: deferred` (permanent for this run; PR stays draft; dependents deferred).
- **Exponential backoff** on rate-limits: back off FIRST, so one rate-limit window doesn't burn the attempt cap.

---

## 7. Observability (D13) — the in-session status block

Rendered from the ledger, emitted on **every transition and each wake**, and on demand ("status?"):

```
SHIP RUN — Stream RPD · started 20:00 · ❤️ updated 2m ago
  ✅ 2 merged · 🔧 0 deferred · 🔁 1 retry-pending · ⏳ 4 remaining · of 7
▶ NOW: RPD-03 · phase GATE (~6m) │ NEXT: RPD-05, RPD-06
DEFERRED: (none yet)
RETRY-PENDING: RPD-02 (rate-limit @20:41 → next pass)
```

The **ledger file is the anchor** (any session — live or a restart — writes it, so it's never stale). An Artifact dashboard is an optional best-effort mirror only, never the source of truth.

---

## 8. `ship-story.mjs` — the per-story workflow

`args` = the story's `{ id, title, risk, deps, specPath, doneWhen, backlogFile, frozenMarker, branchName, baseBranch }`. Builds ONE story to review-CLEAN; **does NOT gate or merge** (the driver does). Returns terse `{ id, branch, pr, status: 'CLEAN' | 'DEFERRED' | 'INFRA', verdict }`.

### 8.1 Pipeline (all inside one story's `isolation:'worktree'`)

1. **[standard-risk only] Approach-check** — an Opus advisor reviews the builder's short TDD plan → `approve | redirect`. One revision, then proceed. High-risk stories skip this (their builder is already Opus).
2. **Impl agent** — full TDD (red→green→refactor, self-review, `dotnet build -nodeReuse:false && dotnet format --verify-no-changes` clean, a **SCOPED foreground** run of its own new class(es) via the built MTP exe + `--minimum-expected-tests` — NOT the full suite), draft PR, flip the backlog `Status` + add the `Completes:` trailer (`backlog-status.md`).
3. **Assign lenses** (deterministic JS): always-on **L1/L2/L6**; marker floor (📦 → L4; `Risk:high` → L3/L4/L5/L7 unconditionally); path/content diff facts from an independent mechanical classifier (never the author's self-report). Err toward firing.
4. **Parallel lens panel** — each assigned lens = one read-only, zero-test reviewer agent, own throwaway worktree, model/effort per §8.3.
5. **Adjudicate** — merge-ready iff no `blocking` finding from any lens. Safety lenses (L3/L4/L5/L7) are blocking-only (no nit tier); quality lenses (L1/L2/L6) may mark `nit` (recorded, non-blocking).
6. **Fix loop ≤ 2 rounds** — full triggered panel re-runs each round (catches fix-induced regressions). Still blocking after round 2 → `DEFERRED`.

### 8.2 The lenses — adapted for IbkrConduit's stack

| Lens | Adversarial mandate | Fires when |
|---|---|---|
| **L1 Spec fidelity** | fully satisfies `Done when` + the merged spec? what's missing/half-built? | always |
| **L2 Correctness** | find the input/state that breaks the logic — edges, error/null, the algorithm | always |
| **L3 Test integrity** | hermeticity (`.claude/rules/testing.md` + this repo's own leaked-dispose/real-timer-parallelism precedent, PVR-13/15/12/#263), the mandatory 401-recovery test on any new integration-tested endpoint, false-green guards, genuine-run (MTP exe + `--minimum-expected-tests`, never `dotnet test`) | tests/`*Operations.cs`/Refit interfaces touched |
| **L4 Permanence & wire contract** | breaking-vs-additive semver was **decided at grooming** — carry it, never re-decide in the story; nullable-as-presence (ADR-0001) on wire-optional fields; `[JsonPropertyName]` mapping correctness; money/quantity fields stay `decimal`, never `double`/`float`; `CancellationToken` propagated end-to-end (`.claude/rules/code-style.md`) | 📦-marked story, or diff touches a DTO/companion Models file/public method signature |
| **L5 Tenancy & isolation** | no new global/static mutable state; per-tenant session/rate-limiter/credential isolation preserved (`.claude/rules/architecture.md`); no cross-tenant bleed via a shared cache/singleton/DI registration | diff touches `SessionManager`, rate limiting, `TenantContext`, or DI registration (`AddIbkrClient`, `ServiceCollection` extensions) |
| **L6 Conventions & contract** | `.claude/rules/code-style.md` + `design-patterns.md` (positional records, companion `I{InterfaceName}Models.cs`, strategy-over-conditional), zero-warnings (`build-quality.md`), central package versions | always (cheap) |
| **L7 Security** | credentials/tokens/key material never in code or fixtures (synthetic only — `.claude/rules/security.md`); log output sanitized; OAuth 1.0a signing/crypto correctness; untrusted-input safety (Refit response/error-body deserialization) | auth/signing/credential/external-input/logging touched |

**Dropped from RTOS's original 7-lens content** (not applicable to this repo): Wolverine handler/saga discipline, Marten aggregate/projection/async-daemon hermeticity, event-evolution frozen-shape/`[MessageIdentity]` rules, conjoined multi-tenancy race safety, OpenAPI `schema.d.ts` client regen, web/`e2e` gating. IbkrConduit has no message bus, no event store, no generated web client, and no `web/` directory — L3–L5 and L7 are re-grounded in this repo's own rule files (`testing.md`, `architecture.md`, `security.md`, ADR-0001) rather than ported verbatim.

### 8.3 Model + effort — baked in

| Agent | model | effort |
|---|---|---|
| Impl — standard-risk | `sonnet` | high |
| Impl — `Risk: high` | `opus` | xhigh |
| Fix | *matches the story's impl tier* | high |
| Approach-check advisor (std-risk only) | `opus` | high |
| L2 · L4 · L5 · L7 | `opus` | high (**xhigh** for L4/L5) |
| L3 | `opus` | high |
| L1 | `sonnet` | high |
| L6 | `haiku` | medium |
| Flake-vs-real judgment (driver-side) | `opus` | high |
| Driver (session) | Opus 4.8 (or newer, incl. Fable 5) | — |

### 8.4 Worktree / isolation discipline (ported — load-bearing)

Every agent — impl, fix, and reviewer — runs in its **own** `isolation:'worktree'`, **never the shared main checkout**. Fix agents edit an existing branch: load it inside the worktree (`git fetch && git reset --hard origin/<branch>`), push by refspec (`git push origin HEAD:<branch>`) — never `cd` to the main checkout, never `git checkout <branch>` by name. The ledger lives in the MAIN checkout, never a worktree.

---

## 9. Ports verbatim vs changes (vs the legacy IbkrConduit skill)

**Port VERBATIM:** the genuine-run guard (built MTP exe + `--minimum-expected-tests`, never `dotnet test`); worktree isolation discipline (§8.4); the local serialized heavy gate + strict-serial-heavy rule; flake-vs-real disambiguation (local-solo → clean-runner); the `backlog-status` flip + `Completes:` trailer; sweep-don't-interrupt semantics; **the offline boundary** (§5 — IbkrConduit-specific, carried forward as a hard rule with no RTOS analog).

**CHANGES:** orchestration **topology** (deterministic `ship-story.mjs` + thin ledger-sole-memory driver, not a single-session wave orchestrator); **serial per-story** build, concurrency 1 (replaces the legacy's ≤3-concurrent wave cap — the same rationale that justified ≤3, agent-API rate limits + suite contention, argues *more* strongly for serial, since IbkrConduit's own full suite is hundreds of WireMock/mock-WebSocket tests that box-starve under concurrent runs); the **7 content-assigned review lenses** (re-grounded in this repo's rules, §8.2) replacing the risk-scalar panel; baked-in per-agent model/effort; the shift-left approach-check advisor for standard-risk; **lease/heartbeat fault recovery** (no cron watchdog — session-only, proven unreliable); the ledger-as-board observability.

---

## 10. Definition of done (this spec)

An implementer can build `ship-story.mjs` + the driver-skill + the lease/ledger mechanics from §§3–8 without a further decision. Open *implementation* details left to the builder (not decisions): the exact glob/grep patterns per lens, the precise `--minimum-expected-tests` floors, the status-block Markdown rendering.

---

## Appendix — Decisions & rationale (D1–D13, inherited from RTOS's design; re-affirmed for this port)

- **D1 — Orchestrator = the Workflow *script*** (deterministic JS; no LLM context to compact). LLM agents are leaves only.
- **D2 — Local serial gate + thin main-loop driver.** The gate needs main-loop background bash a script can't do.
- **D3 — superseded by D4.**
- **D4 — Serial per-story build (concurrency 1); reviewers parallel.** Build parallelism only compresses the low-value build axis while adding box-flakes + concurrent divergence. Serial keeps one story in flight (near-trivial rebase, zero sibling conflict).
- **D5 — Ledger is the driver's SOLE working memory.** Re-derive every iteration; compaction/restart/resume-later are non-events.
- **D6 — Fully autonomous auto-merge** (std + high). Wake only for a systemic blocker; finalize Brief.
- **D7 — Seven distinct content-assigned review lenses** (replaces a vague risk-scalar panel); reviewers run ZERO tests.
- **D8 — Deterministic lens assignment** (marker floor + diff facts; no triage agent). Err toward firing.
- **D9 — By-finding adjudication** + full-panel re-review each fix round + safety lenses blocking-only; ≤2 rounds then sweep.
- **D10 — Per-agent model/effort baked in.**
- **D11 — Advisor pattern as a shift-left approach-check, standard-risk only.**
- **D12 — In-session heartbeat + backoff**; infra-vs-code retry distinction; lease + phase-aware heartbeat.
- **D13 — Ledger-as-glanceable-board + in-session status block.**
