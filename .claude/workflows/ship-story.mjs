// ship-story — build ONE loop-ready backlog story to review-CLEAN.
//
// Ported from realtest-order-steward @ bfec311a, adapted for a contract-first C#/.NET wrapper
// library (IbkrConduit). The deterministic per-story builder half of the two-artifact ship-backlog
// design (spec: .claude/workflows/ship-backlog.workflow.md). It does NOT gate or merge — the
// driver-skill (.claude/skills/ship-backlog) runs the serialized local heavy gate + merge.
// All impl/review/fix churn lives in discardable agents; only a terse verdict is returned,
// so the orchestrating driver's context never accumulates (D1/D5).
//
// INVOKED BY the driver as:
//   Workflow({ scriptPath: ".claude/workflows/ship-story.mjs", args: {
//     id, title, risk,            // risk: 'standard' | 'high'
//     deps,                       // comma id-list or 'none' (informational here; DAG is the driver's)
//     specPath,                   // merged spec path, or 'trivial-skip'
//     doneWhen,                   // the acceptance criteria text
//     backlogFile,                // docs/*-backlog.md holding the story's Status line
//     frozenMarker,               // true if the entry carries 📦 (public-surface/wire-mapping contract change)
//     branchName,                 // OPTIONAL deterministic feat/<id>-… branch. The driver computes it once
//                                 //   (ledger-row creation) and passes the SAME name on every attempt, so an
//                                 //   INFRA retry reuses the prior attempt's branch + draft PR (never orphans).
//     baseBranch                  // OPTIONAL base override (default 'main') — the sandbox testing seam.
//   }})
//
// RETURNS (terse): { id, branch, pr, status: 'CLEAN'|'DEFERRED'|'INFRA', verdict }

export const meta = {
  name: 'ship-story',
  description: 'Build one loop-ready backlog story to review-CLEAN (approach-check → impl → content-assigned lens panel → bounded fix); no gate/merge.',
  phases: [
    { title: 'Approach',  detail: 'standard-risk: Opus approach-check of the TDD plan' },
    { title: 'Implement', detail: 'TDD build in an isolated worktree; draft PR' },
    { title: 'Review',    detail: 'parallel content-assigned lens panel (read-only, zero tests)' },
    { title: 'Fix',       detail: 'bounded ≤2 rounds, full-panel re-review each round' },
  ],
}

// args may arrive as a JSON string (the runtime's round-trip form) OR an object — handle both.
// A corrupt string must NOT throw past this point: an uncaught parse error kills the workflow, the
// driver maps that to retry-pending, and the identical bad call gets retried — livelock fuel. → INFRA.
let s = {}
if (typeof args === 'string') {
  try { s = JSON.parse(args) } catch (e) {
    log(`ABORT — args arrived as a non-JSON string (${e}).`)
    return { id: 'unknown', branch: null, pr: null, status: 'INFRA', verdict: `malformed invocation — args string did not parse: ${e}` }
  }
} else {
  s = args ?? {}
}

// Input guard — bail BEFORE spawning any agent if the invocation is malformed.
{
  const missing = ['id', 'specPath', 'doneWhen', 'backlogFile'].filter((k) => !s[k] || s[k] === 'undefined')
  // risk drives the impl model AND the safety-lens floor — a silently-absent risk would downgrade a
  // high-risk story to the standard path, so an unknown value is malformed input, never defaulted.
  if (s.risk !== 'standard' && s.risk !== 'high') missing.push("risk (must be 'standard'|'high')")
  if (missing.length) {
    log(`ABORT — malformed args (missing/undefined: ${missing.join(', ')}). Received type=${typeof args}, keys=[${Object.keys(s).join(',')}]`)
    return { id: s.id || 'unknown', branch: null, pr: null, status: 'INFRA', verdict: `malformed invocation — missing args: ${missing.join(', ')}` }
  }
}
const HIGH = s.risk === 'high'
const IMPL_MODEL = HIGH ? 'opus' : 'sonnet'
const IMPL_EFFORT = HIGH ? 'xhigh' : 'high'
// Base branch a story builds/diffs against. Defaults to 'main' (production). Overridable via args
// for reusable end-to-end TESTING against a throwaway sandbox base.
const BASE = s.baseBranch || 'main'
// Deterministic branch (optional): the driver passes the SAME branchName on every attempt of a story,
// so an INFRA retry reuses the prior attempt's branch + draft PR (force-with-lease) instead of
// orphaning a second draft. Absent → the impl agent names the branch (feat/<id>-…) itself.
const PINNED_BRANCH = s.branchName || null

