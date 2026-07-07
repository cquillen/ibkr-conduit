# PVR-04 — Streaming mapper per-element isolation wave 2 & ssd row completeness

**Story:** PVR-04 (`docs/backlog.md`) · **Findings:** WIR-1 (high), PRB-3.2, PRB-3.3, WIR-5 (all PLAUSIBLE → shapes now live-pinned, see Evidence) · **Decided by:** VCR-03 pattern (per-element isolation, on `main` since #247) + design doc §12.5 (D6 `AccountSummaryRow` surface line) + ADR-0002 drop taxonomy · **Semver:** additive — `feat:` (new `AccountSummaryRow` members; mapper robustness is behavioral-internal) · **Risk:** high (delivery semantics — account-money frames)

## Evidence (2026-07-07 live probe, `recordings/streaming-probe-2026-07-07.log`)

Captured `ssd+DUO873728` frames: 135 rows per frame — 114 monetary rows `{key, currency, monetaryValue, severity, timestamp}` and **21 non-monetary rows `{key, value, severity, timestamp}`** (e.g. `{"key":"Cushion","value":"1",...}`) whose `value` today maps nowhere. Captured `sld+DUO873728` frames: 2 rows × 24 keys (`cashbalance`, `netLiquidationValue`, `realizedPnl`, `unrealizedPnl`, `settledCash`, market-value breakdowns, …). Sanitized WireMock/mock-WS fixtures derived from these captures carry the shapes into committed tests.

## Scope

1. **Per-element isolation** (the `TradeExecutionMapper.MapMany` materialize-then-yield + `onElementDropped` pattern) applied to: `OrderUpdateMapper.MapMany` (`sor`), `PnlUpdateMapper.MapMany` (`spl`), `AccountSummaryUpdateMapper.Map` and `AccountLedgerUpdateMapper.Map` row loops (`ssd`/`sld`). Dropped elements report through `StreamingOperations.RecordMapperDrop` (ADR-0002 taxonomy) with the wire topic.
2. **`AccountSummaryRow` completeness (PRB-3.3):** add `[JsonPropertyName("value")] string? Value` and a `[JsonExtensionData]` overflow bag, mirroring `AccountLedgerRow` — pinned to the captured non-monetary row shape.
3. **`MarketDataTickMapper` guards (WIR-5):** `_updated`/`conid` reads gain ValueKind tolerance (string-tolerant parse via the existing converter idioms); non-numeric unmapped keys land in `AdditionalData` (or the XML doc corrected if it stays unused — the captured `smd` frame carries `conidEx`, `server_id`, `6119`, `6509`).
4. **Money-field census extension:** the WIR-5/VCR-10 census signal (`RecordMissingMoneyField`) wires into the `ssd`/`sld` paths for their required money fields, as `sor`/`str` already have.

## Out of scope

- Topic routing/keying — PVR-01 (coordinate lanes; this story touches mappers + models, PVR-01 touches registries).
- REST DTO retypes — PVR-02.

## Acceptance criteria

- A `sor`/`spl`/`ssd`/`sld` frame with one malformed element delivers every other element, increments the drop counter (cause=mapper, correct wire topic), and logs once — pinned per topic via mock-WS tests.
- The captured non-monetary ssd row maps with `Value == "1"` (not lost), and unmapped row keys survive in the overflow bag.
- The captured sld row shape deserializes with its money fields intact; an absent required money field raises the census signal.
- An `smd` frame with a string `_updated`/`conid` maps instead of throwing.

## Test plan (TDD)

Red tests from sanitized probe-derived fixtures: mixed valid/malformed-element frames per topic; the Cushion-row pin; sld row pin; smd tolerance cases. Census/counter assertions via `MeterListener`. All offline.
