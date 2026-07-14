# RPD-03 — Bracket/OCA per-leg outcome classification for `PlaceOrdersAsync`

**Spec date:** 2026-07-14 · **Story:** RPD-03 · **Risk:** high · **Semver:** `feat!:` (📦 breaking — `PlaceOrdersAsync`'s return type changes)
**Touches:** `src/IbkrConduit/Client/OrderOperations.cs` (`PlaceOrdersAsync`, new `ClassifyGroupResponses`), `src/IbkrConduit/Client/IOrderOperations.cs` (signature + XML doc). Implements [ADR-0008](../../adr/0008-bracket-per-leg-outcome-classification.md) (revised 2026-07-14) and design doc §9.11. Evidence: `recordings/rpd02-invalidchild-{a,b,c}-*/` (2026-07-14 live probe).

## Problem

`PlaceOrdersAsync`'s classifier (`ClassifyOrderResponses`, shared with single-order `PlaceOrderAsync`) reads only `responses[0]` and treats it as the group's single outcome, per the current (false) contract: *"IBKR returns a single result for the group (the parent's outcome)... Child order ids are NOT returned here."* A 2026-07-14 live probe (3 invalidity mechanisms against a bracket's child leg) found:

- Where IBKR returns an array at all, the array holds **one entry per leg** IBKR assigned an identity to — not a single group-level result. The child is structurally `responses[0]`, the parent `responses[1]`, in both array-shaped samples observed — but this is not a documented IBKR contract, and the fix must not classify by position.
- A leg's row can be a normal success shape (`order_id` + a live `order_status`), a sentinel-shaped rejection (`order_id="-1"`, `order_status="Failed"`), **or** a real-`order_id` row whose `order_status` is itself terminal and non-transmitting (`"Inactive"`, observed on a *parent's* row when the whole bracket was rejected together) — the last case is the dangerous one: it structurally passes today's "has an `order_id`" check and gets misreported as `OrderSubmitted`.
- Some invalidity mechanisms produce no array at all — a bare `{"error": "..."}` object (whole submission hard-rejected before any leg gets an identity). This case is **already handled correctly** by the existing whole-call classification (`ResultFactory`'s AMB-4 rule) and needs no change here.

## Design

### New per-leg outcome type

No new record types. A leg's outcome is `OneOf<OrderSubmitted, IbkrOrderRejectedError, IbkrAmbiguousOrderError>` — reusing three types that already exist (`OrderSubmitted`, and the two `IbkrError` subtypes from ADR-0003/ADR-0006). `PlaceOrdersAsync`'s return type becomes:

```csharp
Task<Result<OneOf<IReadOnlyList<OneOf<OrderSubmitted, IbkrOrderRejectedError, IbkrAmbiguousOrderError>>, OrderConfirmationRequired>>> PlaceOrdersAsync(...)
```

`PlaceOrderAsync` (single order) is **unchanged** — it keeps `ClassifyOrderResponses`/`ClassifyResponse` exactly as they are today.

### `ClassifyGroupResponses` (new method, `PlaceOrdersAsync`-only)

Given the deserialized `IReadOnlyList<OrderSubmissionResponse>` and the original `IReadOnlyList<OrderRequest> orders`:

1. **Empty array** → unchanged existing behavior: whole-call `IbkrOrderRejectedError` (AMB-4). Not touched by this story.
2. **Single question-shaped element** (`Id` + `Message` present, no `OrderId`) → unchanged existing behavior: `OrderConfirmationRequired` for the whole group (a question blocks every leg alike; there is nothing to break down per-leg yet).
3. **Otherwise, classify every element independently, preserving wire order** (never reorder, never key on index):
   - `element.Error is not null` → `IbkrOrderRejectedError(element.Error, rawBody, requestPath)`.
   - Else `element.OrderStatus` is in the terminal non-transmitting set (see below) → `IbkrOrderRejectedError($"Order not transmitted (status: {element.OrderStatus})", rawBody, requestPath)`, **even when `element.OrderId` is a real-looking value** — this is the fix for the dangerous case (a rejected parent must never surface as `OrderSubmitted` just because it carries an `order_id`).
   - Else `element.OrderId is not null` → `OrderSubmitted(element.OrderId, element.OrderStatus ?? "", element.LocalOrderId, element.OcaGroupId)` (existing mapping, unchanged).
   - Else (a row shape recognized as none of the above — defensive, not wire-observed) → `IbkrAmbiguousOrderError(null, "Unrecognized order-leg response shape", rawBody, requestPath, ReauthSucceeded: false)`.
4. **Leg-count shortfall:** if `responses.Count < orders.Count`, append `orders.Count - responses.Count` additional `IbkrAmbiguousOrderError` entries to the end of the classified list — one per unaccounted-for leg. **Known limitation, stated explicitly, not silently glossed over:** for a group with more than 2 legs, this cannot identify *which* specific requested leg(s) are missing, only that some count is — the wire evidence only covers 2-leg groups (1 parent + 1 child). A future finding on 3+-leg groups may refine this.

**Terminal non-transmitting `order_status` set** (internal constant, not a public enum — deliberately open/extensible, not closed): `"Failed"`, `"Inactive"`. Wire-observed values only; the classifier's defensive fallback (step 3's last bullet) exists specifically so an unrecognized future status doesn't silently misclassify as success — it degrades to `IbkrAmbiguousOrderError` instead, which is the safe direction.

### `PlaceOrdersAsync` call site

Replace the `ClassifyOrderResponses(...)` call with `ClassifyGroupResponses(apiResult.Value, orders, rawBody, requestPath)`. The `_submissionCount`/`_submissionDuration` telemetry recording stays gated on `result.IsSuccess` (true whenever the outer `Result` succeeds — i.e., IBKR accepted the *request*, independent of what each leg's outcome turned out to be, consistent with existing telemetry semantics elsewhere in this file). `TryRetainForConfirmation`'s signature needs no change — it already pattern-matches on the `OneOf`'s `OrderConfirmationRequired` arm, which is untouched by this story.

### XML doc correction

`IOrderOperations.PlaceOrdersAsync`'s doc comment currently states *"IBKR returns a single result for the group (the parent's outcome)... Child order ids are NOT returned here — query `GetLiveOrdersAsync` and correlate."* This becomes: the group returns a per-leg outcome list; each transmitted leg's `OrderSubmitted` carries its own `order_id` directly (no `GetLiveOrdersAsync` round-trip needed for the common case); `GetLiveOrdersAsync` correlation remains necessary only for a leg classified `IbkrAmbiguousOrderError` (the count-shortfall case) or to confirm the eventual live/filled state of a transmitted leg.

## TDD steps

All new tests target `ClassifyGroupResponses` directly (internal, unit-tested like its `ClassifyOrderResponses`/`ClassifyResponse` siblings) plus one WireMock integration test per the mandatory-401-recovery and real-shape-fixture requirements.

1. **Red — clean 2-leg bracket:** two elements, both real `order_id`/non-terminal `order_status` (fixture from `recordings/bracket/004-GET-iserver-account-orders.json`'s submission-response analog — or the 2026-06-28 `recordings/bracket/002-POST...json`). Assert a 2-element list, both `OrderSubmitted`. **Green:** implement steps 1–3 (empty/question/per-element mapping) minus the terminal-status check.
2. **Red — the dangerous case, wire-verified:** two elements exactly matching `recordings/rpd02-invalidchild-b-negprice/001-POST...json` — `{"order_id":"-1","order_status":"Failed",...}` then `{"order_id":"1760268467","order_status":"Inactive",...}`. Assert **both** elements classify as `IbkrOrderRejectedError` — neither surfaces as `OrderSubmitted`. This is the money-safety regression test for the original defect. **Green:** implement the terminal-non-transmitting-status check.
3. **Red — real-ID array via confirmation chain:** two elements matching `recordings/rpd02-invalidchild-c-mismatchconid/005-GET...json`'s final state (`order_id` real on both, one `PendingSubmit` with `parent_order_id`, one `PreSubmitted` with `local_order_id`). Assert both classify `OrderSubmitted`. **Green:** confirm no false-positive rejection on legitimate non-`Failed`/`Inactive` statuses like `PendingSubmit`/`PreSubmitted`.
4. **Red — empty array unchanged:** assert `ClassifyGroupResponses` on `[]` still produces the existing whole-call `IbkrOrderRejectedError` (no behavior change, regression guard).
5. **Red — question unchanged:** assert a single question-shaped element still produces `OrderConfirmationRequired` for the whole group (no behavior change, regression guard).
6. **Red — leg-count shortfall:** 3 requested orders (1 parent + 2 children), response array has only 2 elements. Assert the classified list has 3 entries: the 2 real ones classified normally, 1 trailing `IbkrAmbiguousOrderError`. Explicitly note in the test's Arrange comment that this scenario is a defensible generalization, not wire-observed (only 2-leg groups were probed).
7. **Red — defensive fallback:** a synthetic element with no `Error`, no recognized terminal status, no `OrderId` (a shape nothing above matches). Assert `IbkrAmbiguousOrderError`, not an exception and not a silent `OrderSubmitted` with empty fields.
8. **Red — bare-object hard reject still works end-to-end:** WireMock integration test replaying `recordings/rpd02-invalidchild-a-bogusconid/001-POST...json`'s bare `{"error": "..."}` shape through the full `PlaceOrdersAsync` call. Assert `Result.Failure` with `IbkrOrderRejectedError` — proves this story doesn't regress the already-correct no-array path.
9. **Red — end-to-end WireMock integration test, per `.claude/rules/testing.md`:** stub the sentinel-array shape (test 2's fixture) behind WireMock and drive the *public* `PlaceOrdersAsync` API (not just the internal classifier) to confirm the per-leg list surfaces correctly through the whole `Result`/`OneOf` stack.
10. **401 recovery (mandatory per `.claude/rules/testing.md`):** order-mutating POSTs are excluded from automatic 401 replay (ADR-0003) — so the required test is that a 401 on `PlaceOrdersAsync`'s POST surfaces the existing `IbkrAmbiguousOrderError` (ADR-0003's gate), unchanged by this story's per-leg classification. Confirms the two ADRs compose correctly rather than one masking the other.
11. **Refactor:** confirm `PlaceOrderAsync`'s existing tests are untouched and still green (its classification path is explicitly not modified); update `IOrderOperations.PlaceOrdersAsync`'s XML doc per the correction above; full order-path suite green.

## Done when

`PlaceOrdersAsync` returns a per-leg outcome list (`IReadOnlyList<OneOf<OrderSubmitted, IbkrOrderRejectedError, IbkrAmbiguousOrderError>>`) instead of a single collapsed result; a bracket submission with a rejected child — in either the "child sentinel, parent inactive" or "child sentinel, parent live" shape — never surfaces a rejected leg as `OrderSubmitted`; classification keys on each row's field signature, never array position; the empty-array and confirmation-question paths are unchanged; `PlaceOrderAsync`'s classification is untouched; the XML doc no longer claims child order ids are unavailable.

## Risk / semver

`Risk: high` — order placement/modification, the money-boundary surface this repo's review history treats with the highest scrutiny. `feat!:` — `PlaceOrdersAsync`'s return type changes for every caller, including the happy path; RTOS and any other bracket/OCA consumer must migrate. Should land in the same release-please cut as any other pending breaking stories in this stream, per this repo's usual breaking-set-batching convention (see VCR's "Release train" precedent, `docs/backlog.md`).
