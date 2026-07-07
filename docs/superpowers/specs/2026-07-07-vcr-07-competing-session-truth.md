# VCR-07 — Competing-session truth & health evidence

**Story:** VCR-07 (`docs/backlog.md`, depends on VCR-01) · **Findings:** SES-1 (high), GAP3-1 (high), GAP3-2, GAP3-3, SES-4 (high) · **Decides by:** [ADR-0004](../../adr/0004-competing-session-truth-and-health-evidence.md), design doc §7.7; DTO shapes per [ADR-0001](../../adr/0001-nullable-as-presence-wire-fidelity.md) · **Semver:** BREAKING-behavioral — `feat!:` (init/reauth failure semantics; additive `SessionStatusEvent` fields) · **Risk:** high (auth/session)

## Decisions (all closed — ADR-0004)

Truthful + back off: `authenticated=false` ssodh ⇒ failed init/reauth with competing evidence carried; health state fed from server responses; `Compete=false` + competing observed ⇒ backoff; `sts` competing/fail surfaced; tickle successes are liveness evidence.

## Scope

1. **ssodh honesty (SES-1):** `EnsureInitializedAsync`/`ReauthenticateAsync` capture and inspect `SsodhInitResponse`; `Authenticated == false` ⇒ the operation fails (no Ready transition, no reauth-epoch increment) with a session error carrying `IsCompeting` when the response/tickle/`sts` evidence reports competition. With `Compete == false` and competing observed, the retry path backs off (bounded exponential; parameters pinned in the plan) instead of looping at the failure interval.
2. **Truthful `IsCompeting` (GAP3-1):** `TokenRefreshHandler` propagates the competing evidence captured by (1) into the `IbkrSessionError` it wraps — the literal-`false` construction site is eliminated; a post-retry 401 with competing evidence maps to a competing session error.
3. **No literal health writes (GAP3-2):** `SessionHealthState.Update` calls after init/reauth pass the response's `Authenticated`/`Competing`, never literals; competing evidence recorded by a tickle survives a reauth cycle (sticky until a server response reports competing:false).
4. **`sts` evidence (GAP3-3):** `SessionStatusEvent` gains `Competing` (`bool?`) and `FailReason` (`string?`) — ADR-0001 presence shapes, mapped defensively when present in `args` (this is why the story depends on VCR-01's landed nullable semantics); `sts` competing=true feeds `SessionHealthState`.
5. **Tickle liveness (SES-4):** tickle successes record into `LastSuccessfulCallTracker` (handler added to the session pipeline, or `RecordSuccess` from `TickleTimer`'s success branch); `EvaluateOverallStatus` treats "initialized + tickling, no consumer call yet/recently" as healthy — consumer-call staleness alone no longer reports Unhealthy.

## Out of scope

- Tickle-401/transport handling, LST expiry, refresh retry, timer leak — VCR-06 (must be consistent with this spec's health rules; build order handles the shared files).
- The nullable retrofit of existing `SessionStatusEvent.Authenticated` — VCR-01.

## Acceptance criteria

- WireMock ssodh responding 200 `{authenticated:false, competing:true}` ⇒ init/reauth fails, error `IsCompeting == true`, health shows authenticated:false + competing:true (SES-1/GAP3-1).
- After a tickle records competing:true, a subsequent reauth does NOT reset health to competing:false unless the server response says so (GAP3-2).
- An `sts` frame carrying `competing`/`fail` surfaces them on `SessionStatusEvent` and in health; frames without them yield `null` fields (GAP3-3 + ADR-0001).
- With `Compete=false` and competing observed, reauth attempts space out per the backoff (no 5-second ping-pong) (SES-1).
- A freshly provisioned, idle, tickling session reports healthy at t>120s; a session whose tickles stop degrades (SES-4).
- 401-recovery integration tests for touched endpoints remain green.

## Test plan (TDD)

Red tests from the findings' suggested regression tests (SES-1, SES-4, GAP3-1/2/3): WireMock ssodh/tickle scenarios over the full DI stack; mock-WS `sts` frames via `BroadcastTextAsync`; health assertions through `GetHealthStatusAsync`/`IbkrHealthCheck`. Backoff tested with the mock clock pattern used by rate-limiter tests.
