# Test Quality Spec 3: WebSocket & Resource Management — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add ~9 tests for WebSocket reconnect triggers, message pump edge cases, dispose safety, and heartbeat failure recovery.

**Architecture:** Extend `FakeWebSocketAdapter` with a configurable send failure mode. All new tests go in the existing `IbkrWebSocketClientTests.cs` file, using existing test infrastructure (FakeWebSocketAdapter, FakeSessionApi, FakeLifecycleNotifier).

**Tech Stack:** xUnit v3, Shouldly, existing hand-written fakes

---

## Dependency Graph

```
Task 1 (extend FakeWebSocketAdapter)
         │
         ▼
Tasks 2-5 (all depend on the extended fake)
         │
         ▼
      [independent — can run in any order after Task 1]
```

**Branch:** `test/websocket-resource-mgmt-tests` (single branch for all tasks)

---

## Task 1 — Extend FakeWebSocketAdapter with Send Failure Mode

### Files Modified

- `tests/IbkrConduit.Tests.Unit/Streaming/FakeWebSocketAdapter.cs`

### What to Change

Add a `FailSendAfterCount` property and a send counter. When the counter exceeds the threshold, `SendAsync` throws `WebSocketException`.

### Steps

- [ ] Read `tests/IbkrConduit.Tests.Unit/Streaming/FakeWebSocketAdapter.cs`
- [ ] Add these fields/properties:
  ```csharp
  private int _sendCount;

  public int? FailSendAfterCount { get; set; }

  public int ConnectCallCount { get; private set; }
  ```
- [ ] Modify `SendAsync` to increment `_sendCount` and throw when threshold exceeded:
  ```csharp
  public Task SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType,
      bool endOfMessage, CancellationToken cancellationToken)
  {
      _sendCount++;
      if (FailSendAfterCount.HasValue && _sendCount > FailSendAfterCount.Value)
      {
          throw new WebSocketException("Simulated send failure");
      }

      var text = Encoding.UTF8.GetString(buffer.Span);
      _sentMessages.Enqueue(text);
      return Task.CompletedTask;
  }
  ```
- [ ] Modify `ConnectAsync` to increment `ConnectCallCount`:
  ```csharp
  public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
  {
      ConnectCallCount++;
      if (_failOnConnect)
      {
          throw new WebSocketException("Simulated connection failure");
      }

      ConnectedUri = uri;
      _state = WebSocketState.Open;
      return Task.CompletedTask;
  }
  ```
- [ ] Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~IbkrWebSocketClientTests"`
- [ ] Verify all existing tests still pass (the new fields have no effect unless set)
- [ ] Commit: `test: extend FakeWebSocketAdapter with send failure mode and connect counter`

---

## Task 2 — WebSocket Reconnect Trigger Tests (3 tests)

### Files Modified

- `tests/IbkrConduit.Tests.Unit/Streaming/IbkrWebSocketClientTests.cs`

### Important

Read `src/IbkrConduit/Streaming/IbkrWebSocketClient.cs`:
- **Close frame** (line 295-299): Message pump detects `WebSocketMessageType.Close`, fires `ReconnectAsync` on `Task.Run`.
- **Reconnect failure** (line 407-410): `ConnectCoreAsync` throws during reconnect → caught, logged, `_connectLock` released. No retry.
- **Post-dispose guard** (line 374-377): `ReconnectAsync` returns immediately if `_disposed`.
- **Session refresh** (line 418-427): `OnSessionRefreshedAsync` checks `_disposed` before calling `ReconnectAsync`.

`FakeWebSocketAdapter.SignalClose()` sets state to `CloseReceived` and releases the inbound signal. The message pump's `ReceiveAsync` returns a `Close` frame result.

`FakeWebSocketAdapter.FailOnConnect` causes `ConnectAsync` to throw `WebSocketException`.

### Test Inventory

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| 1 | `ServerCloseFrame_TriggersReconnect` | After connect, call `SignalClose()`. Wait 1500ms (reconnect has 1000ms delay). Verify `ConnectCallCount >= 2` (initial + reconnect). |
| 2 | `ReconnectFailure_DoesNotCrash` | Connect, then set `FailOnConnect = true` and `SignalClose()`. Wait 1500ms. No exception propagates. Verify adapter state is not `Open` (reconnect failed gracefully). |
| 3 | `SessionRefreshAfterDispose_DoesNotReconnect` | Connect, note `ConnectCallCount`. Dispose. Trigger `_notifier.TriggerRefreshAsync`. Wait 200ms. Verify `ConnectCallCount` did not increase. |

### Steps

- [ ] Add test `ServerCloseFrame_TriggersReconnect`:
  ```
  Connect. Record ConnectCallCount. SignalClose(). Wait 1500ms.
  Assert ConnectCallCount >= 2.
  ```
