# FO-4 — Reap empty streaming subscriber-map entries

**Spec date:** 2026-07-09 · **Story:** FO-4 · **Risk:** high · **Semver:** `fix:` (no public-surface change)
**Touches:** `src/IbkrConduit/Streaming/IbkrWebSocketClient.cs` (`_subscribers`, `SubscribeTopicAsync`, unsubscribe path). Relates to ADR-0005 (full-topic-identity routing), findings CON-2/CON-3.

## Problem

`_subscribers` is a `ConcurrentDictionary<string, List<ChannelWriter<JsonElement>>>` keyed by the **full topic identity** (ADR-0005/PVR-01 — e.g. `smd+265598`, `ssd+DUO873728`). When the last writer for a key unsubscribes, `writers.Remove(...)` leaves an **empty list still mapped** under that key. For a long-lived client that rotates many conids/accounts (subscribe → unsubscribe → new conid → …), the map grows without bound over the process lifetime — a slow leak, bounded per key but unbounded in distinct keys.

## Constraint — the CON-2/CON-3 race

Subscribe/unsubscribe/dispatch are serialized by a coarse `_subscriptionLock` plus a per-list `lock(writers)`; **but** `SubscribeTopicAsync` calls `_subscribers.GetOrAdd(routingKey, _ => [])` *before* taking `_subscriptionLock`. A naive reap (`if (writers.Count == 0) _subscribers.TryRemove(routingKey)`) races a concurrent subscribe that has already captured the soon-empty list via `GetOrAdd` but not yet added its writer under the lock: the reap removes the key, the subscribe then adds its writer to an **orphaned** list no longer mapped in `_subscribers`, and dispatch (which does `TryGetValue` on the map) never routes frames to it — a silently dead subscription. This is the exact hazard the existing CON-2 rollback guards for a concurrent *dispose*.

## Design

Reap is **atomic with writer-removal, under the same locks, and value-conditional**; the subscribe path is extended to detect and recover from a benign reap (distinct from a fatal dispose).

1. **Reap on empty (unsubscribe path):** immediately after `writers.Remove(channel.Writer)` inside `lock(_subscriptionLock) { lock(writers) { … } }`, if `writers.Count == 0`, remove via the **value-conditional** overload `_subscribers.TryRemove(new KeyValuePair<string, List<…>>(routingKey, writers))` — which removes the key **only if it still maps that exact empty list instance**, never a list a concurrent subscribe has since repopulated or replaced.
2. **Subscribe post-add re-assertion (extend CON-2):** after adding the writer under the locks, re-assert `_subscribers.TryGetValue(routingKey, out var mapped) && ReferenceEquals(mapped, writers)`. If the instance is no longer mapped:
   - **disposing/connection swept** → keep today's CON-2 behavior: roll back (remove writer, dispose channel) and **fail** (ODE);
   - **otherwise (reaped)** → roll back and **retry once** via a fresh `GetOrAdd(routingKey, _ => [])` + re-add under the locks; on a second miss, fail defensively. A reap is benign housekeeping and must **not** fail a legitimate concurrent subscribe.
3. **Dispatch and dispose are unchanged** — dispatch already `lock(writers)`-guards and tolerates an absent key; `DisposeAsync` still clears the whole map under `_subscriptionLock`.

**Invariant (the fork this closes):** no writer is ever left in a `List` that is not the instance currently mapped under its `routingKey` in `_subscribers`; an empty list is never left mapped after its last writer unsubscribes; a reap never fails a concurrent subscribe, and a dispose still does.

## TDD steps

1. **Red — reap:** subscribe one topic, unsubscribe it; assert the key is gone from `_subscribers` (internal count/contains test seam — add an `internal` accessor if none exists, per the VCR-08/PVR-13 test-gate pattern). Fails today. **Green:** implement step 1.
2. **Red — no premature reap:** two subscriptions on the **same** routing key; unsubscribe one; assert the key remains and the surviving writer still receives a broadcast frame.
3. **Red — CON-3 reap-vs-subscribe race (deterministic gate):** gate a subscribe after its `GetOrAdd` but before the add; on another thread unsubscribe the last existing writer for that key (triggering a reap); release the gate; assert the new subscription **is** routed a subsequently-broadcast frame (not orphaned). Implement step 2's re-assertion + retry. Verify.
4. **Red — dispose-vs-subscribe still fails (CON-2 preserved):** the existing CON-2 test still asserts a subscribe racing dispose rolls back and fails (ODE) — must stay green.
5. **Refactor:** confirm no dispatch/dispose regression; run the full streaming suite.

## Done when

After the last writer for a routing key unsubscribes, that key is removed from `_subscribers`; a key with surviving writers is retained and keeps delivering; a subscribe racing a reap ends up routed (never orphaned); and a subscribe racing a dispose still fails as before. No public surface changes.

## Risk / semver

`Risk: high` — streaming delivery/thread-safety. `fix:` — internal data-structure hygiene; no DTO, method, or guarantee change. Lands independently of the 0.9.0 train.
