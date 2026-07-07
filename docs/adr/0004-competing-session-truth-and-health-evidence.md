# ADR-0004 — Truthful competing-session signaling and health evidence

**Status:** Accepted · **Date:** 2026-07-07
**Relates to:** findings SES-1, SES-4, GAP3-1, GAP3-2, GAP3-3 (`docs/findings/2026-07-04-rtos-venue-consumer-review.md`); design doc §7.7. **Implemented by:** VCR-07 (spec `docs/superpowers/specs/2026-07-07-vcr-07-competing-session-truth.md`), health-adjacent fixes in VCR-06.

## Context

IBKR signals a lost compete or failed brokerage-bridge bring-up as **HTTP 200 with `authenticated=false`** on `ssodh/init`, and relays competing-session status on the `sts` topic. The library discards both: init/reauth ignore the `SsodhInitResponse` body and mark the session Ready (SES-1), `IbkrSessionError.IsCompeting` is hardcoded `false` at its only construction site (GAP3-1 — the flag consumers map to session-loss recovery is dead code), reauth unconditionally writes `competing:false` into health state, erasing evidence a tickle just recorded (GAP3-2), the `sts` mapper drops the `competing`/`fail` fields (GAP3-3), and tickle successes never count as liveness evidence, so an idle healthy session reports Unhealthy after 120 s (SES-4). Net: the session/health surface can report live-when-dead *and* dead-when-live.

## Decision

1. **A 200 `ssodh/init` with `authenticated=false` is a FAILED init/reauth**, never success: the session does not transition to Ready, and the surfaced error carries `IsCompeting=true` when the response (or the triggering tickle/`sts` evidence) reports competition.
2. **`SessionHealthState` is fed from server responses, never from literals** — the `SsodhInitResponse`'s `Authenticated`/`Competing` values flow in; no code path writes a hardcoded `competing:false`.
3. **With `Compete=false` and a competing session observed, re-authentication backs off** instead of looping at the failure interval (two `Compete=true` processes must not ping-pong the session every few seconds).
4. **`sts` competing/fail evidence surfaces to consumers** — `SessionStatusEvent` carries the fields (shaped per ADR-0001 presence rules) and competing evidence feeds health state.
5. **Tickle successes are liveness evidence:** an initialized session with a passing tickle loop is healthy while consumer-idle; "no consumer call in 120 s" alone is not Unhealthy.

## Alternatives considered

- **Truthful health only** (feed health state honestly but keep init/reauth "succeeding" on `authenticated=false`, no backoff): smaller change, but the error surface consumers map to session-loss stays dead code and the reauth ping-pong remains — the two live-money failure modes the review flagged. Rejected.
- **Status quo:** health that lies in both directions; competing detection unreachable. Rejected.

## Consequences

- 📦 **Breaking-behavioral** (`feat!:`): init/reauth now fail where they previously "succeeded" into a dead session; consumers see new failure signals (the honest ones). `SessionStatusEvent` gains fields (additive, ADR-0001-shaped).
- A competing takeover becomes observable through error, event, and health surfaces consistently; spurious Unhealthy-while-idle disappears.
- Cost: consumers that treated init success as unconditional must handle the failure path; backoff tuning lives in VCR-07's spec.

## Relationships

Design doc §7.7 (new) + §7.3/§7.4 (tickle/refresh mechanics it feeds); findings SES-1/SES-4/GAP3-1/GAP3-2/GAP3-3; implemented by VCR-07 (VCR-06 must not contradict it — its health-state writes follow this ADR); ADR-0001 shapes the event DTO; ADR-0003 is the order-path sibling in the same error taxonomy.
