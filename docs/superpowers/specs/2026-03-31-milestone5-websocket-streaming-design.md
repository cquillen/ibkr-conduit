# Milestone 5 — WebSocket Streaming

**Date:** 2026-03-31
**Status:** Draft
**Goal:** Add real-time WebSocket streaming for market data, order updates, P&L, account summary, and account ledger to the `IIbkrClient` facade.

---

## Scope

M5 adds WebSocket streaming to the library. After M5:

1. `client.Streaming.MarketData(conid, fields)` → `IObservable<MarketDataTick>`
2. `client.Streaming.OrderUpdates(days?)` → `IObservable<OrderUpdate>`
3. `client.Streaming.ProfitAndLoss()` → `IObservable<PnlUpdate>`
4. `client.Streaming.AccountSummary()` → `IObservable<AccountSummaryUpdate>`
5. `client.Streaming.AccountLedger()` → `IObservable<AccountLedgerUpdate>`
6. Automatic reconnect on session refresh via `ISessionLifecycleNotifier`
7. Internal 10-second heartbeat keeps the WebSocket alive

### Deferred

- **WebSocket market data history (smh)** — can be added later as another `IObservable`
- **Price ladder (sbd)** — niche, add on demand
- **System.Reactive operators** — consumers add the NuGet package themselves if they want Rx operators

---

## Architecture

### Component Diagram

```
Consumer
    │
    └── client.Streaming (IStreamingOperations)
            │
            ▼
        StreamingOperations
            │  - builds topic subscribe messages
            │  - maps JsonElement → typed models
            │  - returns IObservable<T> via ChannelObservable<T>
            │
            ▼
        IbkrWebSocketClient (internal)
            ├── ClientWebSocket → wss://api.ibkr.com/v1/api/ws?oauth_token={accessToken}
            ├── cookie: api={sessionToken from /tickle}
            ├── 10-second heartbeat (PeriodicTimer → {"topic":"tic"})
            ├── message pump (background Task → route by topic prefix)
            ├── subscribers: ConcurrentDictionary<prefix, List<ChannelWriter<JsonElement>>>
            └── subscribes to ISessionLifecycleNotifier → reconnect

    ISessionLifecycleNotifier (singleton)
            ├── SessionManager calls NotifyAsync() after re-auth
            └── IbkrWebSocketClient reconnects on notification
```

### WebSocket URL

For OAuth 1.0a connections:
```
wss://api.ibkr.com/v1/api/ws?oauth_token={accessToken}
```

With cookie header: `api={sessionTokenFromTickle}`

The `oauth_token` is the OAuth access token from credentials (NOT the Live Session Token). The session token comes from the `/tickle` response's `session` field.

### IObservable<T> — No External Dependencies

The `IObservable<T>` interface is in-box (`System.Runtime`). Our implementation uses `Channel<T>` internally and a minimal `ChannelObservable<T>` adapter. Consumers who want Rx operators (`Buffer`, `Throttle`, etc.) add `System.Reactive` themselves — our library has no dependency on it.

---

## Task 5.1 — ISessionLifecycleNotifier

### Interface

```csharp
/// <summary>
/// Notifies subscribers when the brokerage session has been refreshed.
/// Used by the WebSocket client to reconnect after session re-authentication.
/// </summary>
public interface ISessionLifecycleNotifier
{
    /// <summary>
    /// Subscribes to session refresh notifications.
    /// Returns an IDisposable that removes the subscription when disposed.
    /// </summary>
    IDisposable Subscribe(Func<CancellationToken, Task> onSessionRefreshed);

    /// <summary>
    /// Notifies all subscribers that the session has been refreshed.
    /// Called by SessionManager after successful re-authentication.
    /// </summary>
    Task NotifyAsync(CancellationToken cancellationToken);
}
```

### SessionLifecycleNotifier (implementation)

- Thread-safe subscriber list (lock on add/remove/iterate)
- `NotifyAsync` invokes subscribers sequentially — no parallel thundering reconnects
- Individual subscriber exceptions are caught and logged, not propagated
- `Subscribe` returns an `IDisposable` that removes the callback from the list

