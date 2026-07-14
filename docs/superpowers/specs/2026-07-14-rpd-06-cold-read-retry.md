# RPD-06 — Heuristic auto-retry-once on sparse first-read for Positions/Trades

**Spec date:** 2026-07-14 · **Story:** RPD-06 · **Risk:** standard · **Semver:** `fix:` (no public-surface change)
**Touches:** `src/IbkrConduit/Client/PortfolioOperations.cs` (`GetPositionsAsync`), `src/IbkrConduit/Client/OrderOperations.cs` (`GetTradesAsync`). Implements [ADR-0009](../../adr/0009-positions-trades-cold-read-retry.md) and design doc §10.7. Evidence: `recordings/coldread-rpd06/` (2026-07-14 live probe, verified 3/3).

## Problem

`GET /portfolio/{accountId}/positions/{pageId}` and `GET /iserver/account/trades` return thin/empty results on the first read of a session — Positions rows missing `name`/`ticker`, Trades an empty array — and reprime on an immediate follow-up call. Neither endpoint carries a wire-reported freshness flag (unlike LiveOrders' `snapshot: bool`), so consumers have no signal to distinguish a cold read from a genuinely-empty/thin one. The 2026-07-14 probe verified both the sparse-first-read behavior and the immediate-no-delay-retry-sufficiency assumption, 3/3, under homogeneous conditions.

## Design

### Sparse predicates (per endpoint, no shared abstraction — the two shapes are different enough not to force one)

- **Positions:** `positions.Count > 0 && positions.Any(p => string.IsNullOrEmpty(p.Name) || string.IsNullOrEmpty(p.Ticker))`. An **empty** positions list is NOT itself evidence of sparseness — the probe's cold-read case returned non-empty rows with missing fields, not an empty list — so a genuinely-zero-position account never triggers a retry from this predicate. (Aside, not fixed by this story: `Position.Name`/`Position.Ticker` are declared non-nullable `string` today; the wire omits them on a cold read regardless of the C# annotation, so the null-check here works at the runtime/JSON level independent of that separate ADR-0001 gap — flagged for whoever grooms `Position`'s field nullability next, not fixed here.)
- **Trades:** `trades.Count == 0`. Accepted false-positive: indistinguishable from a legitimately quiet trading day (ADR-0009's documented cost — one wasted retry, no data corruption).

### Retry, both methods identically shaped

No session-start tracking, no state — a purely local, per-call heuristic: after the first `ResultFactory.FromResponse` call, if `result.IsSuccess` and the sparse predicate is true, issue exactly one more call to the same Refit method (passing the same parameters and `cancellationToken`), re-run `ResultFactory.FromResponse` on *that* response, and **adopt the retry's result only when the retry itself succeeds — a retry HTTP failure (500/503/timeout/etc.) keeps the original first-read result instead of surfacing the retry's error.** (Amended 2026-07-14, attended grooming, after the ship-backlog run's first attempt shipped this behavior undisclosed and was correctly deferred for it — see "Retry-failure decision" below.) No second retry, no loop, per ADR-0009's "capped at one attempt" regardless of outcome. Set an `Activity` tag (`ibkr.cold_read_retry = true`) only when the retry actually fires (attempted, regardless of its own outcome); omit it otherwise (presence-as-signal, consistent with this repo's general DTO convention, applied here to diagnostics instead of wire fields).

```csharp
// GetPositionsAsync, after the existing ResultFactory.FromResponse call:
if (result.IsSuccess && LooksSparse(result.Value))
{
    activity?.SetTag("ibkr.cold_read_retry", true);
    var retryResponse = await _api.GetPositionsAsync(accountId, page, waitForSecDef: waitForSecDef, cancellationToken: cancellationToken);
    var retryResult = ResultFactory.FromResponse(retryResponse, retryResponse.RequestMessage?.RequestUri?.AbsolutePath);
    if (retryResult.IsSuccess)
    {
        result = retryResult;
    }
    // else: keep the original (sparse-but-valid) `result` — a failed retry must never turn an
    // already-successful read into a hard failure for the caller.
}
```

```csharp
// GetTradesAsync, after the existing ResultFactory.FromResponse call:
if (result.IsSuccess && result.Value.Count == 0)
{
    activity?.SetTag("ibkr.cold_read_retry", true);
    var retryResponse = await _orderApi.GetTradesAsync(days, cancellationToken);
    var retryResult = ResultFactory.FromResponse(retryResponse, retryResponse.RequestMessage?.RequestUri?.AbsolutePath);
    if (retryResult.IsSuccess)
    {
        result = retryResult;
    }
    // else: keep the original (empty-but-valid) `result` — same rationale as GetPositionsAsync.
}
```

No change to either method's return type, parameters, or the `Result<List<T>>` shape. `LogResult`/telemetry calls operate on the final (possibly-retried) `result`, unchanged from today's pattern.

### Retry-failure decision (ratified 2026-07-14, attended grooming)

**Decided:** on a retry HTTP failure, keep the first successful (sparse-but-valid) result rather than surfacing the retry's failure. Rationale: this heuristic exists purely to *improve* a read that already succeeded — an internal reliability optimization must never make the caller's outcome worse than if the optimization hadn't been attempted at all. A transient blip on the *optional* retry call should not turn an already-valid (if sparse) read into a hard `Result.Failure`.

**Rejected alternative:** unconditional adoption of the retry's result (win or lose), matching this spec's original literal pseudocode. Rejected because it lets a transient retry-call failure discard a genuinely successful first read, which is a reliability regression relative to not retrying at all — the exact failure mode `ibkr.cold_read_retry`'s bounded-cost design (§ADR-0009 Consequences) was trying to avoid, just triggered from the other direction (a failed retry instead of a false-positive sparse detection).

This decision was first implemented, undisclosed, by an unattended ship-backlog run (2026-07-14) — its review panel correctly deferred the story for shipping an undocumented behavioral deviation from this spec's literal text, per `.claude/rules/contract-design.md`'s rule that error-classification behavior is decided at spec/ADR authority, not silently by an implementer. This amendment closes that gap with the same outcome, now genuinely decided.

## TDD steps

WireMock scenario stubbing (call-count-sequenced responses) per `.claude/rules/testing.md`'s integration-test convention, plus fast unit tests for the sparse predicates.

1. **Red — Positions sparse predicate, unit:** table-driven cases — empty list (not sparse), one row missing `Name` (sparse), one row missing `Ticker` (sparse), fully populated row (not sparse), fixtures drawn from `recordings/coldread-rpd06/s1-positions-1.json` (sparse) and `s1-positions-2.json` (enriched). **Green:** implement `LooksSparse`.
2. **Red — Positions retry fires and returns enriched data, WireMock integration:** stub call 1 → `s1-positions-1.json` (sparse), call 2 → `s1-positions-2.json` (enriched). Assert `GetPositionsAsync`'s caller-visible result is the **enriched** data (retry is transparent) and exactly 2 HTTP calls were made.
3. **Red — Positions no retry when not sparse, WireMock integration:** stub call 1 → enriched data. Assert exactly **1** HTTP call was made (the heuristic must not fire on a clean read).
4. **Red — Positions retry capped at one attempt:** stub call 1 → sparse, call 2 (the retry) → **also** sparse. Assert `GetPositionsAsync` returns the still-sparse result from call 2 and makes exactly **2** calls total, never a third.
5. **Red — Trades retry fires and returns populated data, WireMock integration:** stub call 1 → `s1-trades-1.json` (`[]`), call 2 → `s1-trades-2.json` (2 trades). Assert the caller-visible result has 2 trades, 2 HTTP calls made.
6. **Red — Trades no retry when not empty:** stub call 1 → non-empty trades. Assert exactly 1 call.
7. **Red — Trades retry capped at one attempt:** stub call 1 → `[]`, call 2 → `[]` (a genuinely trade-free day). Assert `GetTradesAsync` returns `[]` after exactly 2 calls, not a third — proves the false-positive cost is bounded, not compounding.
7a. **Red — Positions retry HTTP failure keeps the first result** (`GetPositionsAsync_SparseFirstReadRetryFails_ReturnsFirstSuccessfulResult`): stub call 1 → sparse (200 OK), call 2 (the retry) → 500/timeout. Assert `GetPositionsAsync` returns `Result.Success` with the sparse-but-valid first-read data, NOT `Result.Failure` — the retry-failure decision above.
7b. **Red — Trades retry HTTP failure keeps the first result** (`GetTradesAsync_EmptyFirstReadRetryFails_ReturnsFirstSuccessfulResult`): stub call 1 → `[]` (200 OK), call 2 (the retry) → 500/timeout. Assert `GetTradesAsync` returns `Result.Success` with the empty-but-valid first-read list, NOT `Result.Failure`.
8. **Red — Activity tag observability:** using this repo's existing `ActivityListener`-capture test pattern, assert `ibkr.cold_read_retry` is present and `true` on the span when a retry fires (test 2/5) and absent when it doesn't (test 3/6).
9. **401 recovery (mandatory per `.claude/rules/testing.md`):** both endpoints are idempotent GETs, so they participate in the standard `TokenRefreshHandler` replay. Assert: first call 401s, `TokenRefreshHandler` re-authenticates and replays automatically (existing behavior, unchanged) — and if *that* replayed response also looks sparse, the retry-once-for-sparseness still fires on top, composing correctly with the pre-existing 401 replay rather than being bypassed or double-counted.
10. **Refactor:** confirm `GetPositionsAsync`'s and `GetTradesAsync`'s existing tests are untouched and green; full portfolio/order-read suite green.

## Done when

`GetPositionsAsync`/`GetTradesAsync` retry once internally on a heuristically-sparse first read per call (predicates per above), capped at one attempt, with the retry recorded on the method's `Activity` span (`ibkr.cold_read_retry`) when it fires; a failed retry (HTTP error) keeps the first successful result rather than surfacing the retry's failure; return types unchanged; the retry composes correctly with existing 401 replay.

## Risk / semver

`Risk: standard` — internal read-path behavior change, not order placement/modification, auth/signing, credential handling, or streaming delivery semantics. `fix:` — no public-surface change (return types, parameters, and the `Result<List<T>>` shape are all unchanged); purely an internal reliability improvement.
