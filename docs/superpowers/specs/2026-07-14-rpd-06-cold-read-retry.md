# RPD-06 — Positions cold-read auto-retry-once; Trades cold-read documented (no retry)

**Spec date:** 2026-07-14 (re-scoped 2026-07-14, attended design pass — see "Re-scope decision") · **Story:** RPD-06 · **Risk:** standard · **Semver:** `fix:` (no public-surface change)
**Touches:** `src/IbkrConduit/Client/PortfolioOperations.cs` (`GetPositionsAsync`), `src/IbkrConduit/Client/IOrderOperations.cs` (`GetTradesAsync` XML docs only). Implements [ADR-0009](../../adr/0009-positions-trades-cold-read-retry.md) (as revised 2026-07-14) and design doc §10.7. Evidence: `recordings/coldread-rpd06/` (2026-07-14 live probe, verified 3/3).

## Problem

`GET /portfolio/{accountId}/positions/{pageId}` and `GET /iserver/account/trades` return thin/empty results on the first read of a session — Positions rows missing `name`/`ticker`, Trades an empty array — and reprime on an immediate follow-up call. Neither endpoint carries a wire-reported freshness flag (unlike LiveOrders' `snapshot: bool`). The 2026-07-14 probe verified both the sparse-first-read behavior and the immediate-no-delay-retry-sufficiency assumption, 3/3, under homogeneous conditions.

## Re-scope decision (2026-07-14, attended design pass — operator-ratified)

The original spec gave **both** endpoints a retry. The second ship-backlog attempt (draft PR #305) implemented that and was deferred by the gate: `Concurrent401_TriggersSingleReauth` failed deterministically because the Trades retry's request received a second 401 and independently re-entered re-auth (2 LST acquisitions vs. the pinned 1). Attended reproduction traced the retry's 401 to a WireMock scenario-cycling artifact (a scenario whose terminal stub lacks `WillSetStateTo` finishes and resets, so the stub pair serves 401/200 alternately forever) — but the failure exposed real, unrecorded decisions, now closed in ADR-0009:

- **Trades loses its retry entirely** (ADR-0009 point 1a): an empty list is absence-of-data, indistinguishable from a quiet trading day, and the endpoint's own `1 req/5 secs` limiter turns every quiet-day misfire into an up-to-~5s stall. `GetTradesAsync` makes one call and returns what IBKR returns; the quirk and the consumer's re-read obligation are documented (XML docs + design doc §10.7) — consistent with `GetLiveOrdersAsync`'s inform-don't-act posture.
- **Positions keeps its retry unchanged** — its trigger is the probe-pinned *positive incompleteness* signature (non-empty rows missing `name`/`ticker`), which never fires on an empty account or a healthy read, and its path has no endpoint limiter.
- **The Positions retry participates in standard 401 recovery, unmarked** (ADR-0009 point 5): fail-closed markers and epoch inheritance were considered and rejected — a retry that 401s after the first read's recovery completed is a genuinely new failure, and a second re-auth is then correct.

**Supersedes draft PR #305**, which implemented the pre-re-scope both-endpoints design plus docs edits that now conflict with `main`. Rebuild on a **fresh branch off `main`**; mine #305 for its Positions implementation, tests, and fixtures (all review-CLEAN), drop its Trades retry and docs commits, and close #305 as superseded when the new PR opens.

## Design

### Positions sparse predicate

`positions.Count > 0 && positions.Any(p => string.IsNullOrEmpty(p.Name) || string.IsNullOrEmpty(p.Ticker))`. An **empty** positions list is NOT itself evidence of sparseness — the probe's cold-read case returned non-empty rows with missing fields, not an empty list — so a genuinely-zero-position account never triggers a retry. (Aside, not fixed by this story: `Position.Name`/`Position.Ticker` are declared non-nullable `string` today; the wire omits them on a cold read regardless of the C# annotation, so the null-check works at the runtime/JSON level independent of that separate ADR-0001 gap — flagged for whoever grooms `Position`'s field nullability next.)

### Positions retry

No session-start tracking, no state — a purely local, per-call heuristic: after the first `ResultFactory.FromResponse` call, if `result.IsSuccess` and the sparse predicate is true, issue exactly one more call to the same Refit method (same parameters and `cancellationToken`), re-run `ResultFactory.FromResponse` on *that* response, and **adopt the retry's result only when the retry itself succeeds — a retry HTTP failure (500/503/timeout/etc.) keeps the original first-read result instead of surfacing the retry's error** (ADR-0009 point 4a; a thrown transport failure is likewise swallowed unless the caller's own token cancelled). No second retry, no loop. Set the `Activity` tag (`ibkr.cold_read_retry = true`) only when the retry actually fires (attempted, regardless of its own outcome); omit it otherwise.

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

No change to the method's return type, parameters, or the `Result<List<Position>>` shape. No special 401 handling: the retry request travels the normal pipeline, `TokenRefreshHandler` included (ADR-0009 point 5).

### Trades — documentation only

No code change to `GetTradesAsync`'s behavior. Its XML docs (`IOrderOperations.cs`) gain the cold-read caveat: the first read of a brokerage session may return an empty list despite trades existing; there is no wire signal distinguishing this from a genuinely trade-free window; the library deliberately does not retry (ADR-0009 point 1a — an empty-trigger retry would stall up to ~5s behind this endpoint's `1 req/5 secs` limiter on every quiet poll); a consumer that needs certainty should call again.

### Retry-failure decision (ratified 2026-07-14, attended grooming — unchanged by the re-scope)

On a retry HTTP failure, keep the first successful (sparse-but-valid) result rather than surfacing the retry's failure. An internal reliability optimization must never make the caller's outcome worse than if it hadn't been attempted. (Now Positions-only, since only Positions retries.)

### Test-hygiene rider (from the design pass)

WireMock.Net scenarios whose terminal stub lacks `WillSetStateTo` finish and **reset**, so 401→200 stub pairs cycle forever — this is what manufactured the gate failure. When touching `Concurrent401_TriggersSingleReauth` (or writing this story's 401-composition test), give scenario chains explicit terminal states so the mock models a server that stays authenticated after recovery. Fixing the existing test's stubs is in scope for this story as a small hardening commit.

## TDD steps

WireMock scenario stubbing (call-count-sequenced responses, explicit terminal states per the rider above) per `.claude/rules/testing.md`, plus fast unit tests for the sparse predicate.

1. **Red — Positions sparse predicate, unit:** table-driven — empty list (not sparse), one row missing `Name` (sparse), one row missing `Ticker` (sparse), fully populated row (not sparse); fixtures from `recordings/coldread-rpd06/s1-positions-1.json` (sparse) and `s1-positions-2.json` (enriched). **Green:** implement `LooksSparse`.
2. **Red — Positions retry fires and returns enriched data, WireMock integration:** call 1 → sparse, call 2 → enriched. Assert the caller-visible result is the **enriched** data and exactly 2 HTTP calls were made.
3. **Red — Positions no retry when not sparse:** call 1 → enriched. Assert exactly **1** HTTP call.
4. **Red — Positions retry capped at one attempt:** call 1 → sparse, call 2 → also sparse. Assert the still-sparse call-2 result is returned after exactly **2** calls, never a third.
5. **Red — Positions retry HTTP failure keeps the first result** (`GetPositionsAsync_SparseFirstReadRetryFails_ReturnsFirstSuccessfulResult`): call 1 → sparse (200), call 2 → 500. Assert `Result.Success` with the sparse first-read data, NOT `Result.Failure`.
6. **Red — Trades does NOT retry on an empty read** (`GetTradesAsync_EmptyFirstRead_MakesExactlyOneCall`): stub `[]` (200). Assert `Result.Success` with the empty list and exactly **1** HTTP call — pins the re-scope so the retry can't silently return.
7. **Red — Activity tag observability:** assert `ibkr.cold_read_retry` is present and `true` on the span when the Positions retry fires (test 2) and absent when it doesn't (test 3).
8. **401 recovery composition (mandatory per `.claude/rules/testing.md`):** Positions: first call 401s → `TokenRefreshHandler` re-auths and replays (existing behavior) → the replayed response is sparse → the cold-read retry still fires on top and returns enriched data. Scenario stubs use explicit terminal states (rider above). Trades keeps its existing standard 401-recovery test unchanged.
9. **Test hygiene:** add terminal states to `Concurrent401_TriggersSingleReauth`'s scenario stubs so the 200 steps don't reset the scenario; assert it still pins exactly 1 LST acquisition.
10. **Green docs:** `GetTradesAsync` XML-doc caveat (no test). **Refactor:** existing `GetPositionsAsync`/`GetTradesAsync` tests untouched and green; full suite green.

## Done when

`GetPositionsAsync` retries once internally on a heuristically-sparse read per call (predicate per above), capped at one attempt, recorded on the method's `Activity` span (`ibkr.cold_read_retry`) only when it fires; a failed retry (HTTP error) keeps the first successful result; the retry participates in standard 401 recovery unmarked and composes with the existing 401 replay (a 401-replayed sparse first read still gets the retry). `GetTradesAsync` behavior is unchanged — no retry on an empty read (pinned by test) — and its XML docs state the cold-read caveat and consumer obligation. Return types unchanged; `Concurrent401_TriggersSingleReauth` green with scenario stubs hardened to terminal states.

## Risk / semver

`Risk: standard` — internal read-path behavior change plus documentation, not order placement/modification, auth/signing, credential handling, or streaming delivery semantics. `fix:` — no public-surface change (return types, parameters, and the `Result<List<T>>` shape are all unchanged).
