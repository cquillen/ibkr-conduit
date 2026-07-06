# Backlog status — keep it current in the work PR

Story `Status:` lines live in the backlog (`docs/backlog.md` and any future `docs/*-backlog.md`) and are the **single source of truth** for whether a story is done. They drift when the "update status in the story's own PR" convention is forgotten. This rule makes the update part of finishing a story — never a separate PR, never a bot editing `main`.

*Ported from realtest-order-steward @ 758375ab, adapted for a contract-first wrapper library.*

## The rule

When a PR **completes** a backlog story, do BOTH of these **in that same PR**:

1. **Flip the story's `**Status:**` line** to `✅ Done — #<PR>` — the line directly under the story's `#### <id> — …` heading in its backlog file.
2. **Add a `Completes: <id>` trailer** to the PR body. One line; comma-separate a multi-story PR: `Completes: CDT-02, CDT-03`. IDs are the backlog heading IDs.

A PR that completes **no** story adds no trailer.

## What enforces it

**Convention-only today** — nothing in this repo's CI checks it yet; the pipeline skills (`ship-backlog`'s impl-agent contract, `groom-backlog`'s handoff) and PR review carry it.

Optional later additions (deliberately **not** built with the pipeline port):

- **A `backlog-status` CI check**: if the PR body has a `Completes:` trailer, fail the PR unless the diff flipped each listed story's Status line to `✅ Done`; no trailer → no-op, so infra/docs/spec PRs are unaffected. Reference implementation: the sibling repo `realtest-order-steward` — its `.github/workflows/ci.yml` `backlog-status` job + `scripts/check-backlog-status.sh`. When a story id appears in more than one backlog file (an active tracker plus a superseded backlog it consolidated), the check passes if **any** backlog file flips it in the PR — only the active tracker needs flipping.
- A `Completes:` field in `.github/pull_request_template.md`.
- A report-only audit skill that backstops a fully-forgotten trailer (RTOS: `backlog-status-audit`).

## Scope

- **Only the Done transition** is part of finishing a story. `In progress — <owner> #<PR>` stays a manual convention you set while the PR is open.
- Markdown is authoritative; automation (if later added) never edits `main` out-of-band and never opens a separate status PR.

## See also

- `.claude/rules/backlog-format.md` — the entry schema (incl. the closed `Status` set) this keeps current.
- `.claude/rules/workflow.md`, `.claude/rules/git-conventions.md` — the story → branch → PR lifecycle this slots into.
- `docs/backlog.md` "How to read this document" → Status values.
