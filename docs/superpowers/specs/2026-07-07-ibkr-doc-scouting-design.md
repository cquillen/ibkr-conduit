# IBKR Live Doc Scouting — design

**Date:** 2026-07-07
**Status:** Approved (brainstormed with operator; this spec records the validated design)
**Problem:** The locally saved IBKR doc mirrors (`docs/ibkr-web-api-spec.md`, `docs/ibkr-web-api-openapi.json`, `docs/ibkr-websocket-api-reference.md`, `docs/ibkr_oauth1.0a.md`, satellites) are bit-rotting — IBKR's documentation is a moving target, is inconsistent and scattered across several web surfaces, and no single source covers everything. The mirrors are also wired into the contract discipline as the "External ground truth" doc layer (`.claude/rules/contract-design.md`, `groom-backlog`, `writing-adrs`), so their rot leaks into design and grooming decisions.

## Decisions (closed during brainstorm)

| Fork | Decision |
|---|---|
| Doc strategy | **Live-first + evidence snapshots.** Live IBKR docs become the doc authority. When a scouted claim feeds a spec/ADR/grooming decision, a dated excerpt (URL + retrieval date + exact quotes) is saved into the repo as provenance. The big local mirrors stop being maintained and retire by attrition. |
| Consumers | **Attended stages + ad-hoc only.** Design passes, `draft-backlog`, `groom-backlog`, and operator ad-hoc questions invoke live scouting. `ship-backlog`'s unattended loop never scouts live — it builds from evidence files grooming already committed. |
| Registration | **Probe & characterize.** Registering a source fetches it live, maps structure and fetch strategy, records coverage and overlaps. Registry entries are immediately usable by the scout. |
| Evidence storage | **Evidence dir + inline cite.** Dated file per decision-feeding scout under `docs/ibkr-doc-evidence/`; consuming specs/ADRs cite the file path; the file cites the live URLs. |
| Architecture | **Approach A: two skills + scout agent + probe agent** (single-skill and deterministic-fetcher approaches considered and rejected — see Alternatives). |
| Probes | **In the scouting loop.** Live paper-account probes are a tier-2 step of `scout-ibkr-docs`; wire observations join the reconciliation and always beat doc claims. |

## Operator-supplied constraints

