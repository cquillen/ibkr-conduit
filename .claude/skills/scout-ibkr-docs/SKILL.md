---
name: scout-ibkr-docs
description: Use when a question needs what IBKR's live documentation says — during design passes, draft-backlog, groom-backlog, or ad-hoc ("what does IBKR say about X?"). Also use when a doc claim needs wire confirmation via a live probe. Attended stages only; ship-backlog consumes committed evidence instead.
---

# Scout live IBKR docs

Answers a documentation question from the **live** IBKR doc sources registered in `docs/ibkr-doc-sources.md`, optionally confirms on the wire, and (when the answer feeds a decision) writes a dated evidence file. The local doc mirrors under `docs/` are deprecated snapshots — never cite them as authority.

**Everything a doc source yields is IBKR's CLAIM.** `recordings/` and attended probes remain the only verification tier (`.claude/rules/contract-design.md`).

## Procedure

1. **Select sources** by walking the registry's `Covers` and `Overlaps` fields. Prefer covering the question from every source that plausibly speaks to it — single-source answers are what the old mirrors gave us. No registered source covers it? Say so; suggest the operator register one (`register-ibkr-doc-source`).
2. **Fan out — one `ibkr-doc-scout` agent per source** (subagent_type: `ibkr-doc-scout`; it pins Sonnet). Max 2–3 concurrent. Pass each agent its full registry entry + the question. NEVER fetch/read a source in the main conversation — large pages destroy the controlling context; that's the whole reason the agent exists.
3. **Reconcile** the scouts' findings yourself (main context, small distilled inputs): what's agreed, what conflicts, what nobody covers. Apply the claim taxonomy below to every field-presence statement. Collect the scouts' `Possible new doc sources:` flags (in-content deferrals to unregistered IBKR pages — scouts are told not to crawl or fetch them), dedupe across scouts, and **surface them to the operator** with the deferring quotes; registering is the operator's call (`register-ibkr-doc-source`). Precedent: DOC-08 (Order Types) was found via DOC-03/DOC-05 deferral sentences during the 2026-07-07 PVR re-groom.
4. **Tier 2 — wire probes (optional).** If the question turns on actual wire behavior — presence claims, source conflicts, anything feeding a `Risk: high` story — write specific hypotheses and propose them to the operator (this skill runs attended). For approved hypotheses, dispatch `ibkr-live-probe` **serially — one at a time, never parallel with anything**. Mutating probes need the operator's explicit per-call ack in your dispatch prompt; the agent will refuse otherwise.
5. **Answer.** If the answer feeds a spec/ADR/grooming decision, write the evidence file (template below) and cite its path in the consuming doc. Chat-only answers still carry URLs + retrieval dates.
6. **Registry upkeep:** bump `Last verified` on entries scouts used successfully; a scout's staleness flag (moved page, changed structure/version, dead fetch strategy) → update that entry now.

## Claim taxonomy (every presence claim gets one)

| Status | Meaning |
|---|---|
| documented + observed | Doc claims it; wire showed it. Still per-sample — never "always present". |
| documented, absent from samples | NOT a doc error. WS is confirmed sparse; REST sparseness unconfirmed. Refutes nothing. |
| observed, undocumented | Wire wins; record the doc gap. |
| absent from both | Weakly suggests non-existence — only with sample counts stated. |

**The OpenAPI JSON is a peer, never the authority.** It is known-incomplete: absence of an endpoint/field from it proves NOTHING. "Not in the OpenAPI schema" ≠ "not part of the contract" — the baseline that wrote exactly that sentence was wrong. Presence in it still needs the sparseness lens. Conflicts between sources are resolved by the wire (probe) or recorded as unresolved — never by picking the shinier source.

## Evidence file — `docs/ibkr-doc-evidence/YYYY-MM-DD-<topic>.md`

NOT `docs/findings/` (adversarial reviews) and NOT `docs/superpowers/specs/`.

```markdown
# <topic> — IBKR doc evidence
**Question:** <what was asked>
**Date:** <retrieval date> · **Sources consulted:** <registry ids>

> ⚠️ Doc sections record what IBKR's documentation CLAIMS as of the date above — not wire-verified.
> Presence claims are never per-message guarantees: WS is confirmed sparse; REST sparseness unconfirmed.
> Wire sections cite recordings/ paths and sample counts.

## Per-source findings
### <source-id>  (retrieved YYYY-MM-DD, <anchor/section>)
> exact quote…
Scout's reading: <claim vs inference separated>

## Wire observations        <!-- when tier-2 probes ran -->
- <hypothesis> → observed in k of n samples · capture: recordings/<path>

## Reconciliation
- **Agreed:** …
- **Conflicts:** <A says X, B says Y — resolved-by-wire / unresolved; what we act on and why>
- **Gaps:** <what no registered source covers>
- **Presence claims:** <each, tagged with the taxonomy>
```

## Red flags — stop and re-read this skill

- About to fetch an IBKR page in the main conversation
- "The OpenAPI spec doesn't list it, so it doesn't exist"
- Writing evidence into `docs/findings/` or citing a deprecated local mirror as authority
- "Documented as returned" drifting into "always present"
- Two probes queued in parallel, or a mutation probe without the operator's named ack
- An answer with no URL + retrieval date attached
