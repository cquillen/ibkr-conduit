# WebSocket Unsubscribe Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add uniform unsubscribe across every WebSocket streaming topic — subscription handles for all topics, the documented `u…` cancel wire messages with refcounting, and corrected `ssd`/`sld` subscribes that include the required `accountId`.

**Architecture:** Each subscribe method returns an `IIbkrSubscription<T>` handle whose disposal sends the topic's cancel wire message (when one exists and no other live subscription shares it), tears down locally, and completes the stream. `StreamingOperations` builds the paired subscribe+cancel strings; `IbkrWebSocketClient` stays topic-agnostic, tracks a subscription registry, and refcounts on the cancel message.

**Tech Stack:** C#/.NET, `System.Threading.Channels`, xUnit v3 + Shouldly, `System.Diagnostics` (Activity/Meter). Spec: `docs/superpowers/specs/2026-07-02-websocket-unsubscribe-design.md`. Wire reference: `docs/ibkr-websocket-api-reference.md`.

---

## File Structure

**New files**
- `src/IbkrConduit/Streaming/IIbkrSubscription.cs` — public handle interface.
- `src/IbkrConduit/Streaming/IbkrSubscription.cs` — internal handle implementation (idempotent unsubscribe).
- `tests/IbkrConduit.Tests.Unit/Streaming/IbkrSubscriptionTests.cs` — handle unit tests.

**Modified files**
- `src/IbkrConduit/Streaming/IIbkrWebSocketClient.cs` — async unsubscribe delegate + `cancelMessage` param.
- `src/IbkrConduit/Streaming/IbkrWebSocketClient.cs` — registry, refcount, cancel wire send, Unsubscribe span/metric.
- `src/IbkrConduit/Client/IStreamingOperations.cs` — return types → handles; `accountId`/`keys`/`fields`; unsolicited properties → methods.
- `src/IbkrConduit/Client/StreamingOperations.cs` — build cancel strings, wrap in handles, corrected `ssd`/`sld`.
- `tests/IbkrConduit.Tests.Unit/Streaming/StreamingOperationsTests.cs` — new signatures + `FakeWebSocketClient` double.
- `tests/IbkrConduit.Tests.Unit/Streaming/IbkrWebSocketClientTests.cs` — `unsubscribe()` → `await unsubscribe(ct)` + new cancel tests.
- `tests/IbkrConduit.Tests.Unit/Client/IbkrClientTests.cs` — any streaming property/method references.
- `examples/IbkrConduit.Examples.MarketDataStream/StreamHost.cs` — migrate to handles.
- `docs/ibkr_conduit_design.md`, `docs/implementation-status.md`, `CHANGELOG.md` — docs + breaking-change note.

---

## Task 1: Subscription handle type

**Files:**
- Create: `src/IbkrConduit/Streaming/IIbkrSubscription.cs`
- Create: `src/IbkrConduit/Streaming/IbkrSubscription.cs`
- Test: `tests/IbkrConduit.Tests.Unit/Streaming/IbkrSubscriptionTests.cs`

Purely additive — nothing consumes these yet, so the build stays green.

- [ ] **Step 1: Write the failing tests**

Create `tests/IbkrConduit.Tests.Unit/Streaming/IbkrSubscriptionTests.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Streaming;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class IbkrSubscriptionTests
{
    private static IObservable<int> EmptyStream() => new NoopObservable();

    [Fact]
    public async Task UnsubscribeAsync_InvokesUnderlyingDelegateOnce()
    {
        var count = 0;
        var sub = new IbkrSubscription<int>(EmptyStream(), _ => { count++; return ValueTask.CompletedTask; });

        await sub.UnsubscribeAsync(TestContext.Current.CancellationToken);
        await sub.UnsubscribeAsync(TestContext.Current.CancellationToken);

        count.ShouldBe(1);
    }

    [Fact]
    public async Task DisposeAsync_InvokesUnsubscribeOnce_EvenAfterExplicitUnsubscribe()
    {
        var count = 0;
        var sub = new IbkrSubscription<int>(EmptyStream(), _ => { count++; return ValueTask.CompletedTask; });

        await sub.UnsubscribeAsync(TestContext.Current.CancellationToken);
        await sub.DisposeAsync();

        count.ShouldBe(1);
    }

    [Fact]
    public void Stream_ReturnsTheProvidedObservable()
    {
        var stream = EmptyStream();
        var sub = new IbkrSubscription<int>(stream, _ => ValueTask.CompletedTask);

        sub.Stream.ShouldBeSameAs(stream);
    }

    [Fact]
    public async Task UnsubscribeAsync_PassesCancellationTokenToDelegate()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken seen = default;
        var sub = new IbkrSubscription<int>(EmptyStream(), ct => { seen = ct; return ValueTask.CompletedTask; });

        await sub.UnsubscribeAsync(cts.Token);

        seen.ShouldBe(cts.Token);
    }

    private sealed class NoopObservable : IObservable<int>
    {
        public IDisposable Subscribe(IObserver<int> observer) => new Noop();
        private sealed class Noop : IDisposable { public void Dispose() { } }
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-class "*IbkrSubscriptionTests*"`
Expected: FAIL — `IbkrSubscription` / `IIbkrSubscription` do not exist (compile error).

- [ ] **Step 3: Create the public interface**

Create `src/IbkrConduit/Streaming/IIbkrSubscription.cs`:

```csharp
namespace IbkrConduit.Streaming;

/// <summary>
/// A live streaming subscription. Dispose (or call <see cref="UnsubscribeAsync"/>) to send the
/// topic's IBKR unsubscribe message, stop delivery, and complete <see cref="Stream"/>.
/// </summary>
/// <typeparam name="T">The type of items emitted by the subscription.</typeparam>
public interface IIbkrSubscription<out T> : IAsyncDisposable
{
    /// <summary>The live stream of items for this subscription.</summary>
    IObservable<T> Stream { get; }

    /// <summary>
    /// Sends the topic's unsubscribe wire message (when one exists and no other live subscription
    /// still shares it), stops local delivery, and completes <see cref="Stream"/>. Idempotent and
    /// best-effort: a failed wire send is logged, not thrown, and local teardown still completes.
    /// <see cref="System.IAsyncDisposable.DisposeAsync"/> calls this.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the unsubscribe wire send.</param>
    ValueTask UnsubscribeAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Create the internal implementation**

Create `src/IbkrConduit/Streaming/IbkrSubscription.cs`:

```csharp
namespace IbkrConduit.Streaming;

