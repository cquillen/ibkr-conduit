# WebSocket Unsubscribe — Design

- **Date:** 2026-07-02
- **Status:** Spec
- **Related:** [ibkr-websocket-api-reference.md](../../ibkr-websocket-api-reference.md);
  Milestone 5 (WebSocket Streaming); trade-execution stream spec §8 (deferred `utr`)

## 1. Motivation

The streaming layer can subscribe to every WebSocket topic but cannot unsubscribe from any. Three concrete gaps:

1. **No public unsubscribe API.** `IStreamingOperations` exposes only subscribe methods returning bare `Task<IObservable<T>>`. There is no subscription handle, so the only way to stop *anything* is disposing the whole client — which drops *every* topic.
2. **The internal unsubscribe delegate is discarded.** `IIbkrWebSocketClient.SubscribeTopicAsync` returns `(Reader, Action Unsubscribe)`, but `StreamingOperations` throws the `Action` away (`var (reader, _) = …`).
3. **Even that delegate never tells IBKR to stop.** `IbkrWebSocketClient.Unsubscribe` does purely local teardown (remove writer, complete channel, drop from the replay set); it sends no `u…` wire message. Server-side subscriptions persist.

**Impact is uneven.** Market data (`smd`) is metered by IBKR "Market Data Lines"; a long-lived client cycling through conids leaks server-side lines and eventually hits the cap while believing it has unsubscribed. The account/PnL/order/trade topics are effectively singletons and lower-impact, but still cannot be stopped mid-session.

**Subscribe-side non-compliance found during this work.** Auditing the library's subscribe messages against the WebSocket reference, the account-summary (`ssd`) and account-ledger (`sld`) subscribes omit the **required** `accountId` segment (the library sends `ssd+{}` / `sld+{}`). This spec corrects both the subscribe and the unsubscribe sides.

## 2. Goals / Non-goals

**Goals**
- Uniform unsubscribe across **all** topics — solicited and unsolicited — sending the documented `u…` counterpart wherever IBKR defines one.
- Make each subscribe method return a disposable **subscription handle** rather than a bare observable.
- Correct `ssd`/`sld` subscribes to include the required `accountId`, and expose the documented optional `keys`/`fields` filters on both.
- **Refcount** shared wire subscriptions so a cancel is sent only when the last consumer of that wire subscription unsubscribes.
- Preserve reconnect-replay semantics (unsubscribed streams are not resubscribed; live ones still are).