### SessionManager Changes

- Add `ISessionLifecycleNotifier` as constructor dependency
- At the end of `ReauthenticateAsync` (after tickle timer restart, before setting state to Ready):
  ```csharp
  await _notifier.NotifyAsync(cancellationToken);
  ```

### DI Registration

- `services.AddSingleton<ISessionLifecycleNotifier, SessionLifecycleNotifier>()`
- Update `SessionManager` constructor in `ServiceCollectionExtensions`

---

## Task 5.2 — IbkrWebSocketClient

### IbkrWebSocketClient (internal, IAsyncDisposable)

**Constructor:** `IIbkrSessionApi`, `IbkrOAuthCredentials`, `ISessionLifecycleNotifier`, `ILogger<IbkrWebSocketClient>`

**Fields:**
- `_webSocket`: `ClientWebSocket`
- `_heartbeatCts`: `CancellationTokenSource` for the heartbeat loop
- `_messagePumpCts`: `CancellationTokenSource` for the message pump
- `_subscribers`: `ConcurrentDictionary<string, List<ChannelWriter<JsonElement>>>` — topic prefix → channel writers
- `_activeSubscriptions`: `List<string>` — subscribe messages to replay on reconnect
- `_notifierSubscription`: `IDisposable` — lifecycle notifier subscription
- `_connectLock`: `SemaphoreSlim(1,1)` — prevents concurrent connect attempts

**`ConnectAsync(CancellationToken)`:**
1. Acquire connect lock
2. Call `_sessionApi.TickleAsync()` to get session token
3. Build URL: `wss://api.ibkr.com/v1/api/ws?oauth_token={credentials.AccessToken}`
4. Create new `ClientWebSocket`, set cookie header `api={tickleResponse.Session}`
5. `await _webSocket.ConnectAsync(uri, cancellationToken)`
6. Start heartbeat loop (10-second `PeriodicTimer`)
7. Start message pump loop (background `Task`)
8. Release connect lock

**`DisconnectAsync()`:**
1. Cancel heartbeat and message pump
2. Close `ClientWebSocket` with `WebSocketCloseStatus.NormalClosure`

**`SubscribeTopicAsync(string subscribeMessage, string topicPrefix)`:**
1. If not connected → `ConnectAsync()`
2. Create `Channel<JsonElement>` (unbounded)
3. Add channel writer to `_subscribers[topicPrefix]`
4. Add subscribe message to `_activeSubscriptions`
5. Send subscribe message on socket as UTF-8 text
6. Return `ChannelReader<JsonElement>`
7. Return an unsubscribe `Action` that removes the channel writer and sends unsubscribe message if applicable

**Heartbeat loop:**
```
while not cancelled:
    await periodicTimer.WaitForNextTickAsync(ct)
    send {"topic":"tic"} on socket
```

**Message pump loop:**
```
while not cancelled:
    read message from socket
    parse as JsonElement
    extract "topic" field (e.g., "smd+265598", "sor", "spl")
    determine prefix (e.g., "smd", "sor", "spl", "ssd", "sld")
    find matching channel writers in _subscribers
    write JsonElement to each channel writer
    handle "sts" (auth status) and "system" messages internally
```

**Reconnect (on session refresh notification):**
1. `DisconnectAsync()`
2. `ConnectAsync()`
3. Re-send all messages in `_activeSubscriptions`

**`DisposeAsync`:**
1. Unsubscribe from `ISessionLifecycleNotifier`
2. `DisconnectAsync()`
3. Complete all channel writers (triggers `OnCompleted` for observers)

---

## Task 5.3 — Streaming Response Models

All models as immutable records with `JsonExtensionData`.

### MarketDataTick

```csharp
/// <summary>
/// A real-time market data tick from the WebSocket smd topic.
/// </summary>
public record MarketDataTick
{
    /// <summary>Contract identifier.</summary>
    public int Conid { get; init; }

    /// <summary>Epoch timestamp of the update.</summary>
    public long? Updated { get; init; }

    /// <summary>All field values keyed by field ID string.</summary>
    public IReadOnlyDictionary<string, string>? Fields { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
```