// ── Baked-in model/effort per lens ──────────────────────────────────────────────
const LENS_CFG = {
  L1: { name: 'Spec fidelity',              model: 'sonnet', effort: 'high' },
  L2: { name: 'Correctness',                model: 'opus',   effort: 'high' },
  L3: { name: 'Test integrity',             model: 'opus',   effort: 'high' },
  L4: { name: 'Permanence & wire contract', model: 'opus',   effort: 'xhigh' },
  L5: { name: 'Tenancy & isolation',        model: 'opus',   effort: 'xhigh' },
  L6: { name: 'Conventions & contract',     model: 'haiku',  effort: 'medium' },
  L7: { name: 'Security',                   model: 'opus',   effort: 'high' },
}
// Safety lenses are BLOCKING-ONLY — any finding blocks, no nit tier.
const BLOCKING_ONLY = new Set(['L3', 'L4', 'L5', 'L7'])

// ── Structured-output schemas ──────────────────────────────────────────────────
const PLAN = {
  type: 'object', additionalProperties: false,
  required: ['tddPlan', 'files', 'howItMeetsDoneWhen'],
  properties: {
    tddPlan: { type: 'string', description: 'The failing tests first, then the implementation shape.' },
    files: { type: 'array', items: { type: 'string' } },
    howItMeetsDoneWhen: { type: 'string' },
    rulesConsidered: { type: 'array', items: { type: 'string' } },
  },
}
const APPROACH_VERDICT = {
  type: 'object', additionalProperties: false,
  required: ['decision', 'notes'],
  properties: {
    decision: { type: 'string', enum: ['approve', 'redirect'] },
    notes: { type: 'string', description: 'If redirect: the specific course correction.' },
  },
}
// The impl agent reports the touched SURFACE as raw mechanical facts (it has the worktree +
// git). The script maps facts→lenses deterministically — no LLM judgment in assignment.
const IMPL_RESULT = {
  type: 'object', additionalProperties: false,
  required: ['outcome', 'branch', 'pr', 'filesByArea', 'scopedTests', 'guardsConfirmed'],
  properties: {
    outcome: { type: 'string', enum: ['built', 'blocked'], description: "'built' = branch pushed + draft PR opened; 'blocked' = could not implement — say why in blockedReason (never fabricate)." },
    blockedReason: { type: 'string' },
    blockedKind: { type: 'string', enum: ['infra', 'code'], description: "only when blocked: 'infra' = environmental/transient (network, registry, git/gh auth, tooling — the driver RETRIES it); 'code' = the story itself can't be implemented as specced (permanent for this run)." },
    branch: { type: 'string', description: 'the feat/<id>-… branch name; empty if blocked' },
    pr: { type: 'string' },
    filesByArea: { type: 'string' },
    scopedTests: { type: 'string', description: 'class(es) run + pass/fail + count' },
    guardsConfirmed: { type: 'string', description: 'genuine-run + false-green guard confirmation' },
    deviations: { type: 'string' },
    risksForReviewer: { type: 'string' },
  },
}
// Lens assignment is driven by an INDEPENDENT mechanical diff-classifier (Haiku/low), NOT the author's
// self-report — no LLM relevance-judgment gating safety-critical assignment.
const DIFF_FACTS = {
  type: 'object', additionalProperties: false,
  required: ['touchesTestsOrIntegrationSurface', 'touchesWireContractOrMoney', 'touchesSessionRateLimiterOrDI', 'touchesAuthInputSecretsOrLogging'],
  properties: {
    touchesTestsOrIntegrationSurface: { type: 'boolean' },
    touchesWireContractOrMoney: { type: 'boolean' },
    touchesSessionRateLimiterOrDI: { type: 'boolean' },
    touchesAuthInputSecretsOrLogging: { type: 'boolean' },
    evidence: { type: 'string', description: 'the matching paths/grep lines per true flag' },
  },
}
const FIX_RESULT = {
  type: 'object', additionalProperties: false,
  required: ['pushed', 'summary'],
  properties: {
    pushed: { type: 'boolean' },
    summary: { type: 'string' },
    scopedTests: { type: 'string' },
  },
}
const REVIEW_VERDICT = {
  type: 'object', additionalProperties: false,
  required: ['lens', 'verdict', 'findings'],
  properties: {
    lens: { type: 'string' },
    verdict: { type: 'string', enum: ['CLEAN', 'FINDINGS'] },
    findings: {
      type: 'array',
      items: {
        type: 'object', additionalProperties: false,
        required: ['severity', 'area', 'what', 'fix'],
        properties: {
          severity: { type: 'string', enum: ['blocking', 'nit'] },
          area: { type: 'string' },
          what: { type: 'string' },
          fix: { type: 'string' },
        },
      },
    },
  },
}