/// <summary>
/// Default <see cref="IIbkrSubscription{T}"/>: wraps the stream observable and an async unsubscribe
/// delegate, guaranteeing the delegate runs at most once regardless of how disposal is triggered.
/// </summary>
/// <typeparam name="T">The type of items emitted by the subscription.</typeparam>
internal sealed class IbkrSubscription<T> : IIbkrSubscription<T>
{
    private readonly Func<CancellationToken, ValueTask> _unsubscribe;
    private int _unsubscribed;

    /// <summary>Creates a new <see cref="IbkrSubscription{T}"/>.</summary>
    /// <param name="stream">The stream observable exposed via <see cref="Stream"/>.</param>
    /// <param name="unsubscribe">The delegate that tears the subscription down.</param>
    public IbkrSubscription(IObservable<T> stream, Func<CancellationToken, ValueTask> unsubscribe)
    {
        Stream = stream;
        _unsubscribe = unsubscribe;
    }

    /// <inheritdoc />
    public IObservable<T> Stream { get; }

    /// <inheritdoc />
    public ValueTask UnsubscribeAsync(CancellationToken cancellationToken = default) =>
        Interlocked.Exchange(ref _unsubscribed, 1) == 0
            ? _unsubscribe(cancellationToken)
            : ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => UnsubscribeAsync(CancellationToken.None);
}
```

- [ ] **Step 5: Run to verify pass**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-class "*IbkrSubscriptionTests*"`
Expected: PASS (4 tests).

- [ ] **Step 6: Commit**

```bash
git add src/IbkrConduit/Streaming/IIbkrSubscription.cs src/IbkrConduit/Streaming/IbkrSubscription.cs tests/IbkrConduit.Tests.Unit/Streaming/IbkrSubscriptionTests.cs
git commit -m "feat(streaming): add IIbkrSubscription<T> handle"
```

---

## Task 2: WS client — async unsubscribe delegate, registry, refcount, cancel wire send

