# PVR-02 — Presence-preserving REST money DTOs (portfolio, account summary, event contracts)

**Story:** PVR-02 (`docs/backlog.md`) · **Findings:** WIR-3 (high, PLAUSIBLE), WIR-4 (medium, CONFIRMED) · **Decided by:** [ADR-0001](../../adr/0001-nullable-as-presence-wire-fidelity.md) + design doc §6.5 (incl. the D2 money-numeric rule: `decimal`, never `double`) · **Semver:** BREAKING — `feat!:` (public DTO property types change) · **Risk:** high (money wire fidelity — VCR-01 precedent)

## Decisions (all closed — recorded contract)

§6.5: a wire-optional field on a public DTO is nullable (`null` = not present / not parseable); money and quantity fields are `decimal` (`decimal?` when wire-optional), never `double`/`float`; empty-string→0 coercion is never applied to money/quantity/verdict fields.

## Scope

1. **`Position` / `LedgerEntry`** (`IIbkrPortfolioApiModels.cs`): money/quantity fields (position, mktPrice, mktValue, avgCost, avgPrice, realizedPnl, unrealizedPnl; every LedgerEntry balance field) become `decimal?` — absent/empty wire values map to `null`, not `0`.
2. **`AccountSummaryOverview` / `AccountSummaryCashBalance`** (`IIbkrAccountApiModels.cs`): all money fields retype `double` → `decimal?` (the shared empty-tolerant converters already handle the wire's string/empty forms).
3. **Event-contract strike/payout money fields** (`IIbkrEventContractApiModels.cs`): same retype per the §6.5 rule.
4. **Converter/census audit:** confirm every retyped field routes through the registered empty-tolerant decimal converters; no new converter types.
5. **Migration notes** in the PR body/release notes: property-type changes enumerated for RTOS re-pin (rides the single PVR breaking cut).

WIR-3's sparse-row trigger is unpinned upstream (PLAUSIBLE); the retrofit is safe under both answers — no live dependency.

## Out of scope

- Streaming DTOs (done in VCR-01) and Flex DTOs (PVR-09).
- The subaccounts2 response shape — PVR-03.

## Acceptance criteria

- A portfolio positions fixture with an omitted and an empty-string money field deserializes to `null` for both (never `0m`), and a populated field round-trips exactly (decimal precision preserved for a 15-significant-digit value that `double` would corrupt).
- Account-summary and event-contract fixtures: same presence + precision assertions across every retyped field.
- No remaining `double` money/quantity property on any public REST DTO (sweep test or analyzer-style reflection test over the models assemblies pins the §6.5 rule).
- Existing suites green with fixtures corrected where they enshrined fabricated zeros.

## Test plan (TDD)

Red tests per model family: presence (omitted/empty → null), precision (decimal-exact round-trip), and the reflection sweep pinning "no double money fields". WireMock DI-stack integration tests reuse existing endpoint fixtures with sparse-row variants added. All offline.