// ── Deterministic lens assignment ───────────────────────────────────────────────
function assignLenses(facts) {
  const set = new Set(['L1', 'L2', 'L6']) // always-on
  if (HIGH) { ['L3', 'L4', 'L5', 'L7'].forEach((l) => set.add(l)); return [...set] } // Risk:high floor → full scrutiny
  // marker floor (from the backlog entry — independent of any agent)
  if (s.frozenMarker) set.add('L4') // 📦 — public-surface/wire-mapping contract change
  // mechanical diff facts (from the INDEPENDENT diff-classifier — not the author; over-firing is safe)
  if (facts.touchesTestsOrIntegrationSurface) set.add('L3')
  if (facts.touchesWireContractOrMoney) set.add('L4')
  if (facts.touchesSessionRateLimiterOrDI) set.add('L5')
  if (facts.touchesAuthInputSecretsOrLogging) set.add('L7')
  return [...set]
}

const hasBlocking = (panel) =>
  panel.filter(Boolean).some((v) => (v.findings || []).some((f) => f.severity === 'blocking'))

function blockingSummary(panel) {
  const items = panel.filter(Boolean).flatMap((v) =>
    (v.findings || []).filter((f) => f.severity === 'blocking').map((f) => `[${v.lens}] ${f.area}: ${f.what} → ${f.fix}`))
  return items.join(' · ')
}

// Author self-report fields that are actually informative (drop the "none"/"n/a" filler).
const notable = (t) => (t && t.trim() && !/^(none|n\/a|nothing)\b/i.test(t.trim())) ? t.trim() : null

// ── Prompts (the "how to ship a story" expertise lives here + in the driver) ────
const RULES = 'Obey CLAUDE.md + all .claude/rules/. xUnit v3 + Shouldly; MethodName_Scenario_ExpectedResult naming (.claude/rules/testing.md).'
// CRITICAL isolation directive — a review agent `cd`'ing out of its worktree into the shared main
// checkout and running `git checkout` there would detach the orchestrator's HEAD (the RTOS lineage's
// first e2e driver test hit exactly this).
const ISOLATION = 'CRITICAL — WORKTREE ISOLATION: you are ALREADY inside an isolated git worktree (your current working directory). Do ALL git and file work HERE, in your cwd. NEVER `cd /repos/ibkr-conduit` or any absolute repo path, and never `git -C` a path outside your worktree — that is the ORCHESTRATOR\'S SHARED main checkout, and any checkout/reset/branch/commit there corrupts the entire run.'
// For read-only agents (reviewers, diff-classifier): inspect the branch with NO checkout.
const inspectNoCheckout = (branch) => `Inspect the branch WITHOUT any checkout: \`git fetch origin\`, then \`git diff origin/${BASE}...origin/${branch}\` (and \`git show origin/${branch}:<path>\` for a file's contents). A diff needs no checkout, and origin/${branch} is reachable from your worktree's shared object store after fetch. NEVER run \`git checkout\`.`
// The offline boundary — non-negotiable in every agent this script spawns.
const OFFLINE_BOUNDARY = 'The offline boundary is absolute: NEVER set IBKR_CONSUMER_KEY, read .ibkr-credentials/, or open a live IBKR session. [EnvironmentFact] E2E tests auto-skip without credentials — that skip is correct, never chase it or try to make it run.'