**Non-goals**
- Market-data 15-minute auto-termination / re-request-before-10-minutes refresh (separate follow-up; see §10).
- Historical market data (`smh`/`umh`), BookTrader price ladder (`sbd`/`ubd`), and option-exercise (`shs`/`inp`) topics — not implemented in the library at all.
- Library-side dedupe of replayed data (already the consumer's responsibility).
- Publishing account-summary/ledger key/field constant catalogs (like `MarketDataFields`) — possible future nicety.

## 3. Confirmed wire reference

All commands below are confirmed against IBKR's published WebSocket docs (see the reference doc). Cancel key = the string used to refcount shared wire subscriptions.

| Topic | Subscribe (corrected) | Cancel | Cancel / refcount key |
|---|---|---|---|
| Market data | `smd+{conid}+{"fields":[…]}` | `umd+{conid}+{}` | per conid |
| Live orders | `sor+{}` / `sor+{"days":N}` | `uor+{}` | singleton |
| Profit & loss | `spl+{}` | `upl+{}` | singleton |
| Trades / executions | `str+{…}` | **`utr`** (bare — no `+{}`) | singleton |
| Account summary | `ssd+{accountId}+{…}` | `usd+{accountId}+{}` | per accountId |
| Account ledger | `sld+{accountId}+{…}` | `uld+{accountId}+{}` | per accountId |
| Unsolicited (`sts`,`system`,`act`,`blt`,`ntf`) | (none) | `null` (local teardown only) | n/a |

Two special cases the implementation must respect:
- **Trades cancel is literally `utr`** — no braces, no args. Every other cancel is `u…+…+{}`. Stored as an explicit per-topic constant, not derived by an `s→u` transform.
- **Filters do not affect the cancel.** `usd`/`uld` take only `accountId`; `umd` takes only `conid`. So two subscriptions to the same account (or conid) with different `keys`/`fields` share one cancel key and are cancelled together only when the last is disposed.

## 4. Public API

### 4.1 Subscription handle (new public type, `IbkrConduit.Streaming`)

```csharp
/// <summary>
/// A live streaming subscription. Dispose (or call <see cref="UnsubscribeAsync"/>) to
/// send the topic's IBKR unsubscribe message, stop delivery, and complete <see cref="Stream"/>.
/// </summary>
public interface IIbkrSubscription<out T> : IAsyncDisposable
{
    /// <summary>The live stream of items for this subscription.</summary>
    IObservable<T> Stream { get; }

    /// <summary>
    /// Sends the topic's unsubscribe wire message (when one exists and no other live
    /// subscription still shares it), stops local delivery, and completes <see cref="Stream"/>.
    /// Idempotent; best-effort (a failed wire send is logged, not thrown, and local
    /// teardown still completes). <see cref="IAsyncDisposable.DisposeAsync"/> calls this.
    /// </summary>
    ValueTask UnsubscribeAsync(CancellationToken cancellationToken = default);
}
```

### 4.2 Solicited topics (return type changes)

```csharp
Task<IIbkrSubscription<MarketDataTick>> MarketDataAsync(
    int conid, string[] fields, CancellationToken cancellationToken = default);

Task<IIbkrSubscription<OrderUpdate>> OrderUpdatesAsync(
    int? days = null, CancellationToken cancellationToken = default);

Task<IIbkrSubscription<TradeExecution>> TradeExecutionsAsync(
    bool? realtimeUpdatesOnly = null, int? days = null, CancellationToken cancellationToken = default);

Task<IIbkrSubscription<PnlUpdate>> ProfitAndLossAsync(CancellationToken cancellationToken = default);

Task<IIbkrSubscription<AccountSummaryUpdate>> AccountSummaryAsync(
    string accountId, string[]? keys = null, string[]? fields = null,
    CancellationToken cancellationToken = default);

Task<IIbkrSubscription<AccountLedgerUpdate>> AccountLedgerAsync(
    string accountId, string[]? keys = null, string[]? fields = null,
    CancellationToken cancellationToken = default);
```

`accountId` on the two account methods is **required** and mirrors their REST twins (`GetAccountSummaryAsync(string accountId, …)`, `GetLedgerAsync(string accountId, …)`). `keys`/`fields` are the documented optional filters; both omitted → args `{}` (all keys/fields), built with the same omit-when-empty pattern the `str` topic uses.

### 4.3 Unsolicited topics (property → method)

The five unsolicited streams change from `IObservable<T>` **properties** (lazy shared singletons) to methods returning a fresh handle each call:

```csharp
IIbkrSubscription<SessionStatusEvent> SubscribeSessionStatus();
IIbkrSubscription<BulletinEvent> SubscribeBulletins();
IIbkrSubscription<NotificationEvent> SubscribeTradingNotifications();
IIbkrSubscription<SystemEvent> SubscribeSystemEvents();
IIbkrSubscription<AccountStatusEvent> SubscribeAccountStatus();
```

These are **synchronous** (no wire message is sent — IBKR pushes these unconditionally), so they return the handle directly, not a `Task`. Their `UnsubscribeAsync` does local teardown only (`CancelMessage` = null). The "subscribe before `ConnectAsync`" ordering guidance is unchanged: call `SubscribeSessionStatus()` before connecting, exactly as the property was accessed before.

### 4.4 Consumer usage

```csharp
await using var quotes  = await streaming.MarketDataAsync(conid, fields);
await using var summary = await streaming.AccountSummaryAsync("DU1234567");
await using var status = streaming.SubscribeSessionStatus();   // unsolicited: synchronous acquire, async-disposable

quotes.Stream.Subscribe(tick => ...);
await streaming.ConnectAsync();

// Later — leaving scope sends "umd+{conid}+{}" / "usd+DU1234567+{}" and completes the streams,
// or explicitly: await quotes.UnsubscribeAsync();
```

## 5. Internal design

### 5.1 Cancel-message ownership

`StreamingOperations` (which already builds subscribe messages and knows each topic) builds the paired **cancel** message and passes both down. `IbkrWebSocketClient` stays topic-agnostic — it never hardcodes topic strings.

### 5.2 `IIbkrWebSocketClient` changes

- `SubscribeTopicAsync` gains a `string? cancelMessage` parameter and returns
  `(ChannelReader<JsonElement> Reader, Func<CancellationToken, ValueTask> Unsubscribe)` — the unsubscribe delegate becomes **async** so it can await the wire send.
- `RegisterUnsolicitedTopic` returns the same shape; its `cancelMessage` is null and its delegate does local teardown wrapped in a completed `ValueTask`.
- Replace the flat `_activeSubscriptions` (`List<string>`) with a subscription **registry**:

  ```csharp
  private sealed record TopicSubscription(
      string TopicPrefix,
      string SubscribeMessage,
      string? CancelMessage,
      ChannelWriter<JsonElement> Writer);
  ```

  Only solicited subscriptions get a registry entry (unsolicited topics send no wire subscribe and are not replayed — unchanged from today).
- **Routing** (`_subscribers`: prefix → writer list) is unchanged; the hot `ProcessMessage` path stays as-is.
- **Replay** on (re)connect = the **distinct** `SubscribeMessage`s across registry entries (dedupes repeated conids, which the current list does not).
- **Refcount** for a cancel = the count of registry entries sharing the same `CancelMessage`.

### 5.3 Refcount key = cancel message

Using the cancel message itself as the refcount key is exact: two market-data handles for the same conid produce the same `umd+{conid}+{}` regardless of their `fields`; singletons share one key by construction; account topics key on `usd+{accountId}+{}` / `uld+{accountId}+{}`. No separate key concept is needed.

### 5.4 Unsubscribe flow

Under `_subscriptionLock` (decision computed atomically; the async send happens *after* releasing the lock — never hold a lock across `await`):

1. Remove this subscription's entry from the registry.
2. Remove its writer from `_subscribers[prefix]` and `TryComplete()` it (today's local teardown).
3. Compute `shouldSendCancel = CancelMessage is not null && no remaining entry shares CancelMessage && socket is open`.

Then, outside the lock: if `shouldSendCancel`, `await SendTextAsync(cancelMessage, ct)`. Best-effort — a send failure is logged and swallowed; local teardown already completed.

### 5.5 Handle implementation

An internal `IbkrSubscription<T>` wraps the observable (`Stream`) and the unsubscribe delegate, with an `Interlocked` guard so the underlying delegate runs at most once:

```csharp
public ValueTask UnsubscribeAsync(CancellationToken ct = default) =>
    Interlocked.Exchange(ref _unsubscribed, 1) == 0
        ? _unsubscribe(ct)
        : ValueTask.CompletedTask;

public ValueTask DisposeAsync() => UnsubscribeAsync(CancellationToken.None);
```

`StreamingOperations` constructs the `Stream` (`ChannelObservable` / `FanOutChannelObservable` as today) and wraps it plus the delegate in the handle.

## 6. Reconnect, edge cases, error handling

- **Reconnect replay.** Unsubscribe removes the entry, so its `SubscribeMessage` is no longer replayed; still-live subscriptions replay unchanged. Cancels are never replayed (a fresh connection has no server-side subscription to cancel).
- **Unsubscribe while disconnected** (mid-reconnect). `shouldSendCancel` is false (socket not open); local teardown + entry removal still happen, so the next connect won't resubscribe it — the correct end state with no wire message.
- **Idempotency.** Handle-level `Interlocked` guard → double-dispose / dispose-after-`UnsubscribeAsync` invokes the delegate once, sends one cancel.
- **Thread safety.** The "am I the last for this cancel key?" decision is made under the subscription lock; the async send runs after the lock is released.
- **Best-effort send** *(decision — revisitable)*. A wire-send failure during unsubscribe is logged and swallowed for both `UnsubscribeAsync` and `DisposeAsync`; the local stream is torn down regardless. Rationale: unsubscribe is cleanup, the observable is already completed, and `DisposeAsync` cannot cleanly surface exceptions.
- **Full client dispose** still completes all writers (unchanged); no per-topic cancels are needed because closing the connection ends everything server-side.

## 7. Observability

- New span `IbkrConduit.WebSocket.Unsubscribe` in the WS client, tagged with `TenantId` and `Topic` (mirrors the existing `IbkrConduit.WebSocket.Subscribe` span).
- New counter `ibkr.conduit.websocket.unsubscribe.count` tagged by `TenantId` and `Topic`.
- `ActiveSubscriptionCount` now reflects the registry and **decrements** on unsubscribe (previously only grew) — a useful health signal and a test hook.

## 8. Testing

Per the repo's TDD + xUnit v3 / Shouldly rules; a fake `IWebSocketAdapter` captures outbound frames.

**Unit (`Tests.Unit`)**
- Each topic: disposing the handle sends the exact cancel string — `umd+{conid}+{}`, `uor+{}`, `upl+{}`, `utr` (bare), `usd+{accountId}+{}`, `uld+{accountId}+{}`.
- Corrected subscribes: `ssd+{accountId}+{}` / `sld+{accountId}+{}`; with `keys`/`fields` → correct args JSON; omit-when-empty.
- Refcount: two market-data handles for the same conid → cancel sent only after both disposed; different conids → independent cancels. Same for account topics keyed by `accountId`.
- Unsubscribe completes `Stream` (`OnCompleted`) and stops delivery.
- Idempotent double-dispose → exactly one cancel.
- Unsubscribe then reconnect → the subscribe is **not** replayed; the cancel is not replayed.
- Unsubscribe while disconnected → no throw, local teardown, not replayed on next connect.
- Unsolicited handle dispose → local teardown only, no wire frame.
- Wire-send failure on unsubscribe → local teardown still completes, no throw.
- `ActiveSubscriptionCount` decrements on unsubscribe.

**Integration (`Tests.Integration`, WireMock + mock WS)**
- Capture/assert the outbound cancel frame end-to-end through the DI stack (extends the fake adapter to record sent frames).

**E2E (`[EnvironmentFact]`, live paper account)**
- Subscribe → unsubscribe → assert the stream completes / data stops, for market data and an account topic (confirms `usd+{accountId}+{}` accountful form live). Dovetails with the deferred `str` E2E.

## 9. Breaking changes / migration

Pre-1.0 (0.6.0); breaking changes are acceptable and called out in the CHANGELOG under `BREAKING`.

- `IStreamingOperations`: all six solicited return types change to `Task<IIbkrSubscription<T>>`; `AccountSummaryAsync`/`AccountLedgerAsync` gain required `accountId` + optional `keys`/`fields`; the five unsolicited **properties** become **methods** returning `IIbkrSubscription<T>`.
- New public type `IIbkrSubscription<T>`.
- Consumer migration: `var obs = await streaming.MarketDataAsync(…)` → `await using var sub = await streaming.MarketDataAsync(…); sub.Stream.Subscribe(…)`; unsolicited property access → `Subscribe…()` call.
- Update the streaming examples, the WebSocket E2E test, and the streaming docs.

## 10. Out of scope / follow-ups

- **Market-data 15-minute auto-termination.** `smd` streams end after 15 min and must be re-requested by 10 min; the library does not refresh them today. Separate feature.
- **Unimplemented topics.** Historical (`smh`/`umh`), BookTrader ladder (`sbd`/`ubd`), and option exercise (`shs`/`inp`) are neither subscribed nor unsubscribed by the library.
- **Key/field constant catalogs** for account summary/ledger (parallel to `MarketDataFields`).

## 11. Resolved decisions

- **Scope:** all topics, uniform (not market-data-only).
- **API shape:** subscription handles (`IIbkrSubscription<T>`) for every topic, solicited and unsolicited.
- **Refcount key:** the cancel message itself.
- **Account topics:** `accountId` required on subscribe *and* unsubscribe; optional `keys`/`fields` exposed.
- **Error handling:** best-effort log + swallow on unsubscribe send failure (revisitable).
