# VCR-04 — Order-outcome classification & 401 replay gate

**Story:** VCR-04 (`docs/backlog.md`) · **Findings:** AMB-2 (high/PLAUSIBLE), AMB-3, AMB-4, WIR-4 · **Decides by:** [ADR-0003](../../adr/0003-order-post-replay-gate.md), design doc §9.9 · **Semver:** BREAKING-behavioral — `feat!:` (replay behavior change; new public error shape) · **Risk:** high (order placement/modification + auth)

**Empirical status (recorded honestly):** whether IBKR can process an order POST and then 401 is unpinned in either direction and cannot be induced on demand; ADR-0003's gate is safe under both answers, so the story does not build on the unverified behavior — it removes the dependence on it.

## Decisions (all closed — ADR-0003)

Order-mutating POSTs are excluded from automatic 401 replay and surface a dedicated ambiguous-outcome error; idempotent requests keep replay; all 2xx order-path responses classify through `ResultFactory`; 2xx-unparseable surfaces as a classified error.

## Scope

1. **Replay gate (AMB-2):** `TokenRefreshHandler` gains an order-mutating gate — method == POST and path matches `/iserver/account/*/orders`, `/iserver/account/*/order/*` (modify), or `/iserver/reply/*` → after re-auth, do **not** re-send; return a response the pipeline converts to the ambiguous error. GET and DELETE (cancel) keep today's replay.
2. **Ambiguous error shape:** new `IbkrAmbiguousOrderError` in the `IbkrError` taxonomy (`src/IbkrConduit/Errors/IbkrError.cs` pattern: immutable record, pattern-matchable) carrying endpoint, HTTP status of the original response, and whether re-auth succeeded. Meaning documented: "sent; outcome unknown; reconcile via order/trade queries before resubmitting."
3. **Reply classification (AMB-3):** `OrderOperations.ReplyAsync`'s 2xx path routes through `ResultFactory.FromResponse` (the convention every other order path follows), so the documented bare-object 200-OK reject (`docs/ibkr-error-patterns-report.md` §2.6) returns `Failure(IbkrHiddenError/IbkrOrderRejectedError)` with the reject text and raw body.
4. **Unrecognized 200 shapes (AMB-4):** the place/modify success branch guards the empty-array case and detects an array-wrapped `[{"error":"…"}]` reject (add an `Error` property or extension-data read on `OrderSubmissionResponse`), returning `Failure(IbkrOrderRejectedError)` with `RawBody` instead of throwing; the residual fallback exception message includes the raw body.
5. **Wire hardening (WIR-4):** `[JsonConverter(typeof(FlexibleStringJsonConverter))]` on `OrderSubmissionResponse.OrderId` and `Id`; `DeserializeReplyResponse` uses `IbkrRefitSettings.Options` (the hardened serializer). A 2xx body that still fails deserialization surfaces as a classified error type (not a raw exception) so consumers can map it to their ambiguous leg by type.

## Out of scope

- Opt-in replay for cOID-disciplined consumers — rejected in ADR-0003 (revisit only via a superseding ADR).
- Live-orders priming (VCR-05); presence semantics (VCR-01).

## Acceptance criteria

- A WireMock 401-then-success scenario on POST place/modify/reply yields the ambiguous error and **exactly one** upstream POST (no replay); the same scenario on GET live-orders and DELETE cancel replays and succeeds (the existing 401-recovery contract preserved for idempotent calls).
- Reply 200 with `{"error":"…"}` → classified refusal carrying the reject text; place 200 with `[{"error":"…"}]` or `[]` → classified failure with raw body, no `InvalidOperationException`.
- Place 200 with numeric `order_id` deserializes successfully.
- Every touched endpoint keeps/gains its mandatory 401-recovery integration test (`.claude/rules/testing.md`) — with the assertion updated to the gate semantics for order-mutating POSTs.

## Test plan (TDD)

Red tests from the findings' suggested regression tests (AMB-2/3/4, WIR-4) as WireMock scenario tests through the full DI stack; unit tests for the gate's path matcher and the new error record. Consumer-facing migration note drafted for the release (new error type to map).
