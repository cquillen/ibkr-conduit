# VCR-06 — Session lifecycle state-machine hardening

**Story:** VCR-06 (`docs/backlog.md`) · **Findings:** SES-2 (high), SES-3 (high), SES-5, SES-6 · **Decides by:** design doc §7.3/§7.4 (recorded lifecycle intent) + [ADR-0004](../../adr/0004-competing-session-truth-and-health-evidence.md) for every health-state write · **Semver:** `fix:` (behavioral repair within the recorded lifecycle contract; no surface change) · **Risk:** high (auth/session)

## Decisions (all closed)

All four defects are repairs against the recorded §7 lifecycle intent; the one coordination rule is: **every health-state write this story adds or touches follows ADR-0004** (fed from server responses, never literals, competing evidence preserved). VCR-07 owns the ssodh-response handling itself; this story must not pre-empt or contradict it.

## Scope

1. **Tickle 401 → re-auth (SES-2):** `TickleTimer`'s catch distinguishes `ApiException` with `StatusCode == 401` from transport failures: on 401, update `SessionHealthState` (authenticated:false — per ADR-0004 this reflects the server's verdict) and invoke `_onFailure` so `ReauthenticateAsync` runs (it uses `RefreshAsync`, so the dead LST is not needed). Transport failures keep log-and-continue, but repeated consecutive transport failures mark health failed so it cannot stay green indefinitely (threshold pinned in the plan; the behavior is "health cannot report a session live on stale evidence forever").
2. **LST expiry + honest state (SES-3):** `SessionTokenProvider.GetLiveSessionTokenAsync` checks `Expiry` and transparently re-acquires when expired; failed init/reauth paths reset `_state` to a failed/uninitialized value so re-entry is an intentional clean path instead of a permanent `IbkrConfigurationException` wedge.
3. **Proactive refresh retry (SES-5):** `RunProactiveRefreshAsync` retries with backoff until success or dispose; `ScheduleProactiveRefresh` with `timeUntilRefresh <= 0` attempts the refresh immediately instead of silently skipping.
4. **Tickle-timer leak (SES-6):** `EnsureInitializedAsync` stops and disposes any existing `_tickleTimer` before creating a new one (or re-uses it); combined with (2)'s state reset, re-init cannot accumulate concurrent tickle loops.

## Out of scope

- `SsodhInitResponse` inspection, competing signaling, health-evidence semantics (tickle-as-liveness) — VCR-07 (ADR-0004). This story's health writes are limited to reflecting server-observed 401/transport evidence.
- The 401 replay gate (VCR-04) — different handler.

## Acceptance criteria

- A 200-tickle loop that starts receiving 401s triggers exactly one re-auth cycle (not zero, not a storm), after which tickles succeed (WireMock scenario); health reflects the 401 window truthfully.
- An expired-LST session recovers transparently on the next consumer call instead of wedging; a failed reauth followed by a later call re-enters init cleanly (no `IbkrConfigurationException` wedge, no second live tickle loop — assert timer instance accounting).
- A proactive-refresh transient failure is retried and eventually succeeds (mock clock/backoff); an already-due refresh fires immediately.
- Existing session integration tests (including 401-recovery suites) stay green; new tests cover each finding's scenario.

## Test plan (TDD)

Red tests from the findings' suggested regression tests (SES-2/3/5/6) as WireMock scenario tests over the full DI stack (`TickleTimerTests`, session tests exist as precedent — extend those classes). Timer-leak test asserts no duplicate tickle traffic after failed-reauth→re-init (mock server counts tickle calls per interval).
