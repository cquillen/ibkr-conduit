# VCR-02 — Streaming delivery observability & subscription semantics

**Story:** VCR-02 (`docs/backlog.md`) · **Findings:** FIL-1 (critical), GAP2-4, FIL-3, FIL-4, FIL-5 · **Decides by:** [ADR-0002](../../adr/0002-streaming-delivery-guarantee.md), design doc §12.8 · **Semver:** BREAKING-behavioral — `feat!:` (throw-on-second-subscribe; buffer default change; additive observability surface) · **Risk:** high (delivery semantics)

## Decisions (all closed — ADR-0002)

At-most-once per subscriber, **loss-is-observable**. `DropOldest` stays; default `StreamingBufferSize` 256 → 2048; every loss path emits Warning + counter (tenant + wire topic + cause); reconnect/gap transitions are consumer-observable; `Stream` is single-observer (second concurrent `Subscribe` throws).

## Scope

1. **Observable evictions (FIL-1):** `IbkrWebSocketClient.SubscribeTopicAsync`/`RegisterUnsolicitedTopic` switch to the `Channel.CreateBounded<T>(BoundedChannelOptions, Action<T> itemDropped)` overload; the callback logs Warning and increments a new counter `ibkr.conduit.streaming.frames.dropped` tagged `{tenant, topic, cause="overflow"}`. Log throttle: Warning on the first drop per topic per connection, then rely on the counter (prevents log floods under a stalled consumer while keeping every drop counted).
2. **Observable mapper/observer drops (GAP2-4, FIL-3):** the per-frame catch in `ChannelObservable`/`FanOutChannelObservable` increments the same counter with `cause="mapper"` or `cause="observer"`, logging the **wire topic** (not the DTO type name). Observer failures get a distinct log message; an `OperationCanceledException` thrown by consumer `OnNext` must not masquerade as graceful completion — it tears down via `OnError` (Rx contract), never `OnCompleted`.
3. **Connection-lifecycle events (FIL-4):** additive public surface on `IStreamingOperations`: a subscribable stream of connection events — `Disconnected(at, reason)` / `Reconnected(at, replayedTopics)` — emitted from every reconnect path (server close, receive error, heartbeat failure, session refresh, tickle watchdog). Same subscription mechanics as existing topics (channel-backed, `IIbkrSubscription`-shaped), so consumers can bound a gap and trigger REST reconciliation.
4. **Single-observer guard (FIL-5):** `ChannelObservable`/`FanOutChannelObservable` gain an `Interlocked` guard — a second concurrent `Subscribe` throws `InvalidOperationException`; the constraint is XML-documented on `IIbkrSubscription<T>.Stream`. Disposing the first subscription frees the slot.
5. **Default change:** `IbkrClientOptions.StreamingBufferSize` 256 → 2048 (update the pinned-default test).

## Out of scope

- Per-element mapper robustness inside `TradeExecutionMapper.MapMany` — VCR-03 (same files; serialize in one lane per the build-order map).
- Configurable `FullMode` — rejected in ADR-0002.

## Acceptance criteria

- With a stalled reader, writing `StreamingBufferSize + 1` frames via the mock WebSocket server increments the drop counter (cause=overflow) and fires the Warning exactly once for that topic/connection (the findings' suggested regression test `SubscribeTopicAsync_BufferOverflow_EmitsDropSignal`).
- A mapper failure logs the wire topic and increments cause=mapper; an observer exception increments cause=observer with the distinct message; an OCE from `OnNext` surfaces via `OnError`.
- A forced reconnect (mock server close) emits `Disconnected` then `Reconnected` with the replayed topics listed.
- A second concurrent `Subscribe` on one subscription throws `InvalidOperationException`; after disposing the first, a new `Subscribe` succeeds.
- Default-value test pins 2048.

## Test plan (TDD)

Red tests via the DI-stack `MockWebSocketServer` harness (`BroadcastTextAsync`, #226): overflow, mapper-drop, observer-throw, OCE, reconnect-events, double-subscribe. Metrics asserted via `MeterListener`. Unit tests for the log-throttle state. All offline; no live account.