- [ ] Add test `ReconnectFailure_DoesNotCrash`:
  ```
  Connect. Set FailOnConnect = true. SignalClose(). Wait 1500ms.
  Assert no exception. Adapter state is not Open.
  ```
- [ ] Add test `SessionRefreshAfterDispose_DoesNotReconnect`:
  ```
  Connect. Record count = ConnectCallCount. DisposeAsync().
  TriggerRefreshAsync(). Wait 200ms.
  Assert ConnectCallCount == count (unchanged).
  ```
- [ ] Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~IbkrWebSocketClientTests"`
- [ ] Verify all tests pass
- [ ] Commit: `test: add WebSocket reconnect trigger tests (close frame, failure, post-dispose)`

---

## Task 3 — Message Pump Edge Cases (2 tests)

### Files Modified

- `tests/IbkrConduit.Tests.Unit/Streaming/IbkrWebSocketClientTests.cs`

### Important

`ProcessMessage` (line 324-369):
- `JsonDocument.Parse` throws `JsonException` → logged, returns (pump continues)
- Internal topics `tic`, `system`, `sts` → silently returned (not delivered to subscribers)

### Test Inventory

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| 1 | `MalformedJson_DroppedWithoutCrash` | Enqueue "not json", then a valid message. Verify the valid message arrives. Pump didn't crash. |
| 2 | `InternalTopics_NotDeliveredToSubscribers` | Subscribe to "smd". Enqueue `{"topic":"tic"}` then `{"topic":"smd+123","data":"ok"}`. Verify only the smd message arrives. |

### Steps

- [ ] Add test `MalformedJson_DroppedWithoutCrash`:
  ```
  Connect. Subscribe to "smd" topic.
  EnqueueServerMessage("not json").
  EnqueueServerMessage("""{"topic":"smd+265598","31":"150.25"}""").
  Read from channel with 5s timeout. Assert topic is "smd+265598".
  ```
- [ ] Add test `InternalTopics_NotDeliveredToSubscribers`:
  ```
  Connect. Subscribe to "smd" topic.
  EnqueueServerMessage("""{"topic":"tic"}""").
  EnqueueServerMessage("""{"topic":"smd+123","31":"100"}""").
  Read from channel with 5s timeout. Assert topic is "smd+123".
  (If "tic" were delivered, it would be read first and the assert would fail.)
  ```
- [ ] Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~IbkrWebSocketClientTests"`
- [ ] Verify all tests pass
- [ ] Commit: `test: add message pump edge case tests (malformed JSON, internal topics)`

---

## Task 4 — Dispose Edge Cases (3 tests)

### Files Modified

- `tests/IbkrConduit.Tests.Unit/Streaming/IbkrWebSocketClientTests.cs`

### Important

`DisposeAsync` (line 139-167):
- `if (_disposed) return;` — double-dispose guard
- Completes all `ChannelWriter`s via `TryComplete()`
- Disposes `_connectLock`

`SendTextAsync` (line 429-444):
- `if (_webSocket == null || _webSocket.State != WebSocketState.Open) return;` — no-op when disconnected

### Test Inventory

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| 1 | `DisposeAsync_CalledTwice_DoesNotThrow` | Create client, connect, dispose, dispose again. No exception. |
| 2 | `DisposeAsync_CompletesSubscriberChannels` | Connect, subscribe to "smd" topic (get channel reader). Dispose. Assert `reader.Completion.IsCompleted` is true (or `WaitToReadAsync` returns false). |
| 3 | `SubscribeAfterDispose_DoesNotThrow` | Connect, dispose. Calling `SubscribeTopicAsync` after dispose — verify it doesn't crash. Since `_webSocket` is null after dispose, `ConnectAsync` will be called, which tries to acquire `_connectLock`. But `_connectLock` is disposed! This may throw `ObjectDisposedException`. If it does, that's acceptable behavior — the test should verify the exception type is `ObjectDisposedException` (not some other crash). |

Note on Test 3: After checking the source, `DisposeAsync` disposes `_connectLock` (line 166). Any subsequent call to `SubscribeTopicAsync` → `ConnectAsync` → `_connectLock.WaitAsync` will throw `ObjectDisposedException`. This is correct behavior. The test should verify that `ObjectDisposedException` is thrown (not a null reference or other unexpected exception).

### Steps

- [ ] Add test `DisposeAsync_CalledTwice_DoesNotThrow`:
  ```
  Create client, connect. DisposeAsync(). DisposeAsync() again.
  Should not throw.
  ```
- [ ] Add test `DisposeAsync_CompletesSubscriberChannels`:
  ```
  Create client, connect. Subscribe to "smd" topic, get reader.
  DisposeAsync(). Assert reader.Completion.IsCompleted is true.
  ```
