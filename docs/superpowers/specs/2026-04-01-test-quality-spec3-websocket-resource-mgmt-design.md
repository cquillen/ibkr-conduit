# Spec 3: WebSocket & Resource Management Test Gap Fill

## Goal

Add tests for WebSocket reconnect triggers, message pump edge cases, dispose safety, and heartbeat failure recovery.

## Context

This is the third and final test quality spec:

- **Spec 1 (done):** Unit test gaps — operations delegation, edge cases for complex components (56 tests)
- **Spec 2 (done):** Concurrency and resilience — concurrent 401s, error response integration tests (7 tests)
- **Spec 3 (this):** WebSocket and resource management — reconnect, message pump, dispose, heartbeat

## What's Already Covered

The existing `IbkrWebSocketClientTests` cover:
- Connect (URL, headers, OAuth token in URI)
- Heartbeat and message pump startup (no-crash check)
- Message routing by topic prefix
- Unknown topics ignored
- Subscribe sends wire message
- Auto-connect on subscribe
- Reconnect replays active subscriptions (session refresh trigger)
- Disconnect stops heartbeat and pump
- Dispose unsubscribes from notifier
- Session refresh triggers reconnect

`FakeWebSocketAdapter` already has `SignalClose()` and `FailOnConnect` hooks that were never exercised in tests.

## Tasks

### Task 1: WebSocket Reconnect Triggers (3 tests)

**File:** `tests/IbkrConduit.Tests.Unit/Streaming/IbkrWebSocketClientTests.cs`

| Test | What It Verifies |
|---|---|
| `ServerCloseFrame_TriggersReconnect` | Call `FakeWebSocketAdapter.SignalClose()` after connect. Wait briefly. Verify `ConnectAsync` was called again (reconnect fired). |
| `ReconnectFailure_LogsAndDoesNotCrash` | Set `FakeWebSocketAdapter.FailOnConnect = true` before triggering reconnect via `SignalClose()`. Verify no exception propagates, client remains in a disconnected state. |
| `SessionRefreshAfterDispose_DoesNotReconnect` | Call `DisposeAsync`, then trigger `ISessionLifecycleNotifier` callback. Verify no new `ConnectAsync` call occurs (dispose guard at line 419 of source). |

### Task 2: Message Pump Edge Cases (2 tests)

**File:** `tests/IbkrConduit.Tests.Unit/Streaming/IbkrWebSocketClientTests.cs`

| Test | What It Verifies |
|---|---|
| `MalformedJson_DroppedWithoutCrash` | Enqueue invalid JSON via `FakeWebSocketAdapter.EnqueueServerMessage("not json")`, then enqueue a valid topic message. Verify the valid message arrives — pump didn't crash on the malformed one. |
| `InternalTopics_NotDeliveredToSubscribers` | Enqueue `{"topic":"tic"}` and a valid subscriber message. Verify only the subscriber message reaches the channel — internal topics are silently filtered. |

### Task 3: Dispose Edge Cases (3 tests)

**File:** `tests/IbkrConduit.Tests.Unit/Streaming/IbkrWebSocketClientTests.cs`

| Test | What It Verifies |
|---|---|
| `DisposeAsync_Twice_DoesNotThrow` | Call `DisposeAsync` twice. Verify no `ObjectDisposedException` or other exception. |
| `DisposeAsync_CompletesSubscriberChannels` | Subscribe to a topic, then dispose. Verify `ChannelReader.WaitToReadAsync` returns `false` (channel completed). |
| `SendAfterDispose_DoesNotThrow` | Dispose, then call a subscribe-like operation that triggers `SendTextAsync`. Verify no exception — `SendTextAsync` returns silently when socket is null/closed. |

### Task 4: Heartbeat Failure (1 test)

**Files:**
- Modify: `tests/IbkrConduit.Tests.Unit/Streaming/FakeWebSocketAdapter.cs` — add `FailSendAfterCount` property
- Modify: `tests/IbkrConduit.Tests.Unit/Streaming/IbkrWebSocketClientTests.cs` — add test

The `FakeWebSocketAdapter.SendAsync` method needs a configurable failure mode. Add a `FailSendAfterCount` property: after N successful `SendAsync` calls, throw `System.Net.WebSockets.WebSocketException`. This allows the heartbeat's "tic" send to fail, triggering reconnect.

| Test | What It Verifies |
|---|---|
| `HeartbeatSendFailure_TriggersReconnect` | Set `FailSendAfterCount` to trigger failure after the initial subscribe messages. Wait for the heartbeat loop to fire (10s default — may need to verify the heartbeat interval or use a shorter wait). Verify reconnect is triggered. |

Note: The heartbeat sends "tic" every 10 seconds. To avoid a 10-second test, check if the heartbeat interval is configurable or if we can trigger it sooner. If not, the test may need to wait ~11 seconds. An alternative: verify the behavior through `FakeWebSocketAdapter.SentMessages` count growth, confirming heartbeats are being sent, then fail on the next one.

## FakeWebSocketAdapter Extension

Add to `FakeWebSocketAdapter`:

```csharp
public int? FailSendAfterCount { get; set; }
private int _sendCount;

// In SendAsync:
_sendCount++;
if (FailSendAfterCount.HasValue && _sendCount > FailSendAfterCount.Value)
{
    throw new System.Net.WebSockets.WebSocketException("Simulated send failure");
}
```

## Testing Conventions

- xUnit v3, Shouldly assertions (no `Assert`)
- Naming: `MethodName_Scenario_ExpectedResult`
- Use existing `FakeWebSocketAdapter` and test infrastructure
- `CancellationToken` via `TestContext.Current.CancellationToken`
- Timing-sensitive tests use generous timeouts (200-500ms) to avoid flakiness

## Out of Scope

- TokenRefreshHandler tests (already well-covered with 9 tests across 2 files)
- Multi-fragment WebSocket messages (low risk, FakeWebSocketAdapter would need significant changes)
- `TryWrite` returning false (requires completing a channel mid-subscription — complex setup for low risk)

## Success Criteria

- Reconnect triggers tested for server close frame, reconnect failure, and post-dispose guard
- Message pump handles malformed JSON without crashing
- Internal topics filtered from subscriber channels
- Double dispose is safe
- Channel completion propagates to subscribers on dispose
- Send after dispose is a no-op
- Heartbeat failure triggers reconnect
- ~9 new passing tests
- `dotnet build --configuration Release && dotnet test --configuration Release && dotnet format --verify-no-changes` passes clean
