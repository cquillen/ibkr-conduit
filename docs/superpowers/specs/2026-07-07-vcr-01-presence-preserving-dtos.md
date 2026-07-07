# VCR-01 — Presence-preserving wire DTOs (streaming + REST)

**Story:** VCR-01 (`docs/backlog.md`) · **Findings:** WIR-1 (critical), WIR-3, FIL-6, GAP2-2, GAP2-3 · **Decides by:** [ADR-0001](../../adr/0001-nullable-as-presence-wire-fidelity.md), design doc §6.5 · **Semver:** BREAKING — `feat!:` · **Risk:** high (delivery semantics / money fields)

**Evidence:** live-captured sparse `sor` frame (`tests/IbkrConduit.Tests.Unit/Streaming/OrderUpdateMapperTests.cs:13`); live capture with `"price": ""` on a filled market order and string-encoded decimals (`recordings/priming/003-GET-iserver-account-orders.json`); the findings' end-to-end traces.

## Decisions (all closed — ADR-0001)

Nullable-as-presence: every wire-optional field on the affected public DTOs becomes nullable; `null` = "not present in (or not parseable from) this frame/row". No fabricated verdicts from absence. Empty-string→0 coercion is confined to genuinely non-optional counters.

## Scope

1. **`OrderUpdate`** (`src/IbkrConduit/Streaming/StreamingModels.cs`): `Size`, `FilledQuantity`, `RemainingQuantity` → `decimal?`; `Conid` → `int?`; `Symbol`, `Side`, `OrderType`, `Status` → `string?` (drop the `string.Empty` defaults). `Price`/`OrderRef` already conform. XML-doc each: null = absent from this frame (WIR-1).
2. **`TradeExecution`**: `Size` → `decimal?`, matching the already-nullable `Price`/`NetAmount` (FIL-6, WIR-3).
3. **REST `Trade`** (`src/IbkrConduit/Orders/IIbkrOrderApiModels.cs`): `Size`, `Price` → `decimal?`; `OrderRef`, `Submitter` → `string?` (WIR-3 — the null-despite-non-nullable `OrderRef` NRE path).
4. **`LiveOrder`**: `FilledQuantity`, `RemainingQuantity`, `TotalSize` → `decimal?` (WIR-3).
5. **`SessionStatusEvent.Authenticated`** → `bool?`; the mapper maps an absent/args-less frame to `null`, never `new SessionStatusEvent()` with fabricated `false` (GAP2-2).
6. **`AccountStatusEvent.IsPaper` (and `IsFT`)** → `bool?` with tolerant boolean parsing, matching `SystemEvent.IsPaper`'s existing presence semantics (GAP2-3).
7. XML docs on every changed member state the null-means-absent contract.

## Out of scope

- `SessionStatusEvent` field *additions* (`Competing`, `FailReason`) — VCR-07 (depends on this story).
- Mapper enumeration robustness — VCR-03. Converter additions on `OrderSubmissionResponse` — VCR-04.
- Consumer-side merge helpers: the library exposes presence; merging is the consumer's.

## Acceptance criteria

- Deserializing the live-captured sparse `sor` frame yields `null` (not `0m`/`""`) for every omitted field (the findings' suggested regression test `OrderUpdateMapper_SparseFrame_OmittedFieldsAreNull`).
- A REST `Trade` row with `"size": ""` or omitted money fields yields `null`, not `0m`.
- An `sts` frame without `authenticated` yields `Authenticated == null`; an `act` frame without `isPaper` yields `IsPaper == null`; string-encoded `"true"` parses.
- No public DTO in the diff retains a non-nullable field the wire can omit (guard: review `DtoFieldMap`-covered DTOs touched here).
- `ResponseSchemaValidationHandler`/`DtoFieldMap` and existing fixtures updated where nullability changes their expectations; all offline suites green.

## Test plan (TDD)

Red tests first, from the findings' suggested regression tests per finding ID (WIR-1, WIR-3, FIL-6, GAP2-2, GAP2-3), using sanitized fixtures derived from the live captures where applicable. Update the pinned-default tests that asserted the old coercion. Integration fixtures: add/adjust WireMock cassettes for `Trade` rows with empty-string money fields (mirror `recordings/priming/003`). The 401-recovery rule applies to any endpoint whose integration tests are touched (`.claude/rules/testing.md`).