const planPrompt = () => `You are planning story ${s.id} — "${s.title}" (risk: ${s.risk}).
Spec: ${s.specPath}. Done when: ${s.doneWhen}.
Produce a SHORT TDD plan: the failing tests to write first, then the implementation shape, the files
you'll touch, and exactly how it satisfies "Done when". Name the .claude/rules/ that constrain it
(esp. contract-design, backlog-format's Risk criteria, code-style, testing, architecture's no-global-
state/multi-tenancy). Do NOT write code — plan only. ${RULES} ${OFFLINE_BOUNDARY}`

const approachPrompt = (plan) => `Adversarially review this TDD PLAN for story ${s.id} BEFORE any code is written.
Spec: ${s.specPath}. Done when: ${s.doneWhen}.
PLAN: ${JSON.stringify(plan)}
Judge: does the approach fully + correctly satisfy Done-when? Does it honor the wire-contract/nullable-
as-presence (ADR-0001), multi-tenancy/no-global-state, and hermeticity rules it will touch? Is there a
materially better or safer shape? Return decision 'approve' if the approach is sound, or 'redirect' with
the specific correction. Bias toward approve unless there is a real problem — this is a shift-left check,
not a rewrite.`

// Branch directive: pinned (retry-safe — reuse the prior attempt's branch + draft PR) vs impl-named.
const BRANCH_REF = PINNED_BRANCH || `a feat/${s.id.toLowerCase()}-… branch`
const branchDirective = PINNED_BRANCH
  ? `use EXACTLY the branch name ${PINNED_BRANCH} — a retry may find it already on origin with a prior attempt's commits and an open draft PR: reuse BOTH (rebuild on origin/${BASE}, push \`git push --force-with-lease origin HEAD:${PINNED_BRANCH}\`, and update the existing draft PR — find it via \`gh pr list --head ${PINNED_BRANCH}\` — rather than opening a second one)`
  : `create branch feat/${s.id.toLowerCase()}-… off it and push by refspec (\`git push origin HEAD:feat/${s.id.toLowerCase()}-…\`)`

const implPrompt = (approachNote) => `Implement story ${s.id} — "${s.title}" (risk: ${s.risk}) end-to-end via TDD.
${ISOLATION} First \`git fetch origin\` and base your work on origin/${BASE} (\`git reset --hard origin/${BASE}\`); do all commits HERE in your worktree; ${branchDirective}.
Spec: ${s.specPath}. Done when: ${s.doneWhen}. Backlog file: ${s.backlogFile}.
${approachNote ? `APPROACH CORRECTION to apply: ${approachNote}` : ''}
${OFFLINE_BOUNDARY}

Do the full loop:
1. TDD red→green→refactor. Write failing tests FIRST (assert the correct contract; a false-green guard test must fail red without the fix). ${RULES}
2. \`dotnet build --configuration Release -nodeReuse:false\` clean (the -nodeReuse:false stops persistent MSBuild server nodes leaking) + \`dotnet format --verify-no-changes\` clean (zero warnings, run as SEPARATE bash calls per .claude/rules/bash-usage.md — never chained with &&).
3. SCOPED test run ONLY (never the full suite — it stalls): run the BUILT MTP test executable directly
   (NOT \`dotnet test\` — it discovers 0 and exits green) filtered to your new class(es) via
   \`--filter-class\` with \`--minimum-expected-tests <N>\` (.claude/rules/test-filtering.md) — prove
   your new tests execute + go green. If you added an integration-tested endpoint, its mandatory
   401-recovery test (.claude/rules/testing.md) must be part of that scoped run.
4. Commit, push the branch, open a DRAFT PR with base ${BASE} (\`gh pr create --base ${BASE} --draft\`) —
   or, on a pinned-branch retry, update the branch's EXISTING draft PR instead of opening a second one.
   Flip the story's **Status** line in ${s.backlogFile} to '✅ Done — #<PR>' and add a 'Completes: ${s.id}'
   trailer to the PR body (.claude/rules/backlog-status.md).
5. Do NOT run the full suite. Do NOT gate or merge. (Review-lens assignment is computed independently — you do not report it.)
6. Set outcome='built' ONLY if you actually pushed ${BRANCH_REF} and a draft PR exists for it — report BOTH
   in branch/pr. Otherwise outcome='blocked' with a blockedReason AND blockedKind: 'infra' when the blocker
   is environmental/transient (network, registry, git/gh auth, tooling — the driver retries), 'code' when
   the story itself cannot be implemented as specced (spec gap/contradiction — the driver sweeps it).
   Never fabricate a branch/PR name.`

