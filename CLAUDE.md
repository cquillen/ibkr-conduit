# IbkrConduit

C#/.NET client library for the Interactive Brokers Client Portal Web API with OAuth 1.0a authentication.

## Key References

- **Design document:** `docs/ibkr_conduit_design.md` — authoritative design reference for all implementation decisions
- **Implementation status:** `docs/implementation-status.md` — check at session start to know what's done and what's next
- **Specs, plans, and prompts:** `docs/superpowers/specs/` (committed design specs), `docs/superpowers/plans/`, and `docs/superpowers/prompts/`
- **ADRs:** `docs/adr/` — cross-cutting decisions going forward (lean, no backfill); see `.claude/rules/contract-design.md` for how they sit beside the design doc

## Backlog pipeline

Story work flows through `docs/backlog.md` via three project skills: **`draft-backlog`** (decompose a recorded design/findings doc into drafted entries) → **`groom-backlog`** (attended: close every fork, verify empirics against the paper account, set `Risk`, spec each story to loop-ready) → **`ship-backlog`** (unattended DAG build-and-merge; offline suite is the gate — never the live account). Entry schema: `.claude/rules/backlog-format.md`. When a PR completes a backlog story, flip that story's `**Status:**` line to `✅ Done — #<PR>` and add a `Completes: <id>` trailer to the PR body, per `.claude/rules/backlog-status.md`.

## Commands

- **Build:** `dotnet build --configuration Release`
- **Test:** `dotnet test --configuration Release`
- **Lint:** `dotnet format --verify-no-changes`
- **Full check:** `dotnet build --configuration Release && dotnet test --configuration Release && dotnet format --verify-no-changes`

## Rules

See `.claude/rules/` for detailed guidance on code style, build quality, git conventions, architecture, testing, and security.
