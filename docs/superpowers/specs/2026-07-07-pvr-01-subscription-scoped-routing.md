# PVR-01 — Subscription-scoped streaming topic routing

**Story:** PVR-01 (`docs/backlog.md`) · **Findings:** PRB-1.1, PRB-1.2, PRB-3.1 (all high, CONFIRMED), PRB-1.3 · **Decided by:** [ADR-0005](../../adr/0005-subscription-scoped-streaming-delivery.md), design doc §12.8/§12.5 · **Semver:** BREAKING-behavioral — `feat!:` (operator-decided 2026-07-07: cross-target delivery ends; unmatched frames drop observably) · **Risk:** high (delivery semantics)

## Decisions (all closed — ADR-0005)

Target-qualified solicited topics (`smd`, `ssd`, `sld`) register and dispatch by **full wire-topic identity** (`smd+265598`, `ssd+DUO873728`); target-less solicited (`sor`/`spl`/`str`) and unsolicited topics keep prefix routing; same-target duplicates fan out; unmatched target-qualified frames drop observably under a distinct cause; the facade validates and escapes consumer-supplied subscribe inputs.

## Evidence (2026-07-07 live probe, `recordings/streaming-probe-2026-07-07.log`)

The wire echoes full topic identities: captured frames carry `"topic":"smd+756733"`, `"topic":"ssd+DUO873728"`, `"topic":"sld+DUO873728"` — the full-topic match key exists on every target-qualified frame. Target-less frames echo bare topics (`sor`, `sts`, `system`, `act`, `tic`).

## Scope

1. **Registry keying (`IbkrWebSocketClient`):** `SubscribeTopicAsync` takes (or derives) the full topic identity — e.g. `smd+{conid}`, `ssd+{accountId}` — as the subscriber key instead of the bare prefix. `ProcessMessage` dispatch: exact full-topic match first; fall back to prefix match only for keys registered as prefix-scoped (target-less/unsolicited). Reconnect replay and unsubscribe refcounting operate on the same key.
2. **Facade keys (`StreamingOperations`):** `MarketDataAsync` registers `smd+{conid}`; `AccountSummaryAsync`/`AccountLedgerAsync` register `ssd+{accountId}`/`sld+{accountId}`; `sor`/`spl`/`str` and unsolicited registrations unchanged (prefix).
3. **Unmatched-frame observability:** a target-qualified frame whose full topic matches no live subscription increments the VCR-02 drop counter with a new cause (e.g. `cause="unmatched"`) and a first-per-topic Warning — never cross-delivered, never silent.
4. **Facade input validation (PRB-1.3):** conid/accountId topic-target segments reject `+`, whitespace, and empty via `ArgumentException` at the facade; `fields`/`keys` args objects are built with `JsonSerializer.Serialize` (escaped), not string interpolation.

## Out of scope

- ssd/sld row-shape completeness and per-element mapper isolation — PVR-04.
- Dispose/connect lock ordering — PVR-15/PVR-16 (same files; lane order PVR-15 → PVR-16 → PVR-01).

## Acceptance criteria

- Two concurrent `MarketDataAsync` subscriptions (conids X and Y) each observe only their own conid's ticks when the mock server broadcasts interleaved `smd+X` / `smd+Y` frames; same per-account for `ssd`/`sld`.
- Two subscriptions for the **same** conid both receive every frame (fan-out preserved), and the wire cancel still fires only on the last dispose (existing refcount pin stays green).
- A broadcast `smd+Z` frame with no `Z` subscription increments the drop counter (cause=unmatched) and delivers to no one.
- `sor`/`spl`/`str` and unsolicited topics deliver exactly as before (existing suites green).
- `MarketDataAsync`/`AccountSummaryAsync`/`AccountLedgerAsync` throw `ArgumentException` for target segments containing `+`, whitespace, or empty; a `fields` value containing `"` or `\` produces valid JSON on the wire.

## Test plan (TDD)

Red tests on the DI-stack `MockWebSocketServer` harness: cross-conid isolation, cross-account isolation, same-target fan-out + last-dispose cancel, unmatched-frame counter (`MeterListener`), reconnect replay under full-topic keys, input-validation unit tests. All offline.