const diffFactsPrompt = (branch) => `You are a MECHANICAL diff classifier for story ${s.id} — NOT a reviewer. Make NO judgment about whether review is "needed"; report only raw facts from grep.
${ISOLATION}
${inspectNoCheckout(branch)} Read the file list via \`git diff --name-only origin/${BASE}...origin/${branch}\` and content via \`git diff origin/${BASE}...origin/${branch}\`.
Set each boolean literally from the diff (over-report if unsure — a false positive is a cheap extra review, a false negative hides a needed one):
- touchesTestsOrIntegrationSurface: any changed path under \`tests/\`, OR matching \`*Operations.cs\`, OR a Refit interface / companion models file (\`I*Api.cs\`, \`I*ApiModels.cs\`).
- touchesWireContractOrMoney: the diff adds/changes a DTO record's properties, \`[JsonPropertyName]\`/nullability, a \`decimal\`-typed money/quantity field, or a public method signature under \`src/IbkrConduit/Client/\`.
- touchesSessionRateLimiterOrDI: changes \`SessionManager\`, rate limiting, \`TenantContext\`, DI registration (\`AddIbkrClient\`, \`ServiceCollection\` extensions), or adds/touches a \`static\`/global field.
- touchesAuthInputSecretsOrLogging: adds/changes OAuth signing/crypto, credential handling, external-input parsing (Refit response/error-body deserialization), secrets/config, or logging.
Include the matching paths/lines as evidence. Run zero tests; write nothing. ${OFFLINE_BOUNDARY}`

