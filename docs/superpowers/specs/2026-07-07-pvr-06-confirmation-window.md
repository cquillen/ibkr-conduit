# PVR-06 — Serialized order-confirmation round & ambiguous invalidated-reply outcome

**Story:** PVR-06 (`docs/backlog.md`) · **Findings:** ORD-3 (medium, CONFIRMED), ORD-1 (medium, PLAUSIBLE) · **Decided by:** [ADR-0006](../../adr/0006-order-confirmation-window.md) (revised 2026-07-07 on probe evidence) + design doc §9.10 · **Semver:** BREAKING-behavioral — `feat!:` (operator-decided: placements now serialize through pending confirmations; reply 503s reclassify) · **Risk:** high (order placement)

## Decisions (all closed — ADR-0006 as revised)

Serialize the confirmation round in-process (per-account order lock held from confirmation-returning placement until reply/dismiss/timeout); configurable confirmation timeout releases the lock and marks that order's outcome ambiguous; a failed reply on an invalidated confirmation classifies as an **ambiguous order outcome** (ADR-0003 family — reconcile before resubmitting), never a definitive refusal, never a generic 503; every 2xx reply shape classifies (ORD-1).

## Evidence (2026-07-07 live probe, `recordings/order-probe-2026-07-07.log`)

Reply on an invalidated confirmation → `503 {"error":"Service Unavailable","statusCode":503}` (no invalidation marker → recognition is reply-endpoint + 503, contextual). The invalidated order **later became a live Submitted order** (released by confirming the other pending same-type question) — "refused → re-place" double-places. Question issuance is non-deterministic (identical order: no question in one run, `o354` the next).

Doc claims (re-groomed live, 2026-07-07 — `../../ibkr-doc-evidence/2026-07-07-order-reply-confirmation-suppression.md`): DOC-03 documents the reply-immediately obligation and the 503 on a stale acknowledgment, but its "Submitting other orders or other requests **will cancel the order**" half is falsified by the probe above — the doc-claimed semantics are exactly the double-place trap ADR-0006 designs out. DOC-01 documents the reply endpoint's 200 response as a `oneOf` **five** shapes — `orderSubmitSuccess`, chained `orderReplyMessage`, `orderSubmitError` (`{"error": "Order not confirmed "}`), `orderReplyNotFound` (`{"error": "reply id not found: '…'"}`), `advancedOrderReject` — the ORD-1 classification net (scope item 3) must cover all five; the three never-wire-observed shapes take fixtures from DOC-01's documented examples.

## Scope

1. **Lock scope change (`OrderOperations`):** when `PlaceOrderAsync`/`PlaceOrdersAsync`/`ModifyOrderAsync` returns `OrderConfirmationRequired`, the per-account semaphore is retained; `ReplyAsync` (confirm or reject) for that pending confirmation releases it on resolution (a reply that returns another question keeps the round open). A new `IbkrClientOptions.ConfirmationTimeout` (positive `TimeSpan`, validated like other options) bounds retention: on expiry the lock releases and the pending order's tracked outcome becomes ambiguous.
2. **Ambiguous classification:** a 503 on `POST /iserver/reply/{id}` classifies into the ADR-0003 ambiguous-outcome error family with reconcile-before-resubmitting guidance (message names the replyId and, when known, the cOID). A post-timeout `ReplyAsync` on the expired confirmation gets the same classification.
3. **Reply classification net (ORD-1):** `DeserializeReplyResponse`'s empty/whitespace/non-JSON 2xx shapes convert to classified `IbkrApiError`s carrying the raw body (widen the existing `JsonException`-only catch to cover the `InvalidOperationException` paths, or reshape the helper to return classifications).
4. **Surface docs:** `PlaceOrderAsync`/`ReplyAsync`/`OrderConfirmationRequired` XML docs state the serialized round, the timeout, and the ambiguous semantics; §9.10 is the recorded contract.

## Out of scope

- The 401 replay gate (ADR-0003, shipped VCR-04) — unchanged.
- Suppression flow (`SuppressMessageIds`) — PVR-14; suppression remains the throughput path for automated flows.

## Acceptance criteria

- With a confirmation pending, a concurrent second placement on the same account does not reach the wire until the first round resolves; it proceeds after reply, dismiss, or timeout (WireMock + fake-clock tests).
- A stubbed reply 503 surfaces as the ambiguous-outcome error (not `IbkrApiError(503)`), and its message carries reconcile guidance; a reply after timeout classifies identically.
- Reply-`false` resolves the round as a definitive refusal and releases the lock; a reply chaining a second question keeps the lock held until the chain resolves.
- Empty, whitespace, and HTML 2xx reply bodies each classify with raw body attached (no `InvalidOperationException` escapes).
- Each of DOC-01's five documented reply-200 shapes resolves the round correctly: success array resolves it, a chained `orderReplyMessage` keeps it open, and `orderSubmitError`/`orderReplyNotFound`/`advancedOrderReject` classify as errors (fixtures from the documented examples — see Evidence).
- A consumer that never replies: the account's next placement proceeds after `ConfirmationTimeout` (fake clock), with the abandoned order's outcome ambiguous.
- No test assumes a question always arrives (probe: non-deterministic issuance).

## Test plan (TDD)

Red tests: WireMock DI-stack scenarios for lock retention/release (reply, reject, chain, timeout via fake `TimeProvider`), 503 classification (fixture = the probe's exact body), ORD-1 shapes, and option validation. Concurrency pinned with deterministic gates (the VCR-08 test-gate pattern). All offline.