Consumers use `MarketDataFields` constants (from M4) to look up values in the `Fields` dictionary.

### OrderUpdate

```csharp
public record OrderUpdate
{
    [JsonPropertyName("orderId")]
    public string OrderId { get; init; } = string.Empty;

    [JsonPropertyName("conid")]
    public int Conid { get; init; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("side")]
    public string Side { get; init; } = string.Empty;

    [JsonPropertyName("size")]
    public decimal Size { get; init; }

    [JsonPropertyName("orderType")]
    public string OrderType { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal? Price { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("filledQuantity")]
    public decimal FilledQuantity { get; init; }

    [JsonPropertyName("remainingQuantity")]
    public decimal RemainingQuantity { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
```

### PnlUpdate

```csharp
public record PnlUpdate
{
    [JsonPropertyName("acctId")]
    public string AccountId { get; init; } = string.Empty;

    [JsonPropertyName("dpl")]
    public decimal DailyPnl { get; init; }

    [JsonPropertyName("upl")]
    public decimal UnrealizedPnl { get; init; }

    [JsonPropertyName("rpl")]
    public decimal RealizedPnl { get; init; }

    [JsonPropertyName("nl")]
    public decimal NetLiquidation { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
```

### AccountSummaryUpdate

```csharp
public record AccountSummaryUpdate
{
    [JsonPropertyName("acctId")]
    public string AccountId { get; init; } = string.Empty;

    /// <summary>Key-value pairs of account summary fields.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Data { get; init; }
}
```

### AccountLedgerUpdate

```csharp
public record AccountLedgerUpdate
{
    [JsonPropertyName("acctId")]
    public string AccountId { get; init; } = string.Empty;

    /// <summary>Currency-keyed ledger data.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Data { get; init; }
}
```

Note: Exact field names for WebSocket models will be refined during E2E testing against real IBKR responses. The `JsonExtensionData` captures everything.

---

## Task 5.4 — ChannelObservable + StreamingOperations

### ChannelObservable<T> (internal)

Minimal `IObservable<T>` implementation backed by a `ChannelReader<JsonElement>`:

```csharp
internal class ChannelObservable<T> : IObservable<T>
{
    private readonly ChannelReader<JsonElement> _reader;
    private readonly Func<JsonElement, T> _mapper;

    public IDisposable Subscribe(IObserver<T> observer)
    {
        var cts = new CancellationTokenSource();
        _ = PumpAsync(observer, cts.Token);
        return new CancellationDisposable(cts);
    }

    private async Task PumpAsync(IObserver<T> observer, CancellationToken ct)
    {
        try
        {
            await foreach (var item in _reader.ReadAllAsync(ct))
            {
                var mapped = _mapper(item);
                observer.OnNext(mapped);
            }
            observer.OnCompleted();
        }
        catch (OperationCanceledException)
        {
            observer.OnCompleted();
        }
        catch (Exception ex)
        {
            observer.OnError(ex);
        }
    }
}
```

### CancellationDisposable (internal)

```csharp
internal sealed class CancellationDisposable : IDisposable
{
    private readonly CancellationTokenSource _cts;

    public CancellationDisposable(CancellationTokenSource cts) => _cts = cts;

    public void Dispose() => _cts.Cancel();
}
```

### IStreamingOperations

```csharp
public interface IStreamingOperations
{
    IObservable<MarketDataTick> MarketData(int conid, string[] fields);
    IObservable<OrderUpdate> OrderUpdates(int? days = null);
    IObservable<PnlUpdate> ProfitAndLoss();
    IObservable<AccountSummaryUpdate> AccountSummary();
    IObservable<AccountLedgerUpdate> AccountLedger();
}
```

### StreamingOperations

Each method:
1. Builds the topic subscribe message
2. Calls `_webSocketClient.SubscribeTopicAsync(message, prefix)`
3. Wraps the returned `ChannelReader<JsonElement>` in a `ChannelObservable<T>` with the appropriate mapper

