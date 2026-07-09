# FO-10 — Reap-symmetry on the STR-4 send-failure path

**Spec date:** 2026-07-09 · **Story:** FO-10 · **Risk:** high · **Semver:** `fix:` (no public-surface change)
**Touches:** `src/IbkrConduit/Streaming/IbkrWebSocketClient.cs` (the STR-4 send-failure rollback inside `SubscribeTopicAsync`). Extends FO-4 (`docs/superpowers/specs/2026-07-09-fo-4-subscriber-map-reap.md`); relates to ADR-0005 (full-topic-identity routing), findings CON-2/CON-3.

## Problem

FO-4 (#282) reaps empty `_subscribers` entries on the **unsubscribe** path — after the last writer for a full-topic-identity routing key (e.g. `smd+265598`, `ssd+DUO873728`) is removed, the now-empty `List<ChannelWriter<JsonElement>>` is removed from the map via a value-conditional `TryRemove`. Both FO-4 quality lenses independently observed that the **sibling send-failure rollback path is not reap-symmetric**: `SubscribeTopicAsync` registers the writer under the locks, then issues the subscribe frame's `SendTextAsync`; if that send throws (socket racing Open→Aborted, or the token cancelled mid-send), the `catch` rolls the writer back out of the list but does **not** reap a now-empty list. A subscribe that was the **sole writer** for a key and fails to send therefore leaves exactly the empty full-identity entry FO-4 targets — the same unbounded-over-lifetime leak class, on the one subscribe path FO-4's spec explicitly scoped out.

Bounded/rare in practice (send failures are uncommon; a later subscribe on the same conid reuses the empty list, whose eventual unsubscribe reaps it) — so this is a **symmetry/hygiene** fix, not a live leak. But it belongs to the same invariant FO-4 established and should hold uniformly across every writer-removal site.

## Design

Apply FO-4's reap discipline to the STR-4 rollback, **identically and under the same locks**:

1. In the send-failure `catch`, after removing the writer from `writers` (the existing rollback), if `writers.Count == 0`, reap via the **value-conditional** overload `_subscribers.TryRemove(new KeyValuePair<string, List<…>>(routingKey, writers))` — remove the key only if it still maps that exact empty list instance. This must happen **under the same `lock(_subscriptionLock)` / `lock(writers)`** the rollback already holds (widen no lock scope; add no new lock; preserve the acquisition order `_subscriptionLock` → `writers` verified by FO-4's lock-discipline review).
2. **Interaction with FO-4's subscribe retry (the one subtlety to get right):** FO-4 added a post-add re-assertion that, on detecting its list was reaped by a concurrent unsubscribe, retries once via a fresh `GetOrAdd`. A rollback-reap must not orphan or double-fail a concurrent subscribe: because the value-conditional `TryRemove` only removes the *exact* empty instance, a concurrent subscribe that has already repopulated (or replaced via `GetOrAdd`) the list is never clobbered; and a subscribe whose own send is failing owns the list it is rolling back. The rollback path already **fails** the subscribe (it propagates the send exception) — reaping its now-empty list is pure cleanup that changes no control flow. No new race is introduced: the reap is value-conditional and lock-atomic, exactly as on the unsubscribe path.
3. Dispatch and `DisposeAsync` are unchanged (dispatch tolerates an absent key; dispose clears the whole map under `_subscriptionLock`).

**Invariant (now uniform across all removal sites):** an empty `List` is never left mapped under its `routingKey` after the last writer is removed — whether the removal is an unsubscribe (FO-4) **or** a send-failure rollback (FO-10); a reap never clobbers a list a concurrent subscribe repopulated/replaced; the CON-2 dispose-vs-subscribe failure and FO-4's reap-vs-subscribe routing both still hold.

## TDD steps

1. **Red — rollback reap:** drive a subscribe whose immediate `SendTextAsync` throws while it is the **sole** writer for a routing key (inject a send failure via the mock-WS/`BroadcastTextAsync` harness or a socket set to fail-on-send, per the VCR-08/PVR-13/FO-4 gated-test pattern). Assert (via the FO-4 `SubscriberKeyCount`/`HasSubscriberKey` internal seam) the key is **absent** from `_subscribers` after the failed subscribe. Fails today (empty list left mapped). **Green:** implement step 1.
2. **Red — no premature reap on send failure with a surviving co-writer:** a second writer already subscribed on the **same** key; a new subscribe on that key fails its send. Assert the key is **retained** and the surviving writer still receives a subsequently-broadcast frame (the rollback removed only the failed writer, not the co-writer's live list).
3. **Race preservation:** the existing FO-4 reap-vs-subscribe routing test and the CON-2 dispose-vs-subscribe failure test stay green (run the streaming suite a few times for nondeterminism, per FO-4).
4. **Refactor:** confirm no dispatch/dispose regression; full streaming suite green.

## Done when

A subscribe whose immediate `SendTextAsync` throws as the sole writer for a routing key leaves **no** empty `_subscribers` entry mapped (value-conditional reap on the rollback path, mirroring FO-4's unsubscribe reap); a send failure with a surviving co-writer on the same key retains the key and keeps delivering; FO-4's reap-vs-subscribe routing and the CON-2 dispose-vs-subscribe failure both still hold. No public surface change.

## Risk / semver

`Risk: high` — streaming delivery / thread-safety (same surface as FO-4). `fix:` — internal data-structure hygiene; no DTO, method, option, or guarantee change. Lands independently of the 0.9.0 train.