const lensPrompt = (lens, branch, authorNote) => {
  const mandate = {
    L1: `SPEC FIDELITY: does the diff FULLY satisfy "${s.doneWhen}" and the merged spec (${s.specPath})? What is missing, half-built, or out of scope?`,
    L2: `CORRECTNESS: find the input/state that breaks the logic — edge cases, error/null handling, the algorithm itself. Be adversarial.`,
    L3: `TEST INTEGRITY: hermeticity traps (.claude/rules/testing.md — no network/file I/O in unit tests, WireMock-only integration through the full DI stack, mandatory 401-recovery test on any new integration-tested endpoint, [Collection("IBKR E2E")] on E2E classes) and this repo's own precedent failure classes (leaked dispose loops, real-timer test parallelism — the DisableParallelization collection), false-green guards (does each test fail RED without the fix?), and genuine-run (do the new tests actually execute — built MTP exe + --minimum-expected-tests, never \`dotnet test\`?).`,
    L4: `PERMANENCE & WIRE CONTRACT: was the breaking-vs-additive semver call for a 📦 story DECIDED AT GROOMING (never re-decided here)? Nullable-as-presence on wire-optional fields (ADR-0001)? Correct \`[JsonPropertyName]\` mapping? Money/quantity fields stay \`decimal\`, never \`double\`/\`float\`? \`CancellationToken\` propagated through the full async call chain (.claude/rules/code-style.md)? Any violation is BLOCKING (a silently-wrong or silently-breaking public surface).`,
    L5: `TENANCY & ISOLATION: no new global/static mutable state; per-tenant session/rate-limiter/credential isolation preserved (.claude/rules/architecture.md — multi-tenant by design, no global or static mutable state); no cross-tenant bleed via a shared cache, singleton, or DI registration. Violations BLOCKING.`,
    L6: `CONVENTIONS & CONTRACT: .claude/rules house style (code-style.md, design-patterns.md — positional records, companion I{InterfaceName}Models.cs, strategy-over-conditional), zero-warnings (build-quality.md), central package versions (Directory.Packages.props, no Version attribute on PackageReference). Style-only issues are 'nit'.`,
    L7: `SECURITY: credentials/tokens/key material never in code or fixtures (.claude/rules/security.md — synthetic values only), log output sanitized, OAuth 1.0a signing/crypto correctness, untrusted-input safety (Refit response/error-body deserialization). Violations BLOCKING.`,
  }[lens]
  const severityRule = BLOCKING_ONLY.has(lens)
    ? `This is a SAFETY lens: every finding is severity 'blocking' (no nit tier).`
    : `Mark each finding 'blocking' (correctness/contract break) or 'nit' (style / non-required path).`
  return `You are review lens ${lens} (${LENS_CFG[lens].name}) for story ${s.id}. READ-ONLY — run ZERO tests.
${ISOLATION}
${inspectNoCheckout(branch)}
${mandate}
${authorNote ? `AUTHOR SELF-REPORT (extra input — verify independently; it does NOT narrow your mandate): ${authorNote}` : ''}
${severityRule}
${OFFLINE_BOUNDARY}
Return your lens, a CLEAN/FINDINGS verdict, and actionable findings (area · what's wrong · specific fix).`
}

const fixPrompt = (branch, blockers) => `Fix the BLOCKING review findings on branch ${branch} for story ${s.id}.
${ISOLATION}
Load the branch INSIDE your worktree (\`git fetch && git reset --hard origin/${branch}\`),
fix, and push by refspec (\`git push origin HEAD:${branch}\`). Never \`git checkout <branch>\` by name.
BLOCKING findings: ${blockers}
Address each; keep tests green (SCOPED run only — built MTP exe + --minimum-expected-tests, never the full suite,
never gate/merge). ${RULES} ${OFFLINE_BOUNDARY}`

// ── The runPanel helper: parallel content-assigned lens agents ──────────────────
// A lens agent can occasionally fail to return a verdict (null — e.g. it didn't call StructuredOutput).
// Silently dropping it loses that lens's coverage. For a SAFETY lens (L3/L4/L5/L7) that is unacceptable —
// synthesize a BLOCKING verdict so the story cannot pass unverified (it re-runs next round; a persistent
// failure → correct DEFERRED). A dropped quality lens (L1/L2/L6) becomes a non-blocking nit.
const SAFETY_LENSES = new Set(['L3', 'L4', 'L5', 'L7'])
const failedLensVerdict = (lens) => SAFETY_LENSES.has(lens)
  ? { lens, verdict: 'FINDINGS', findings: [{ severity: 'blocking', area: 'lens-infra', what: `${lens} (safety) returned no verdict — coverage unverified`, fix: 're-run this lens' }] }
  : { lens, verdict: 'FINDINGS', findings: [{ severity: 'nit', area: 'lens-infra', what: `${lens} returned no verdict`, fix: 're-run this lens' }] }