- **Sources arrive one at a time.** The operator provides each documentation web endpoint + a description; registration is incremental.
- **The OpenAPI JSON is a peer, not a parent.** It looks authoritative but has gaps. Absence from it proves nothing; presence in it still needs the sparseness lens.
- **All sources are large; the rest are HTML.** Each source gets its **own scout agent instance** — sources are never combined into one agent context.
- **Sparse responses are real.** WebSocket responses are *confirmed* sparse: a documented, valid field can be absent from a live frame. REST sparseness is *unconfirmed in either direction*. Evidence and probes must never treat "documented" as "always present" or "absent from a sample" as "doesn't exist."
- **Probe tooling latitude.** The probe agent may build custom code/tooling when the existing example apps / `tools/ApiCapture` don't fit the scenario (guardrails below).
- **Concurrency cap.** Max 2–3 concurrent subagents (operator's standing rule). Doc scouts fan out under the cap; probes are strictly serial.

## Architecture

```
operator hands over URL + description
        │
        ▼
[register-ibkr-doc-source]  ──appends──▶  docs/ibkr-doc-sources.md   (the registry)
   (attended skill)                              │
                                                 │ source selection
anyone asks a doc question                       ▼
        │                              [scout-ibkr-docs] (skill)
        └─────────────────────────────▶  picks N sources, fans out
                                            │  one ibkr-doc-scout agent PER SOURCE
                                            │  (≤3 concurrent)
                                            ▼
                                   reconcile: agree / conflict / gap
                                            │
                              wire-behavior question? ──yes──▶ [ibkr-live-probe] (serial, attended-gated)
                                            │                        │ captures → recordings/
                                            ▼                        ▼
                              decision-feeding? ──yes──▶ docs/ibkr-doc-evidence/YYYY-MM-DD-<topic>.md
                                            │no                    ▲
                                            ▼                      │ cited by
                                     answer in chat          specs / ADRs / grooming
```

Key properties:

- **One scout per source, always.** The scout agent's job is "read one large source deeply, answer one question about it." Cross-source reconciliation happens in the skill layer (main conversation), which sees only the scouts' distilled, quoted findings — never raw pages.
- **`documented ≠ verified` survives intact.** Doc scouting produces IBKR's *claims*. Recordings and probes remain the only verification tier. The scout replaces the bit-rotted mirror, not the empirical discipline.
- **The wire always beats the doc.** A claim-conflict resolved by probe is recorded as resolved-by-wire; a conflict no probe can safely test stays flagged as unresolved claim-conflict.

## Claim taxonomy (sparseness-aware)

Every field-presence claim in evidence and probe output uses this vocabulary:

| Status | Meaning |
|---|---|
| **documented + observed** | Doc claims it; wire showed it. Strongest case — still per-sample, never a per-message guarantee. |
| **documented, absent from samples** | *Not* a doc error. WS is confirmed sparse; absence in N frames refutes nothing, and documentation guarantees nothing per-frame. For REST, sparseness is unconfirmed — same caution applies until probed. |
| **observed, undocumented** | The wire wins; the doc gap is recorded in the evidence file. |
| **absent from both** | Weakly suggests non-existence; only this status may support a "probably doesn't exist" reading, and only with sample counts stated. |

Probes report presence as "observed in k of n samples," never "always present." One sample can prove presence-possible or shape; it can never prove always-present or absent.

## Component 1 — Registry: `docs/ibkr-doc-sources.md`

One entry per registered source:

```markdown
#### <id> — <title>
**URL:** <live URL>
**Kind:** html | openapi-json · **Fetch:** curl-ok | webfetch-ok | needs-browser · **Size:** ~<n>KB
**Registered:** YYYY-MM-DD · **Last verified:** YYYY-MM-DD
**Covers:** <topic list>
**Authority notes:** <per-source caveats — e.g. OpenAPI: "machine-readable but incomplete; absence proves nothing">
**Structure:** <single page vs tree, anchor scheme, JS-rendered?, quirks>
**Overlaps:** <ids of other sources covering the same topics + who tends to win>
```

`Fetch` + `Structure` are what the registration probe pays for — the scout reads them and knows how to get at the source without rediscovering it. `Overlaps` makes multi-source reconciliation cheap to plan. `Last verified` is bumped whenever a scout successfully uses the entry; a scout that finds the entry stale (moved page, changed structure) reports it, and the skill updates the entry.

## Component 2 — Agent: `ibkr-doc-scout` (`.claude/agents/ibkr-doc-scout.md`)

The doc-reading executor. Isolated context so large pages never pollute the main conversation.

- **Input:** ONE registry entry + the question. Never more than one source.
- **Fetch:** per the entry's `Fetch` strategy (curl / WebFetch / browser); targets anchors/sections from `Structure` where possible instead of whole-page reads.
- **Output contract (pinned in the agent definition):**
  - exact quotes with anchor/section locations, URL + retrieval date;
  - "IBKR states" strictly separated from "I infer";
  - explicit "not covered by this source" statements — report coverage gaps honestly, never pad;
  - field-presence statements use the claim taxonomy;
  - staleness flags when the live page disagrees with the registry entry's characterization.
- **Read-only:** the scout returns text; the skill layer writes evidence files and registry updates.

## Component 3 — Agent: `ibkr-live-probe` (`.claude/agents/ibkr-live-probe.md`)

The wire-observing executor. A sibling of the doc scout with a different contract:

- **Input:** a specific hypothesis to test ("does `GET /portfolio/{acct}/summary` return `cushion` for a flat paper account?"). Never open-ended exploration.
- **Environment:** the paper account via `.ibkr-credentials/ibkr-credentials.json`, through the existing surfaces first — example apps, `tools/ApiCapture`, the DI pipeline.
- **Safety tiers (hard-coded in the agent definition):**
  - read-only endpoints: probe freely;
  - anything that mutates (order place/modify/cancel, alert creation, suppression changes): requires the operator's explicit per-probe ack in the conversation; the agent refuses mutation probes it wasn't explicitly authorized for.
- **Session-serial, always.** Probes never run parallel with each other or with anything else holding the session (the competing-session behavior behind `[Collection("IBKR E2E")]`). Doc scouts fan out; probes queue.
- **Sparseness-aware protocol:** presence claims need multiple samples (different account states / times / instruments where feasible); report "observed in k of n samples."
- **Custom tooling latitude:** when existing surfaces don't fit, the probe agent may write custom probe harnesses, with guardrails:
  - custom probes go through the library's DI pipeline as a real consumer (`AddIbkrClient`) — no hand-rolled `HttpClient` + signing — *except* when the wire-form below the library is itself under test, which must be stated explicitly;
  - probe code is scratch by default (untracked scratch dir); promoted into `tools/ApiCapture` in its own commit only if it proves reusable;
  - credential hygiene: creds loaded from `.ibkr-credentials/`, never echoed into output; captures sanitized before saving.
- **Output:** raw sanitized captures into `recordings/` (the existing wire-evidence home); returns distilled observations + capture paths.

## Component 4 — Skill: `register-ibkr-doc-source` (attended)

Operator hands over URL + description →

1. probe the endpoint live: fetch strategy (curl / WebFetch / needs-browser), structure (single page vs tree, anchors), size, JS-rendering — if it needs browser rendering, record that, don't fight it;
2. map coverage topics;
3. check overlap/conflict against existing registry entries;
4. append the registry entry;
5. commit: `docs: register IBKR doc source <id>`.

## Component 5 — Skill: `scout-ibkr-docs`

1. Parse the question; select candidate sources from the registry (`Covers` + `Overlaps`).
2. Fan out one `ibkr-doc-scout` per selected source, ≤3 concurrent.
3. Reconcile: agreed / conflicts / gaps, with the OpenAPI JSON as one voice among several.
4. **Tier 2 (optional):** if the question turns on wire behavior — presence claims, conflicting sources, anything feeding a `Risk: high` story — propose specific probe hypotheses to the attending operator, then run `ibkr-live-probe` serially on the approved ones.
5. Answer in chat. If the answer feeds a spec/ADR/grooming decision, write the evidence file.

## Component 6 — Evidence files: `docs/ibkr-doc-evidence/YYYY-MM-DD-<topic>.md`

```markdown
# <topic> — IBKR doc evidence
**Question:** <what was asked>
**Date:** <retrieval date> · **Sources consulted:** <registry ids>

> ⚠️ Doc sections below record what IBKR's documentation CLAIMS as of the date above —
> claims are not wire-verified. Presence claims are never per-message guarantees:
> WS responses are confirmed sparse; REST sparseness unconfirmed.
> Wire sections cite recordings/ paths and sample counts.

## Per-source findings
### <source-id>  (retrieved YYYY-MM-DD, <anchor/section>)
> exact quoted text…
Scout's reading: <claim vs inference separated>

## Wire observations   (present when tier-2 probes ran)
- <hypothesis> → observed in k of n samples · capture: recordings/<path>

## Reconciliation
- **Agreed:** …
- **Conflicts:** <source A says X, source B says Y — resolved-by-wire / unresolved; what we act on and why>
- **Gaps:** <what no registered source covers>
- **Presence claims:** <each field-presence claim tagged with the claim taxonomy>
```

Specs/ADRs/grooming cite the file path; the file cites the live URLs. When IBKR moves a page, the quote + date survive.

## Pipeline touchpoints (same PR, surgical)

- `.claude/rules/contract-design.md` — "External ground truth" row: replace `docs/ibkr-web-api-spec.md` with "live IBKR docs via `scout-ibkr-docs` (registry: `docs/ibkr-doc-sources.md`) + `docs/ibkr-doc-evidence/` snapshots + `recordings/` + attended live probes."
- `.claude/skills/groom-backlog/SKILL.md` — the empirical-blocker step points at `scout-ibkr-docs` for the doc-claim side (evidence tiers unchanged).
- `.claude/skills/writing-adrs/SKILL.md` — the "nothing to decide" row updated the same way.

## Rollout / demotion of the mirrors

Each large mirror (`ibkr-web-api-spec.md`, `ibkr-web-api-openapi.json`, `ibkr-websocket-api-reference.md`, `ibkr_oauth1.0a.md`) gets a short deprecation banner at the top — "unmaintained snapshot as of ~<date>; live scouting via `scout-ibkr-docs` is the doc authority" — but stays in place. Nothing else breaks; they retire by attrition as evidence files accumulate. (JSON gets the banner via an adjacent note in the registry's OpenAPI entry and the contract-design edit, since JSON can't carry a markdown banner.)

## Verification

The end-to-end test is real usage: register the operator's first source via `register-ibkr-doc-source` (seeding the registry with a genuinely characterized entry), then run one real `scout-ibkr-docs` question against it and confirm the output contract (quotes, dates, taxonomy, honest gaps). Skill/agent files are docs — the offline suite is unaffected; `dotnet format` and build stay green because no code changes ship in this work unless probe tooling is later promoted.

## Alternatives considered

- **B — single skill, no custom agents:** one `ibkr-docs` skill with register+scout modes dispatching general-purpose agents with inline prompts. Rejected: the two procedures share only the registry, and the scout's citation discipline drifts when re-improvised per dispatch.
- **C — deterministic fetcher tooling:** `tools/DocScout` script + JSON registry, thin skill wrappers. Rejected: IBKR's doc surfaces are inconsistent (campus SPA pages, guides, changelog) — a fixed script is brittle exactly where the problem lives; the value is model-driven reading, not fetching.