**Topic messages:**
- Market data: `smd+{conid}+{"fields":["{field1}","{field2}"]}`
- Order updates: `sor+{}` or `sor+{"days":{days}}`
- P&L: `spl+{}`
- Account summary: `ssd+{}`
- Account ledger: `sld+{}`

**Mappers** deserialize `JsonElement` → typed record using `JsonSerializer.Deserialize<T>`. For `MarketDataTick`, the mapper also extracts numeric field keys into the `Fields` dictionary (same pattern as REST snapshot mapping).

---

## Task 5.5 — IIbkrClient Facade Update + DI Wiring

### IIbkrClient Changes

Add:
```csharp
IStreamingOperations Streaming { get; }
```

### IbkrClient Changes

Add `IStreamingOperations` to constructor and expose as property.

### ServiceCollectionExtensions Changes

Register:
- `ISessionLifecycleNotifier` → `SessionLifecycleNotifier` (singleton)
- `IbkrWebSocketClient` (singleton — one WebSocket per tenant)
- `IStreamingOperations` → `StreamingOperations` (singleton — wraps the WebSocket client)
- Update `SessionManager` constructor to include `ISessionLifecycleNotifier`
- Update `IbkrClient` constructor to include `IStreamingOperations`

---

## Task 5.6 — Integration Tests

### Unit Tests

**SessionLifecycleNotifier:**
- `NotifyAsync_WithSubscribers_CallsAll`
- `NotifyAsync_SubscriberThrows_DoesNotBlockOthers`
- `Subscribe_Dispose_RemovesSubscriber`

**ChannelObservable:**
- `Subscribe_ReceivesItems_CallsOnNext`
- `Subscribe_ChannelCompleted_CallsOnCompleted`
- `Subscribe_Dispose_StopsReceiving`

**StreamingOperations:**
- Test topic message formatting for each subscription type

### E2E Test (conditional on IBKR_CONSUMER_KEY)

Using the DI pattern:
```csharp
var services = new ServiceCollection();
services.AddLogging();
services.AddIbkrClient(creds);

await using var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IIbkrClient>();

// Subscribe to SPY market data
var received = new TaskCompletionSource<MarketDataTick>();
using var sub = client.Streaming.MarketData(spyConid, new[] { MarketDataFields.LastPrice })
    .Subscribe(tick => received.TrySetResult(tick));

// Wait for at least one tick (with timeout)
var tick = await received.Task.WaitAsync(TimeSpan.FromSeconds(30));
tick.Conid.ShouldBe(spyConid);
```

Note: WebSocket E2E requires a brokerage session and market data subscription. The test should handle the case where the market is closed (no ticks flowing) gracefully — skip or timeout without failure.

---

## Project Structure (New/Modified Files)

### New Files

```
src/IbkrConduit/
  Session/
    ISessionLifecycleNotifier.cs
    SessionLifecycleNotifier.cs
  Streaming/
    IbkrWebSocketClient.cs
    ChannelObservable.cs
    CancellationDisposable.cs
    StreamingModels.cs
  Client/
    IStreamingOperations.cs
    StreamingOperations.cs

tests/IbkrConduit.Tests.Unit/
  Session/
    SessionLifecycleNotifierTests.cs
  Streaming/
    ChannelObservableTests.cs
    StreamingOperationsTests.cs
```

### Modified Files

```
src/IbkrConduit/Session/SessionManager.cs — add ISessionLifecycleNotifier, call NotifyAsync
src/IbkrConduit/Client/IIbkrClient.cs — add Streaming property
src/IbkrConduit/Client/IbkrClient.cs — add Streaming
src/IbkrConduit/Http/ServiceCollectionExtensions.cs — register new components
```

---

## Dependency Graph

```
Task 5.1 (notifier)      Task 5.3 (streaming models)
         │                        │
         ▼                        │
Task 5.2 (WebSocket client)       │
         │                        │
         └────────┬───────────────┘
                  ▼
         Task 5.4 (observable + operations)
                  │
                  ▼
         Task 5.5 (facade + DI)
                  │
                  ▼
         Task 5.6 (tests)
```

**Parallel opportunities:** Tasks 5.1 and 5.3 are independent.