**Files:**
- Modify: `src/IbkrConduit/Streaming/IIbkrWebSocketClient.cs`
- Modify: `src/IbkrConduit/Streaming/IbkrWebSocketClient.cs`
- Modify: `src/IbkrConduit/Client/StreamingOperations.cs` (pass `cancelMessage`; keep discarding the delegate and returning `Task<IObservable<T>>` — the public API flip is Task 3)
- Modify: `tests/IbkrConduit.Tests.Unit/Streaming/StreamingOperationsTests.cs` (update the `FakeWebSocketClient` double's signatures)
- Modify: `tests/IbkrConduit.Tests.Unit/Streaming/IbkrWebSocketClientTests.cs` (`unsubscribe()` → `await unsubscribe(ct)`; new cancel tests)

The public streaming API is unchanged this task; only internal plumbing changes. Build stays green.

- [ ] **Step 1: Write the failing WS-client tests**

Append these tests to `tests/IbkrConduit.Tests.Unit/Streaming/IbkrWebSocketClientTests.cs` (inside the class). They use the existing `_adapter` (`FakeWebSocketAdapter`) and `CreateClient()` helper:

```csharp
[Fact]
public async Task Unsubscribe_SendsCancelMessage_WhenLastSubscriberForKey()
{
    await using var client = CreateClient();
    await client.ConnectAsync(TestContext.Current.CancellationToken);

    var (_, unsubscribe) = await client.SubscribeTopicAsync(
        "smd+265598+{\"fields\":[\"31\"]}", "smd", "umd+265598+{}",
        TestContext.Current.CancellationToken);

    await unsubscribe(TestContext.Current.CancellationToken);

    _adapter.SentMessages.ShouldContain("umd+265598+{}");
}

[Fact]
public async Task Unsubscribe_NullCancelMessage_SendsNoCancel()
{
    await using var client = CreateClient();
    await client.ConnectAsync(TestContext.Current.CancellationToken);

    var (_, unsubscribe) = await client.SubscribeTopicAsync(
        "smd+1+{}", "smd", cancelMessage: null,
        TestContext.Current.CancellationToken);
    while (_adapter.SentMessages.TryDequeue(out _)) { }

    await unsubscribe(TestContext.Current.CancellationToken);

    _adapter.SentMessages.ShouldBeEmpty();
}

[Fact]
public async Task Unsubscribe_RefcountsByCancelMessage_OnlyCancelsWhenLastGone()
{
    await using var client = CreateClient();
    await client.ConnectAsync(TestContext.Current.CancellationToken);

    // Two subscriptions for the SAME conid -> same cancel message.
    var (_, unsub1) = await client.SubscribeTopicAsync(
        "smd+7+{\"fields\":[\"31\"]}", "smd", "umd+7+{}", TestContext.Current.CancellationToken);
    var (_, unsub2) = await client.SubscribeTopicAsync(
        "smd+7+{\"fields\":[\"84\"]}", "smd", "umd+7+{}", TestContext.Current.CancellationToken);
    while (_adapter.SentMessages.TryDequeue(out _)) { }

    await unsub1(TestContext.Current.CancellationToken);
    _adapter.SentMessages.ShouldNotContain("umd+7+{}"); // survivor still needs conid 7

    await unsub2(TestContext.Current.CancellationToken);
    _adapter.SentMessages.ShouldContain("umd+7+{}");     // now the last is gone
}

[Fact]
public async Task Unsubscribe_WhileDisconnected_SendsNoCancelAndIsNotReplayed()
{
    var fakeTime = new FakeTimeProvider();
    await using var client = CreateClient(fakeTime);
    await client.ConnectAsync(TestContext.Current.CancellationToken);

    var (_, unsubscribe) = await client.SubscribeTopicAsync(
        "sor+{}", "sor", "uor+{}", TestContext.Current.CancellationToken);

    // Close the socket, then unsubscribe -> no wire send possible.
    _adapter.SignalClose();
    await unsubscribe(TestContext.Current.CancellationToken);

    client.ActiveSubscriptionCount.ShouldBe(0);
    _adapter.SentMessages.ShouldNotContain("uor+{}");
}

[Fact]
public async Task Unsubscribe_DropsSubscriptionFromReplaySet()
{
    var fakeTime = new FakeTimeProvider();
    await using var client = CreateClient(fakeTime);
    await client.ConnectAsync(TestContext.Current.CancellationToken);

    var (_, unsubscribe) = await client.SubscribeTopicAsync(
        "spl+{}", "spl", "upl+{}", TestContext.Current.CancellationToken);
    await unsubscribe(TestContext.Current.CancellationToken);
    while (_adapter.SentMessages.TryDequeue(out _)) { }

    var ct = TestContext.Current.CancellationToken;
    var reconnectTask = Task.Run(() => _notifier.TriggerRefreshAsync(ct), ct);
    var deadline = DateTime.UtcNow.AddSeconds(5);
    while (!reconnectTask.IsCompleted && DateTime.UtcNow < deadline)
    {
        fakeTime.Advance(TimeSpan.FromSeconds(1));
        await Task.Yield();
    }
    await reconnectTask;

    _adapter.SentMessages.ShouldNotContain("spl+{}"); // unsubscribed -> not replayed
}

[Fact]
public async Task Unsubscribe_SendFailure_DoesNotThrowAndDecrementsCount()
{
    await using var client = CreateClient();
    await client.ConnectAsync(TestContext.Current.CancellationToken);

    var (_, unsubscribe) = await client.SubscribeTopicAsync(
        "sor+{}", "sor", "uor+{}", TestContext.Current.CancellationToken);

    _adapter.FailSendAfterCount = 0; // every subsequent send throws

    await Should.NotThrowAsync(async () => await unsubscribe(TestContext.Current.CancellationToken));
    client.ActiveSubscriptionCount.ShouldBe(0);
}

[Fact]
public async Task ActiveSubscriptionCount_ReflectsSubscribeAndUnsubscribe()
{
    await using var client = CreateClient();
    await client.ConnectAsync(TestContext.Current.CancellationToken);

    client.ActiveSubscriptionCount.ShouldBe(0);
    var (_, unsubscribe) = await client.SubscribeTopicAsync(
        "spl+{}", "spl", "upl+{}", TestContext.Current.CancellationToken);
    client.ActiveSubscriptionCount.ShouldBe(1);

    await unsubscribe(TestContext.Current.CancellationToken);
    client.ActiveSubscriptionCount.ShouldBe(0);
}
```

Also update the six EXISTING call sites in this file that call `SubscribeTopicAsync(msg, prefix, ct)` and then `unsubscribe()`:
- Add a third argument `cancelMessage` before the cancellation token. Use the documented cancel for the topic, or `null` where the test doesn't care (e.g. `"umd+265598+{}"` for `smd+265598…`, `"uor+{}"` for `sor+{}`, `null` for throwaway `smd+123+{}` cases).
- Change every `unsubscribe();` / `unsub1();` / `unsub2();` call to `await unsubscribe(TestContext.Current.CancellationToken);` (the delegate is now `Func<CancellationToken, ValueTask>`). The enclosing methods are already `async Task`.

Exact existing call sites to update (method → line references at authoring time): `MessagePump_RoutesMessagesByTopicPrefix` (~119/129), `MessagePump_IgnoresUnknownTopics` (~138/151), `SubscribeTopicAsync_SendsSubscribeMessage` (~161/166), `SubscribeTopicAsync_WhenConnected_SendsSubscribeMessage` (~175/180), `ReconnectAsync_ReplaysActiveSubscriptions` (~192/194/219/220).

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-class "*IbkrWebSocketClientTests*"`
Expected: FAIL to compile — `SubscribeTopicAsync` has no `cancelMessage` overload and `unsubscribe` is not awaitable.

- [ ] **Step 3: Update the interface**

In `src/IbkrConduit/Streaming/IIbkrWebSocketClient.cs`, change the two members to:

```csharp
Task<(ChannelReader<JsonElement> Reader, Func<CancellationToken, ValueTask> Unsubscribe)> SubscribeTopicAsync(
    string subscribeMessage,
    string topicPrefix,
    string? cancelMessage,
    CancellationToken cancellationToken);

(ChannelReader<JsonElement> Reader, Func<CancellationToken, ValueTask> Unsubscribe) RegisterUnsolicitedTopic(string topicPrefix);
```

Update the XML `<remarks>`/`<returns>` on `SubscribeTopicAsync` to describe `cancelMessage` ("the IBKR unsubscribe message to send when the last subscription for this cancel message is torn down, or null for local-teardown-only topics") and the async unsubscribe delegate.

- [ ] **Step 4: Rework `IbkrWebSocketClient`**

In `src/IbkrConduit/Streaming/IbkrWebSocketClient.cs`:

1. Add the unsubscribe metric near the other counters (after `_messagesSent`):

```csharp
private static readonly Counter<long> _unsubscribeCount =
    IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.websocket.unsubscribe.count");
```

2. Replace the field `private readonly List<string> _activeSubscriptions = [];` with a registry:

```csharp
private readonly List<TopicSubscription> _subscriptions = [];
```

and add the record type (private nested, at the bottom of the class):

```csharp
private sealed record TopicSubscription(
    string TopicPrefix,
    string SubscribeMessage,
    string? CancelMessage,
    ChannelWriter<JsonElement> Writer);
```

3. Change `ActiveSubscriptionCount` to count the registry:

```csharp
public int ActiveSubscriptionCount
{
    get { lock (_subscriptionLock) { return _subscriptions.Count; } }
}
```

4. Replace `SubscribeTopicAsync` body with the new signature. Register routing + registry under `_subscriptionLock`, and return the async unsubscribe delegate:

```csharp
public async Task<(ChannelReader<JsonElement> Reader, Func<CancellationToken, ValueTask> Unsubscribe)> SubscribeTopicAsync(
    string subscribeMessage,
    string topicPrefix,
    string? cancelMessage,
    CancellationToken cancellationToken)
{
    ObjectDisposedException.ThrowIf(_disposed, this);

    using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.WebSocket.Subscribe");
    activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
    activity?.SetTag(LogFields.Topic, topicPrefix);

    var channel = Channel.CreateBounded<JsonElement>(
        new BoundedChannelOptions(_streamingBufferSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    var entry = new TopicSubscription(topicPrefix, subscribeMessage, cancelMessage, channel.Writer);
    var writers = _subscribers.GetOrAdd(topicPrefix, _ => []);
    lock (_subscriptionLock)
    {
        lock (writers)
        {
            writers.Add(channel.Writer);
        }
        _subscriptions.Add(entry);
    }

    if (_webSocket?.State == WebSocketState.Open)
    {
        await SendTextAsync(subscribeMessage, cancellationToken);
    }

    return (channel.Reader, ct => UnsubscribeSolicitedAsync(entry, ct));
}
```

5. Replace `RegisterUnsolicitedTopic` to return the async-shaped delegate (local teardown only):

```csharp
public (ChannelReader<JsonElement> Reader, Func<CancellationToken, ValueTask> Unsubscribe) RegisterUnsolicitedTopic(string topicPrefix)
{
    ObjectDisposedException.ThrowIf(_disposed, this);

    var channel = Channel.CreateBounded<JsonElement>(
        new BoundedChannelOptions(_streamingBufferSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    var writers = _subscribers.GetOrAdd(topicPrefix, _ => []);
    lock (writers)
    {
        writers.Add(channel.Writer);
    }

    return (channel.Reader, _ =>
    {
        if (_subscribers.TryGetValue(topicPrefix, out var existingWriters))
        {
            lock (existingWriters)
            {
                existingWriters.Remove(channel.Writer);
            }
        }
        channel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    });
}
```

6. Replace the old private `Unsubscribe(...)` method with the async, refcounted version:

```csharp
private async ValueTask UnsubscribeSolicitedAsync(TopicSubscription entry, CancellationToken cancellationToken)
{
    using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.WebSocket.Unsubscribe");
    activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
    activity?.SetTag(LogFields.Topic, entry.TopicPrefix);

    string? cancelToSend = null;
    lock (_subscriptionLock)
    {
        _subscriptions.Remove(entry);

        if (_subscribers.TryGetValue(entry.TopicPrefix, out var writers))
        {
            lock (writers)
            {
                writers.Remove(entry.Writer);
            }
        }
        entry.Writer.TryComplete();

        var stillReferenced = entry.CancelMessage is not null
            && _subscriptions.Exists(s => s.CancelMessage == entry.CancelMessage);
        if (entry.CancelMessage is not null && !stillReferenced && _webSocket?.State == WebSocketState.Open)
        {
            cancelToSend = entry.CancelMessage;
        }
    }

    _unsubscribeCount.Add(1,
        new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId),
        new KeyValuePair<string, object?>(LogFields.Topic, entry.TopicPrefix));

    if (cancelToSend is not null)
    {
        try
        {
            await SendTextAsync(cancelToSend, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogUnsubscribeSendError(ex);
        }
    }
}
```

7. Update `ReplayActiveSubscriptionsAsync` to replay the distinct subscribe messages of the registry:

```csharp
private async Task ReplayActiveSubscriptionsAsync(CancellationToken cancellationToken)
{
    string[] subscriptions;
    lock (_subscriptionLock)
    {
        subscriptions = _subscriptions.Select(s => s.SubscribeMessage).Distinct().ToArray();
    }

    foreach (var sub in subscriptions)
    {
        await SendTextAsync(sub, cancellationToken);
    }
}
```

8. Add the logger message near the other `[LoggerMessage]` declarations:

```csharp
[LoggerMessage(Level = LogLevel.Warning, Message = "WebSocket unsubscribe send failed")]
private partial void LogUnsubscribeSendError(Exception exception);
```

9. Add `using System.Linq;` if not already present (needed for `.Select().Distinct().ToArray()`).

- [ ] **Step 5: Update `StreamingOperations` to pass cancel messages (public API unchanged)**

In `src/IbkrConduit/Client/StreamingOperations.cs`, keep the method signatures and `Task<IObservable<T>>` returns exactly as they are, but pass the documented cancel string as the new third argument and keep discarding the delegate. Update each solicited method's `SubscribeTopicAsync` call:

- `MarketDataAsync`: `var (reader, _) = await _webSocketClient.SubscribeTopicAsync(subscribeMessage, "smd", $"umd+{conid}+{{}}", cancellationToken);`
- `OrderUpdatesAsync`: cancel `"uor+{}"`.
- `TradeExecutionsAsync`: cancel `"utr"` (bare — no braces).
- `ProfitAndLossAsync`: cancel `"upl+{}"`.
- `AccountSummaryAsync`: subscribe still `"ssd+{}"`, cancel `"usd+{}"` (accountId is added in Task 3).
- `AccountLedgerAsync`: subscribe still `"sld+{}"`, cancel `"uld+{}"` (accountId is added in Task 3).

The unsolicited `CreateUnsolicitedObservable` path is unchanged (it calls `RegisterUnsolicitedTopic`, which still returns `(reader, _)` — the discard is type-agnostic).

- [ ] **Step 6: Update the `FakeWebSocketClient` test double**

In `tests/IbkrConduit.Tests.Unit/Streaming/StreamingOperationsTests.cs`, update the nested `FakeWebSocketClient` to the new interface. Add a `LastCancelMessage` capture and return async delegates:

```csharp
public string? LastCancelMessage { get; private set; }

public Task<(ChannelReader<JsonElement> Reader, Func<CancellationToken, ValueTask> Unsubscribe)> SubscribeTopicAsync(
    string subscribeMessage,
    string topicPrefix,
    string? cancelMessage,
    CancellationToken cancellationToken)
{
    LastSubscribeMessage = subscribeMessage;
    LastTopicPrefix = topicPrefix;
    LastCancelMessage = cancelMessage;
    return Task.FromResult<(ChannelReader<JsonElement>, Func<CancellationToken, ValueTask>)>(
        (Channel.Reader, _ => ValueTask.CompletedTask));
}

public (ChannelReader<JsonElement> Reader, Func<CancellationToken, ValueTask> Unsubscribe) RegisterUnsolicitedTopic(string topicPrefix)
{
    var channel = UnsolicitedChannels.GetOrAdd(
        topicPrefix,
        _ => System.Threading.Channels.Channel.CreateUnbounded<JsonElement>());
    return (channel.Reader, _ => ValueTask.CompletedTask);
}
```

(The existing `StreamingOperationsTests` still return `Task<IObservable<T>>` from the ops methods this task, so those assertions remain valid. The `AccountSummaryAsync`/`AccountLedgerAsync` tests still pass a bare cancellation token — unchanged until Task 3.)

- [ ] **Step 7: Run all streaming unit tests to verify pass**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-namespace "*Streaming*"`
Expected: PASS — existing tests plus the seven new WS-client cancel tests.

- [ ] **Step 8: Full build + format**

Run: `dotnet build --configuration Release`
Run: `dotnet format --verify-no-changes`
Expected: build succeeds (zero warnings), format clean.

- [ ] **Step 9: Commit**

```bash
git add src/IbkrConduit/Streaming/IIbkrWebSocketClient.cs src/IbkrConduit/Streaming/IbkrWebSocketClient.cs src/IbkrConduit/Client/StreamingOperations.cs tests/IbkrConduit.Tests.Unit/Streaming/IbkrWebSocketClientTests.cs tests/IbkrConduit.Tests.Unit/Streaming/StreamingOperationsTests.cs
git commit -m "feat(streaming): send u... cancel wire message on unsubscribe with refcounting"
```

---

## Task 3: Public API flip — handles, account-id correction, unsolicited methods

**Files:**
- Modify: `src/IbkrConduit/Client/IStreamingOperations.cs`
- Modify: `src/IbkrConduit/Client/StreamingOperations.cs`
- Modify: `tests/IbkrConduit.Tests.Unit/Streaming/StreamingOperationsTests.cs`
- Modify: `tests/IbkrConduit.Tests.Unit/Client/IbkrClientTests.cs` (only if it references a streaming property/method)

This is the breaking API change. Do it all in one task so the build returns to green.

- [ ] **Step 1: Rewrite the interface**

In `src/IbkrConduit/Client/IStreamingOperations.cs`:

1. Change the six solicited return types to `Task<IIbkrSubscription<T>>` and correct the two account methods:

```csharp
Task<IIbkrSubscription<MarketDataTick>> MarketDataAsync(int conid, string[] fields, CancellationToken cancellationToken = default);

Task<IIbkrSubscription<OrderUpdate>> OrderUpdatesAsync(int? days = null, CancellationToken cancellationToken = default);

Task<IIbkrSubscription<TradeExecution>> TradeExecutionsAsync(
    bool? realtimeUpdatesOnly = null, int? days = null, CancellationToken cancellationToken = default);

Task<IIbkrSubscription<PnlUpdate>> ProfitAndLossAsync(CancellationToken cancellationToken = default);

Task<IIbkrSubscription<AccountSummaryUpdate>> AccountSummaryAsync(
    string accountId, string[]? keys = null, string[]? fields = null, CancellationToken cancellationToken = default);

Task<IIbkrSubscription<AccountLedgerUpdate>> AccountLedgerAsync(
    string accountId, string[]? keys = null, string[]? fields = null, CancellationToken cancellationToken = default);
```

2. Replace the five unsolicited `IObservable<T>` **properties** with methods returning handles, updating each XML doc to say "call before `ConnectAsync`":

```csharp
IIbkrSubscription<SessionStatusEvent> SubscribeSessionStatus();
IIbkrSubscription<BulletinEvent> SubscribeBulletins();
IIbkrSubscription<NotificationEvent> SubscribeTradingNotifications();
IIbkrSubscription<SystemEvent> SubscribeSystemEvents();
IIbkrSubscription<AccountStatusEvent> SubscribeAccountStatus();
```

Update the account-method XML docs to document `accountId` (required), `keys`, and `fields` with the example values from the spec.

- [ ] **Step 2: Rewrite `StreamingOperations`**

In `src/IbkrConduit/Client/StreamingOperations.cs`:

1. Delete the five `Lazy<IObservable<...>>` fields and their initialization in the constructor, and delete the five property getters. Keep the `_webSocketClient` field.

2. Rewrite each solicited method to wrap the stream + delegate in a handle. Example (market data):

```csharp
public async Task<IIbkrSubscription<MarketDataTick>> MarketDataAsync(int conid, string[] fields, CancellationToken cancellationToken = default)
{
    var fieldsJson = string.Join(",", fields.Select(f => $"\"{f}\""));
    var subscribeMessage = $"smd+{conid}+{{\"fields\":[{fieldsJson}]}}";
    var cancelMessage = $"umd+{conid}+{{}}";

    var (reader, unsubscribe) = await _webSocketClient.SubscribeTopicAsync(subscribeMessage, "smd", cancelMessage, cancellationToken);

    return new IbkrSubscription<MarketDataTick>(new ChannelObservable<MarketDataTick>(reader, MarketDataTickMapper.Map), unsubscribe);
}
```

Apply the same wrapping to `OrderUpdatesAsync` (cancel `"uor+{}"`), `TradeExecutionsAsync` (cancel `"utr"`, uses `FanOutChannelObservable` + `TradeExecutionMapper.MapMany`), and `ProfitAndLossAsync` (cancel `"upl+{}"`).

3. Rewrite the two account methods with `accountId` + `keys`/`fields`, and add the shared args builder:

```csharp
public async Task<IIbkrSubscription<AccountSummaryUpdate>> AccountSummaryAsync(
    string accountId, string[]? keys = null, string[]? fields = null, CancellationToken cancellationToken = default)
{
    var subscribeMessage = $"ssd+{accountId}+{BuildKeysFieldsArgs(keys, fields)}";
    var cancelMessage = $"usd+{accountId}+{{}}";

    var (reader, unsubscribe) = await _webSocketClient.SubscribeTopicAsync(subscribeMessage, "ssd", cancelMessage, cancellationToken);

    return new IbkrSubscription<AccountSummaryUpdate>(new ChannelObservable<AccountSummaryUpdate>(reader, AccountSummaryUpdateMapper.Map), unsubscribe);
}

public async Task<IIbkrSubscription<AccountLedgerUpdate>> AccountLedgerAsync(
    string accountId, string[]? keys = null, string[]? fields = null, CancellationToken cancellationToken = default)
{
    var subscribeMessage = $"sld+{accountId}+{BuildKeysFieldsArgs(keys, fields)}";
    var cancelMessage = $"uld+{accountId}+{{}}";

    var (reader, unsubscribe) = await _webSocketClient.SubscribeTopicAsync(subscribeMessage, "sld", cancelMessage, cancellationToken);

    return new IbkrSubscription<AccountLedgerUpdate>(new ChannelObservable<AccountLedgerUpdate>(reader, AccountLedgerUpdateMapper.Map), unsubscribe);
}

private static string BuildKeysFieldsArgs(string[]? keys, string[]? fields)
{
    var parts = new List<string>();
    if (keys is { Length: > 0 })
    {
        parts.Add($"\"keys\":[{string.Join(",", keys.Select(k => $"\"{k}\""))}]");
    }
    if (fields is { Length: > 0 })
    {
        parts.Add($"\"fields\":[{string.Join(",", fields.Select(f => $"\"{f}\""))}]");
    }
    return $"{{{string.Join(",", parts)}}}";
}
```

4. Replace the unsolicited property getters + `CreateUnsolicitedObservable` with methods + a handle-returning helper:

```csharp
public IIbkrSubscription<SessionStatusEvent> SubscribeSessionStatus() => CreateUnsolicitedSubscription("sts", SessionStatusMapper.Map);
public IIbkrSubscription<BulletinEvent> SubscribeBulletins() => CreateUnsolicitedSubscription("blt", BulletinMapper.Map);
public IIbkrSubscription<NotificationEvent> SubscribeTradingNotifications() => CreateUnsolicitedSubscription("ntf", NotificationMapper.Map);
public IIbkrSubscription<SystemEvent> SubscribeSystemEvents() => CreateUnsolicitedSubscription("system", SystemEventMapper.Map);
public IIbkrSubscription<AccountStatusEvent> SubscribeAccountStatus() => CreateUnsolicitedSubscription("act", AccountStatusMapper.Map);

private IbkrSubscription<T> CreateUnsolicitedSubscription<T>(string topicPrefix, Func<JsonElement, T> mapper)
{
    var (reader, unsubscribe) = _webSocketClient.RegisterUnsolicitedTopic(topicPrefix);
    return new IbkrSubscription<T>(new ChannelObservable<T>(reader, mapper), unsubscribe);
}
```

Remove the now-unused `using System.Threading;` only if the compiler flags it; otherwise leave imports. Remove the `LazyThreadSafetyMode` usages.

- [ ] **Step 3: Update `StreamingOperationsTests`**

In `tests/IbkrConduit.Tests.Unit/Streaming/StreamingOperationsTests.cs`:

1. **Subscribe-message tests** now receive handles; assert on `LastCancelMessage` too where relevant. For each `await ops.XxxAsync(...)` that previously returned an observable and asserted `LastSubscribeMessage`, no change is needed for the subscribe assertion — but add the cancel assertion. Representative:

```csharp
[Fact]
public async Task MarketDataAsync_BuildsCorrectTopicAndCancelMessage()
{
    var (ops, wsClient) = CreateOperations();
    await ops.MarketDataAsync(265598, new[] { "31", "84", "86" }, TestContext.Current.CancellationToken);
    wsClient.LastSubscribeMessage.ShouldBe("smd+265598+{\"fields\":[\"31\",\"84\",\"86\"]}");
    wsClient.LastTopicPrefix.ShouldBe("smd");
    wsClient.LastCancelMessage.ShouldBe("umd+265598+{}");
}
```

Add the analogous `LastCancelMessage` assertion to the existing order (`uor+{}`), trade (`utr`), and pnl (`upl+{}`) subscribe-message tests.

2. **Rewrite the two account-message tests** for the corrected wire + filters:

```csharp
[Fact]
public async Task AccountSummaryAsync_BuildsCorrectTopicAndCancelMessage()
{
    var (ops, wsClient) = CreateOperations();
    await ops.AccountSummaryAsync("DU1234567", cancellationToken: TestContext.Current.CancellationToken);
    wsClient.LastSubscribeMessage.ShouldBe("ssd+DU1234567+{}");
    wsClient.LastTopicPrefix.ShouldBe("ssd");
    wsClient.LastCancelMessage.ShouldBe("usd+DU1234567+{}");
}

[Fact]
public async Task AccountSummaryAsync_WithKeysAndFields_BuildsFilteredArgs()
{
    var (ops, wsClient) = CreateOperations();
    await ops.AccountSummaryAsync("DU1234567",
        keys: new[] { "AccruedCash-S", "ExcessLiquidity-S" },
        fields: new[] { "currency", "monetaryValue" },
        cancellationToken: TestContext.Current.CancellationToken);
    wsClient.LastSubscribeMessage.ShouldBe(
        "ssd+DU1234567+{\"keys\":[\"AccruedCash-S\",\"ExcessLiquidity-S\"],\"fields\":[\"currency\",\"monetaryValue\"]}");
}

[Fact]
public async Task AccountLedgerAsync_BuildsCorrectTopicAndCancelMessage()
{
    var (ops, wsClient) = CreateOperations();
    await ops.AccountLedgerAsync("DU1234567", cancellationToken: TestContext.Current.CancellationToken);
    wsClient.LastSubscribeMessage.ShouldBe("sld+DU1234567+{}");
    wsClient.LastTopicPrefix.ShouldBe("sld");
    wsClient.LastCancelMessage.ShouldBe("uld+DU1234567+{}");
}

[Fact]
public async Task AccountLedgerAsync_WithKeys_BuildsFilteredArgs()
{
    var (ops, wsClient) = CreateOperations();
    await ops.AccountLedgerAsync("DU1234567",
        keys: new[] { "LedgerListUSD" },
        cancellationToken: TestContext.Current.CancellationToken);
    wsClient.LastSubscribeMessage.ShouldBe("sld+DU1234567+{\"keys\":[\"LedgerListUSD\"]}");
}
```

Delete the old `AccountSummaryAsync_BuildsCorrectTopicMessage` and `AccountLedgerAsync_BuildsCorrectTopicMessage` tests (they asserted the now-invalid `ssd+{}`/`sld+{}`).

3. **Mapper/delivery tests** that did `var observable = await ops.XxxAsync(...); observable.Subscribe(...)` now use the handle's `Stream`. Change each to `var sub = await ops.XxxAsync(...); ... sub.Stream.Subscribe(...)`. Affected tests: `TradeExecutionsAsync_FrameWithMultipleExecutions_EmitsOnePerExecution`, `TradeExecutionsAsync_FrameWithNoArgs_EmitsNothing`, `MarketDataAsync_MapperExtractsFieldsFromJson`, `OrderUpdatesAsync_MapperDeserializesJson`, `ProfitAndLossAsync_MapperDeserializesJson`. Representative diff:

```csharp
var sub = await ops.MarketDataAsync(265598, new[] { "31" }, ct);
using var s = sub.Stream.Subscribe(new TestObserver<MarketDataTick>(onNext: t => received.TrySetResult(t)));
```

4. **Unsolicited tests** that did `((IStreamingOperations)ops).SessionStatus` now call the method and use `.Stream`. Affected: `SessionStatus_DeliversTypedEventOnTopicMessage`, `SessionStatus_DeliversAuthenticatedFalse`, `Bulletins_DeliversTypedEventOnTopicMessage`, `TradingNotifications_*` (2), `SystemEvents_*` (2), `AccountStatus_DeliversTypedEvent_AllFieldsPresent`. Representative diff:

```csharp
var sub = ((IStreamingOperations)ops).SubscribeSessionStatus();
using var s = sub.Stream.Subscribe(new TestObserver<SessionStatusEvent>(onNext: e => received.TrySetResult(e)));
```

Map each old property to its method: `SessionStatus`→`SubscribeSessionStatus()`, `Bulletins`→`SubscribeBulletins()`, `TradingNotifications`→`SubscribeTradingNotifications()`, `SystemEvents`→`SubscribeSystemEvents()`, `AccountStatus`→`SubscribeAccountStatus()`.

- [ ] **Step 4: Check `IbkrClientTests`**

Run: `grep -nE "Streaming\.(SessionStatus|Bulletins|SystemEvents|AccountStatus|TradingNotifications|MarketDataAsync|AccountSummaryAsync|AccountLedgerAsync)" tests/IbkrConduit.Tests.Unit/Client/IbkrClientTests.cs`
If any streaming property/method is referenced, update it to the new shape (property→method, observable→`.Stream`, add `accountId`). If nothing matches, no change.

- [ ] **Step 5: Run streaming + client unit tests**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-namespace "*Streaming*"`
Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-class "*IbkrClientTests*"`
Expected: PASS.

- [ ] **Step 6: Full build + format**

Run: `dotnet build --configuration Release`
Run: `dotnet format --verify-no-changes`
Expected: build succeeds (zero warnings), format clean.

- [ ] **Step 7: Commit**

```bash
git add src/IbkrConduit/Client/IStreamingOperations.cs src/IbkrConduit/Client/StreamingOperations.cs tests/IbkrConduit.Tests.Unit/Streaming/StreamingOperationsTests.cs tests/IbkrConduit.Tests.Unit/Client/IbkrClientTests.cs
git commit -m "feat(streaming)!: return IIbkrSubscription handles; require accountId on ssd/sld

BREAKING CHANGE: IStreamingOperations subscribe methods now return
IIbkrSubscription<T> handles; AccountSummaryAsync/AccountLedgerAsync
require accountId and accept optional keys/fields; unsolicited streams
are now Subscribe...() methods instead of properties."
```

---

## Task 4: Migrate the MarketDataStream example

**Files:**
- Modify: `examples/IbkrConduit.Examples.MarketDataStream/StreamHost.cs`

- [ ] **Step 1: Read the current usage**

Run: `sed -n '55,100p' examples/IbkrConduit.Examples.MarketDataStream/StreamHost.cs`
Note the `subscriptions` collection type and how `observable.Subscribe(...)` results are stored/disposed.

- [ ] **Step 2: Migrate subscribe + disposal**

Change the market-data subscribe from:

```csharp
var observable = await client.Streaming.MarketDataAsync(conid, fields, cancellationToken);
subscriptions.Add(observable.Subscribe(new ActionObserver<MarketDataTick>(...)));
```

to hold the handle and subscribe to its `Stream`:

```csharp
var subscription = await client.Streaming.MarketDataAsync(conid, fields, cancellationToken);
handles.Add(subscription);
subscriptions.Add(subscription.Stream.Subscribe(new ActionObserver<MarketDataTick>(...)));
```

Add a `List<IIbkrSubscription<MarketDataTick>> handles = new();` alongside the existing `subscriptions` list, and in the example's cleanup path dispose each handle: `foreach (var h in handles) { await h.DisposeAsync(); }` (or `await using` if the structure allows). If the example currently only disposes the `IDisposable` subscriptions, add handle disposal so the example demonstrates unsubscribe.

- [ ] **Step 3: Build the example**

Run: `dotnet build examples/IbkrConduit.Examples.MarketDataStream --configuration Release`
Expected: build succeeds.

- [ ] **Step 4: Commit**

```bash
git add examples/IbkrConduit.Examples.MarketDataStream/StreamHost.cs
git commit -m "docs(examples): migrate MarketDataStream to IIbkrSubscription handles"
```

---

## Task 5: Integration test — outbound cancel frame through the DI stack

**Files:**
- Create: `tests/IbkrConduit.Tests.Integration/Streaming/WebSocketUnsubscribeTest.cs`

Use the existing integration streaming test as the pattern.

- [ ] **Step 1: Read the existing integration streaming test**

Run: `cat tests/IbkrConduit.Tests.Integration/Streaming/WebSocketReconnectViaTickleWatchdogTest.cs`
Reuse its host/DI/mock-WS setup verbatim (WireMock for tickle, the mock WS adapter registration, `AddIbkrClient` with `IbkrClientOptions`).

- [ ] **Step 2: Write the test**

Create `tests/IbkrConduit.Tests.Integration/Streaming/WebSocketUnsubscribeTest.cs` following that pattern: build the DI client, connect, `await using`/subscribe to market data for a conid, then `await sub.UnsubscribeAsync()`, and assert the mock WS recorded an outbound `umd+{conid}+{}` frame. If the mock WS in that fixture cannot capture originated frames (see the known `MockWebSocketServer` limitation), assert instead on the observable completing after unsubscribe and document the frame-capture gap in an inline comment referencing the E2E task. Mirror the collection/attribute conventions of the existing test (e.g. `[Collection("IBKR E2E")]` if present).

- [ ] **Step 3: Run the test**

Run: `dotnet test --project tests/IbkrConduit.Tests.Integration --filter-class "*WebSocketUnsubscribeTest*"`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add tests/IbkrConduit.Tests.Integration/Streaming/WebSocketUnsubscribeTest.cs
git commit -m "test(streaming): integration coverage for websocket unsubscribe"
```

---

## Task 6: Docs and status

**Files:**
- Modify: `docs/ibkr_conduit_design.md` (§12.5 topic table)
- Modify: `docs/implementation-status.md`

> **Note:** Do **not** hand-edit `CHANGELOG.md` — this repo generates it via release-please from conventional-commit footers. The `feat(streaming)!: … BREAKING CHANGE:` commit in Task 3 is what produces the changelog entry and version bump.

- [ ] **Step 1: Extend the design doc topic table**

In `docs/ibkr_conduit_design.md` §12.5, add an "Unsubscribe" column to the topic table with the confirmed cancels (`umd+{conid}+{}`, `uor+{}`, `upl+{}`, `utr`, `usd+{accountId}+{}`, `uld+{accountId}+{}`) and a sentence noting summary/ledger subscribes take the required `accountId`. Add a short subsection after §12.5 describing the handle model (`IIbkrSubscription<T>`, dispose-to-unsubscribe, refcounting). Cross-reference `docs/ibkr-websocket-api-reference.md`.

- [ ] **Step 2: Update implementation status**

In `docs/implementation-status.md`, add a Milestone 5 row: `| 5.8 | WebSocket unsubscribe (u… topics) + subscription handles | Done |`.

- [ ] **Step 3: Commit**

```bash
git add docs/ibkr_conduit_design.md docs/implementation-status.md
git commit -m "docs(streaming): document websocket unsubscribe and handle model"
```

---

## Task 7: Live E2E (paper account)

**Files:**
- Create or extend a streaming E2E test file under `tests/IbkrConduit.Tests.Integration/Streaming/` using `[EnvironmentFact("IBKR_CONSUMER_KEY")]`.

- [ ] **Step 1: Write the E2E test**

Following the E2E conventions in `.claude/rules/testing.md` (full DI via `OAuthCredentialsFactory.FromEnvironment()` + `AddIbkrClient`, `[Collection("IBKR E2E")]`), write an `[EnvironmentFact("IBKR_CONSUMER_KEY")]` test that: resolves the paper `accountId` (via `Portfolio.GetAccountsAsync` or the configured account), connects, subscribes to `AccountSummaryAsync(accountId)`, receives at least one update, then `await sub.UnsubscribeAsync()` and asserts the stream completes. This is the live confirmation that `usd+{accountId}+{}` is accepted by IBKR (the spec's one residual verification item). Add a second case for `MarketDataAsync` unsubscribe.

- [ ] **Step 2: Run against paper (only where creds exist)**

Run: `dotnet test --project tests/IbkrConduit.Tests.Integration --filter-class "*WebSocketUnsubscribeE2E*"`
Expected: PASS when `IBKR_CONSUMER_KEY` is set; SKIPPED otherwise. Note in the PR whether it was run live.

- [ ] **Step 3: Commit**

```bash
git add tests/IbkrConduit.Tests.Integration/Streaming/
git commit -m "test(streaming): live E2E for websocket unsubscribe (paper)"
```

---

## Final verification

- [ ] Run the full check:

```bash
dotnet build --configuration Release
dotnet test --configuration Release
dotnet format --verify-no-changes
```

Expected: build zero-warnings, all tests pass (E2E skipped without creds), format clean.