// Rate-limit-safe fan-out: never more than MAX_CONCURRENT lens agents in flight — a full panel is 7,
// five of them opus.
const MAX_CONCURRENT = 5
async function waves(items, fn) {
  const out = []
  for (let i = 0; i < items.length; i += MAX_CONCURRENT) {
    const chunk = items.slice(i, i + MAX_CONCURRENT)
    out.push(...(await parallel(chunk.map((it) => () => fn(it)))))
  }
  return out
}
const runPanel = async (lenses, branch, authorNote) => {
  const raw = await waves(lenses, async (lens) => {
    const v = await agent(lensPrompt(lens, branch, authorNote), {
      label: `${lens}:${s.id}`, phase: 'Review',
      model: LENS_CFG[lens].model, effort: LENS_CFG[lens].effort,
      isolation: 'worktree', schema: REVIEW_VERDICT,
    })
    return { lens, v }
  })
  const got = new Set(raw.filter(Boolean).map((r) => r.lens))
  const out = raw.filter(Boolean).map(({ lens, v }) => v || failedLensVerdict(lens))
  for (const lens of lenses) if (!got.has(lens)) out.push(failedLensVerdict(lens)) // thunk itself died
  return out
}

// ══ Pipeline ═══════════════════════════════════════════════════════════════════

// Phase 1 — Approach-check (standard-risk only; high-risk builder is already Opus)
phase('Approach')
let approachNote = ''
if (!HIGH) {
  const plan = await agent(planPrompt(), { label: `plan:${s.id}`, phase: 'Approach', model: 'sonnet', effort: 'high', schema: PLAN })
  if (plan) {
    const verdict = await agent(approachPrompt(plan), { label: `approach:${s.id}`, phase: 'Approach', model: 'opus', effort: 'high', schema: APPROACH_VERDICT })
    if (verdict?.decision === 'redirect') approachNote = verdict.notes
    if (!verdict) log(`${s.id}: approach-check agent died — proceeding without it (advisory only)`)
  } else {
    log(`${s.id}: plan agent died — proceeding without the approach-check (advisory only)`)
  }
}

// Phase 2 — Implement (own worktree; draft PR)
phase('Implement')
const impl = await agent(implPrompt(approachNote), {
  label: `impl:${s.id}`, phase: 'Implement',
  model: IMPL_MODEL, effort: IMPL_EFFORT, isolation: 'worktree', schema: IMPL_RESULT,
})
if (!impl) return { id: s.id, branch: null, pr: null, status: 'INFRA', verdict: 'impl agent died (terminal)' }
// Don't trust a green — verify the impl ACTUALLY produced a branch + PR before reviewing (else the
// panel reviews an empty diff and false-greens).
if (impl.outcome !== 'built' || !impl.branch || !impl.branch.startsWith('feat/')
    || (PINNED_BRANCH && impl.branch !== PINNED_BRANCH) || !impl.pr) {
  // Infra-vs-code (the load-bearing retry rule): a self-reported 'blocked' is permanent (DEFERRED)
  // only when the STORY is the blocker (blockedKind='code' or unspecified). An environmental blockage
  // ('infra') and a built-report that violates the contract (missing/wrong branch or PR — an agent
  // malfunction) are both retryable → INFRA; the driver's attempt cap bounds the retries.
  const permanent = impl.outcome === 'blocked' && impl.blockedKind !== 'infra'
  const why = impl.outcome === 'blocked'
    ? `impl blocked (${impl.blockedKind || 'code'}): ${impl.blockedReason || 'no reason'}`
    : `impl reported built but violated the contract (branch=${impl.branch || 'none'}${PINNED_BRANCH && impl.branch !== PINNED_BRANCH ? ` ≠ pinned ${PINNED_BRANCH}` : ''}, pr=${impl.pr || 'none'})`
  return { id: s.id, branch: impl.branch || null, pr: impl.pr || null, status: permanent ? 'DEFERRED' : 'INFRA', verdict: why }
}
// The author's self-report feeds the REVIEWERS as extra input. It must never gate lens ASSIGNMENT, but
// "probe the author's own flagged risks" is real reviewer value.
const authorDeviations = notable(impl.deviations)
const authorRisks = notable(impl.risksForReviewer)
const implContext = [
  authorDeviations ? `deviations: ${authorDeviations}` : '',
  authorRisks ? `flagged risks: ${authorRisks}` : '',
].filter(Boolean).join(' · ')