- [ ] Add test `SubscribeAfterDispose_ThrowsObjectDisposedException`:
  ```
  Create client, connect. DisposeAsync().
  Should.ThrowAsync<ObjectDisposedException>(
      () => client.SubscribeTopicAsync("smd+123+{}", "smd", ct));
  ```
- [ ] Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~IbkrWebSocketClientTests"`
- [ ] Verify all tests pass
- [ ] Commit: `test: add WebSocket dispose edge case tests (double-dispose, channel completion, post-dispose subscribe)`

---

## Task 5 — Heartbeat Send Failure (1 test)

### Files Modified

- `tests/IbkrConduit.Tests.Unit/Streaming/IbkrWebSocketClientTests.cs`

### Important

The heartbeat loop (line 235-267) fires every 10 seconds (`_heartbeatIntervalSeconds`). When `SendTextAsync` throws (non-`OperationCanceledException`), the catch block (line 254-258) logs the error and fires `ReconnectAsync` on `Task.Run`.

Since the heartbeat interval is 10 seconds, a direct test would take 10+ seconds. The `FailSendAfterCount` on `FakeWebSocketAdapter` triggers on any `SendAsync` call. The subscribe message also calls `SendAsync`. So: connect (1 tickle), subscribe (1 send for subscribe message), set `FailSendAfterCount` to the current send count, then wait for the heartbeat to fire after 10 seconds and fail.

To avoid a 10-second wait, an alternative: the subscribe call itself can be made to fail by setting `FailSendAfterCount` before subscribing. But that tests a different code path (subscribe, not heartbeat).

The practical approach: accept the 11-second test. The heartbeat fires at 10s, the send fails, reconnect triggers (1s delay), and we check `ConnectCallCount`.

Actually, a better approach: set `FailSendAfterCount` immediately after connect (before any subscribe). The initial connect calls `ConnectCoreAsync` which calls `StartHeartbeat`. The heartbeat's first tick is at 10s. No subscribe messages are sent. The first `SendAsync` call will be the heartbeat "tic" at 10s, which triggers the failure. Total wait: ~11-12s.

### Test Inventory

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| 1 | `HeartbeatSendFailure_TriggersReconnect` | Connect. Set `FailSendAfterCount = 0` (fail on first send). Wait ~12 seconds. Verify `ConnectCallCount >= 2`. |

### Steps

- [ ] Add test `HeartbeatSendFailure_TriggersReconnect`:
  ```
  Create client, connect.
  Assert ConnectCallCount == 1.
  Set adapter.FailSendAfterCount = 0 (next send will fail).
  Wait 12 seconds for heartbeat tick + reconnect delay.
  Assert ConnectCallCount >= 2 (reconnect was triggered by heartbeat failure).
  Note: Also set FailOnConnect = false (default) to allow reconnect to succeed
  after the first failure. Reset FailSendAfterCount = null to allow reconnect
  sends to succeed.
  ```
  Actually this is tricky: we need the FIRST send to fail (heartbeat), but the reconnect will also try to send. So we need to reset `FailSendAfterCount` after the failure triggers reconnect. Since we can't easily do this from the test (it's async), instead set `FailSendAfterCount` to a value that allows the heartbeat to fail but subsequent reconnect sends to succeed. Use `FailSendAfterCount = 1` — the first send (heartbeat "tic") succeeds, the second send (next heartbeat or something else) fails. But this adds more wait time.

  Simplest approach: set `FailSendAfterCount = 0` so any send fails. The reconnect's `ConnectCoreAsync` will succeed (no sends during connect itself — only `StartHeartbeat` and `StartMessagePump` are started). Then the reconnect's subscription replay won't send anything (no active subscriptions). The new heartbeat will also fail on its next tick, but by then we've already verified the reconnect. 12 seconds total.

  If 12 seconds is too long, skip this test and leave a comment explaining why. The heartbeat → reconnect code path is identical to the close-frame → reconnect path (both call `ReconnectAsync`), which is tested in Task 2.
- [ ] Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~HeartbeatSendFailure"`
- [ ] Verify test passes
- [ ] Commit: `test: add heartbeat send failure reconnect test`

---

## Final Verification

After all tasks are committed:

- [ ] Run full check: `dotnet build /workspace/ibkr-conduit --configuration Release`
- [ ] Run full check: `dotnet test /workspace/ibkr-conduit --configuration Release`
- [ ] Run full check: `dotnet format /workspace/ibkr-conduit --verify-no-changes`

### Test Count Summary

| Task | Tests |
|------|-------|
| Task 1: FakeWebSocketAdapter extension | 0 (infrastructure) |
| Task 2: Reconnect triggers | 3 |
| Task 3: Message pump edge cases | 2 |
| Task 4: Dispose edge cases | 3 |
| Task 5: Heartbeat failure | 1 |
| **Total** | **9** |
