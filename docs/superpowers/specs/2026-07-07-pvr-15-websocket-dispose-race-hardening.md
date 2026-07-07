# PVR-15 — WebSocket dispose/connect race hardening

**Story:** PVR-15 (`docs/backlog.md`) · **Findings:** STR-3, STR-2 (high, CONFIRMED), CON-2 (medium, CONFIRMED), STR-6 (low, CONFIRMED) · **Decided by:** findings' fix directions (pattern repairs within the recorded §12 lifecycle; no contract change) · **Semver:** `fix:` · **Risk:** high (delivery semantics — teardown must not strand or duplicate delivery)

## Decisions (all closed)

Lock ordering: `DisposeAsync` acquires `_connectLock` before teardown and sets `_disposed` under it; all connect/reconnect/subscribe mutation re-checks `_disposed` inside the lock. No new public surface.

## Scope

1. **`DisposeAsync` vs in-flight reconnect (STR-3):** `DisposeAsync` cancels `_disposeCts` first, then acquires `_connectLock` before disposing the semaphore/socket; reconnect work links to `_disposeCts` regardless of caller tokens; `_subscriptions` is cleared under the lock so a straggler replay has nothing to resubscribe.
2. **Subscribe vs dispose (CON-2):** after committing a writer to the registries, re-check `_disposed` (or hold the shared registration lock) — if disposed, complete + remove the writer and throw `ObjectDisposedException`, mirroring the MGR-3 post-install re-check pattern.
3. **Pump/slot lifetime (STR-2):** `SingleObserverChannelObservable`/`ChannelObservable` — the pump observes cancellation per item, and `SubscriptionSlot.Dispose` frees the single-observer slot only after the pump task has exited (store the pump `Task`; release in its continuation) so re-subscribe never yields two concurrent pumps on one reader.
4. **Adapter leak (STR-6):** `ConnectCoreAsync` wraps the connect attempt; on failure (or on cancellation before assignment) the factory-created adapter is disposed before rethrow.

## Out of scope

- Subscribe rollback / stale reconnect trigger / replay duplication — PVR-16 (next in the same lane).
- Routing keys — PVR-01 (after PVR-16 in the lane).

## Acceptance criteria

- Dispose during an in-flight reconnect: no `ObjectDisposedException` escapes, no reconnect completes after dispose, no subscription survives (deterministic race tests with test gates).
- Subscribe racing dispose: either completes fully (then disposed cleanly) or throws `ObjectDisposedException` — never a live writer on a disposed client.
- Dispose-then-resubscribe on an observable slot: exactly one pump ever reads the channel (no interleaved deliveries; pinned with a slow-observer gate).
- A failed `ConnectAsync` leaves no undisposed adapter (tracking-fake adapter factory).

## Test plan (TDD)

Red tests: unit-level with the fake `IWebSocketAdapter` factory + deterministic gates for each race (dispose-vs-reconnect, subscribe-vs-dispose, slot re-subscribe, failed connect). Existing mock-WS integration suites stay green. All offline.