// Phase 3 — independent mechanical diff-classifier → deterministic lens assignment → parallel panel → bounded fix
const ALL_LENSES = ['L1', 'L2', 'L3', 'L4', 'L5', 'L6', 'L7']
let lenses
if (HIGH) {
  lenses = assignLenses({}) // Risk:high floor is unconditional — the classifier couldn't change it, so skip the agent
} else {
  const facts = await agent(diffFactsPrompt(impl.branch), {
    label: `facts:${s.id}`, phase: 'Review', model: 'haiku', effort: 'low', isolation: 'worktree', schema: DIFF_FACTS,
  })
  lenses = facts ? assignLenses(facts) : [...ALL_LENSES] // detector died → fail-safe: full panel
  if (!facts) log(`${s.id}: facts-detector died → full panel`)
}
log(`${s.id}: lenses ${lenses.join(',')} (risk ${s.risk})`)

let panel = await runPanel(lenses, impl.branch, implContext)
let round = 0
while (hasBlocking(panel) && round < 2) {
  round++
  phase('Fix')
  let fix = await agent(fixPrompt(impl.branch, blockingSummary(panel)), {
    label: `fix:${s.id}:r${round}`, phase: 'Fix',
    model: IMPL_MODEL, effort: 'high', isolation: 'worktree', schema: FIX_RESULT,
  })
  if (fix && !fix.pushed) {
    // A no-push fix leaves the blockers standing — re-reviewing the identical diff would silently burn
    // the round (and up to 7 agents). One in-script retry; then treat as non-convergence, not infra.
    log(`${s.id}: fix r${round} pushed nothing (${fix.summary}) — one retry`)
    fix = await agent(`${fixPrompt(impl.branch, blockingSummary(panel))}
(RETRY — the previous fix attempt pushed nothing: "${fix.summary}". If the findings genuinely cannot be fixed, say so in summary and leave pushed=false.)`, {
      label: `fix:${s.id}:r${round}-retry`, phase: 'Fix',
      model: IMPL_MODEL, effort: 'high', isolation: 'worktree', schema: FIX_RESULT,
    })
  }
  if (!fix) return { id: s.id, branch: impl.branch, pr: impl.pr, status: 'INFRA', verdict: `fix round ${round} died (terminal)` }
  if (!fix.pushed) return { id: s.id, branch: impl.branch, pr: impl.pr, status: 'DEFERRED', verdict: `fix round ${round} could not push a fix (${fix.summary}); blockers stand: ${blockingSummary(panel)}` }
  // A fix can WIDEN the diff (a newly-touched DTO, a newly-touched session/DI surface) — re-classify and
  // grow the lens set monotonically (never narrow: an already-assigned lens stays). High-risk is already full.
  if (!HIGH && lenses.length < ALL_LENSES.length) {
    const refacts = await agent(diffFactsPrompt(impl.branch), {
      label: `facts:${s.id}:r${round}`, phase: 'Review', model: 'haiku', effort: 'low', isolation: 'worktree', schema: DIFF_FACTS,
    })
    // a dead re-classifier keeps the current set (it covered round 0) — degraded, never blocking
    if (refacts) for (const l of assignLenses(refacts)) if (!lenses.includes(l)) { lenses.push(l); log(`${s.id}: fix r${round} widened the diff → +${l}`) }
  }
  panel = await runPanel(lenses, impl.branch, implContext) // full-panel re-review catches fix-induced cross-lens regressions
}

if (hasBlocking(panel)) {
  return { id: s.id, branch: impl.branch, pr: impl.pr, status: 'DEFERRED', verdict: `unconverged after 2 fix rounds: ${blockingSummary(panel)}` }
}
const nits = panel.filter(Boolean).flatMap((v) => (v.findings || []).filter((f) => f.severity === 'nit').map((f) => `[${v.lens}] ${f.what}`))
const extras = []
if (nits.length) extras.push(`nits: ${nits.join('; ')}`)
if (authorDeviations) extras.push(`author deviations: ${authorDeviations}`) // surfaced for the driver's ledger notes
return { id: s.id, branch: impl.branch, pr: impl.pr, status: 'CLEAN', verdict: extras.length ? `CLEAN (${extras.join(' · ')})` : 'CLEAN' }
