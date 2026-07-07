# VCR-05 — Live-orders priming surface & filters/sor interaction

**Story:** VCR-05 (`docs/backlog.md`) · **Findings:** GAP1-1 (high), GAP1-2 (high), GAP1-3 · **Decides by:** design doc §10.6 (operator-decided 2026-07-07; story-scoped, no ADR) · **Semver:** BREAKING — `feat!:` (return-type change) · **Risk:** high (order surface)

**Evidence (live captures, local per repo convention — `recordings/` is gitignored; committed test evidence lands as sanitized WireMock fixtures in this story):** `recordings/orders/001-002` (unprimed `snapshot:false` → primed `snapshot:true` two-call sequence); `recordings/priming/001-003` (live probe 2026-07-07: a **filtered** call returns fake-empty `snapshot:false` while cancelled orders demonstrably exist; `force=true` returns the documented blank array; the next plain call returns `snapshot:true` with all orders). The `sor`-suppression effect of filtered calls is documented (captured spec `docs/ibkr-web-api-spec.md:4150`) but not independently observable on demand — the chosen mechanism is defensive with respect to it (harmless if the suppression never manifests).

## Decisions (all closed — design doc §10.6)

Primedness becomes consumer-visible via a record return; the library owns the filters↔sor quirk by issuing the `force=true` follow-up itself.

## Scope

1. **Return shape (GAP1-1):** `GetLiveOrdersAsync` returns `Result<LiveOrdersSnapshot>` where `LiveOrdersSnapshot` is an immutable record `(IReadOnlyList<LiveOrder> Orders, bool IsSnapshot)` (`.claude/rules/design-patterns.md` shapes; co-located per the models-file convention). `OrdersResponse.Snapshot` maps through instead of being discarded. XML docs state: `IsSnapshot == false` means the cache was unprimed — an empty `Orders` is NOT evidence of no orders; call again.
2. **Auto-force after filtered calls (GAP1-2):** after any `GetLiveOrdersAsync` call that passed `filters`, the library issues a `force=true` follow-up (fire-and-forget through the same rate-limited pipeline, logged) to clear IBKR's cached filter behavior before `sor` subscription traffic is affected. XML-doc warnings land on the `filters` parameter and on `OrderUpdatesAsync` citing §10.6.
3. **Test truth (GAP1-3):** the existing `GET-live-orders-empty.json` fixture is renamed/annotated as the **unprimed** shape; new sanitized fixtures cover `snapshot:true` and the force-cleared blank array (derived from the 2026-07-07 `recordings/priming/` captures); a WireMock scenario test pins the unprimed-then-primed sequence and the filtered→force follow-up call.

## Out of scope

- Internal re-poll-until-primed — rejected (operator decision: primedness is surfaced, the consumer decides; auto re-poll adds latency/rate cost to every call).
- Verifying the `sor`-suppression effect end-to-end (timing/state-dependent; the mechanism does not depend on it — see Evidence).
- Order-outcome classification (VCR-04).

## Acceptance criteria

- `GetLiveOrdersAsync` surfaces `IsSnapshot` faithfully for the recorded unprimed/primed/force-cleared shapes (sanitized fixtures derived from the recorded shapes).
- A filtered call is followed by exactly one `force=true` request through the pipeline (WireMock asserts the call sequence); an unfiltered call triggers no follow-up.
- The unprimed-empty fixture no longer masquerades as the canonical "no orders" case; the scenario test covers unprimed→primed.
- 401-recovery test present for the endpoint per `.claude/rules/testing.md` (aligned with VCR-04's gate semantics: GET replays).
- Migration note drafted: return-type change + how to read `IsSnapshot`.

## Test plan (TDD)

Red tests first: `GetLiveOrders_Unprimed_SurfacesIsSnapshotFalse`, `GetLiveOrders_Primed_SurfacesOrdersAndIsSnapshotTrue`, `GetLiveOrders_Filtered_IssuesForceFollowUp` (WireMock scenario, full DI stack), plus the renamed-fixture truth test. Unit tests for the record mapping.
