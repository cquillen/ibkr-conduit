# IbkrConduit

C#/.NET client library for the Interactive Brokers Client Portal Web API with OAuth 1.0a authentication.

## Key References

- **Design document:** `docs/ibkr_conduit_design.md` — authoritative design reference for all implementation decisions
- **Implementation status:** `docs/implementation-status.md` — check at session start to know what's done and what's next
- **Specs and plans:** `docs/superpowers/specs/` and `docs/superpowers/plans/`

## Commands

- **Build:** `dotnet build --configuration Release`
- **Test:** `dotnet test --configuration Release`
- **Lint:** `dotnet format --verify-no-changes`
- **Full check:** `dotnet build --configuration Release && dotnet test --configuration Release && dotnet format --verify-no-changes`

## Rules

See `.claude/rules/` for detailed guidance on code style, build quality, git conventions, architecture, testing, and security.
