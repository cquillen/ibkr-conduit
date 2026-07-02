# WebSocket Trade Execution Stream (`str`) — Design

**Date:** 2026-07-02
**Status:** Approved (pending spec review)
**Milestone:** Extends Milestone 5 (WebSocket Streaming)

## 1. Goal

Add a real-time **trade execution** stream over the existing IBKR WebSocket
connection, subscribing to IBKR's `str` topic. This surfaces individual
execution/fill records (price, size, side, net amount, exchange, timestamps,
etc.) as they occur, distinct from the already-implemented `sor` order-status
stream (`OrderUpdatesAsync`).

`str` is **not** a mirror of the REST `GET /iserver/account/trades` endpoint:
the REST `Trade` model has 8 fields; the `str` frame carries 23. This work
introduces a dedicated `TradeExecution` model — no reuse of `Trade`.

## 2. IBKR `str` Protocol (authoritative)

### Subscribe

```
str+{ "realtimeUpdatesOnly": <bool>, "days": <int> }
```

- `realtimeUpdatesOnly` (optional, default `false`): when `true`, suppress
  historical executions and stream only new real-time fills.
- `days` (optional, default `1`): number of days of historical executions to
  return on subscribe.
- Both optional; empty argument is `str+{}`.

### Response frame

```json
{
  "topic": "str",
  "args": [
    {
      "execution_id": "...",
      "symbol": "...",
      "supports_tax_opt": "...",
      "side": "...",
      "order_description": "{SIDE} {SIZE} @ {PRICE} on {EXCHANGE}",
      "trade_time": "YYYYMMDD-HH:mm:ss",
      "trade_time_r": 1730000000000,
      "size": 100,
      "order_ref": "...",
      "price": "123.45",
      "exchange": "...",
      "net_amount": 12345.0,
      "account": "...",
      "accountCode": "...",
      "company_name": "...",
      "contract_description_1": "...",
      "contract_description_2": "...",
      "sec_type": "...",
      "conid": 265598,
      "conidEx": "...",
      "open_close": "...",
      "liquidation_trade": "...",
      "is_event_trading": "..."
    }
  ]
}
```

Key structural fact: **`args` is an array of execution objects** — one frame can
carry many executions. No existing topic in this library fans out an array
(`sor`/`spl` deserialize the frame root into one record; `sts`/`ntf`/`act` read
`args` as a single object).

### Cancel

IBKR defines `utr` to cancel the trades subscription. **Out of scope** for this
work (see §8).

## 3. Public API

One new method on `IStreamingOperations` (already surfaced to consumers via
`IIbkrClient.Streaming` — no facade change required):

```csharp
/// <summary>
/// Subscribes to the real-time trade execution stream (IBKR <c>str</c> topic).
/// Emits one item per execution. On subscribe IBKR replays up to <paramref name="days"/>
/// of historical executions unless <paramref name="realtimeUpdatesOnly"/> is true;
/// the same replay occurs after any reconnect, so consumers should dedupe on
/// <see cref="TradeExecution.ExecutionId"/>.
/// </summary>
/// <param name="realtimeUpdatesOnly">When true, suppress historical executions and stream new fills only. Omitted from the wire message when null (IBKR default: false).</param>
/// <param name="days">Days of historical executions to include on subscribe. Omitted from the wire message when null (IBKR default: 1).</param>
/// <param name="cancellationToken">Cancellation token.</param>
Task<IObservable<TradeExecution>> TradeExecutionsAsync(
    bool? realtimeUpdatesOnly = null,
    int? days = null,
    CancellationToken cancellationToken = default);
```

### Subscribe-message building

Mirror `OrderUpdatesAsync`: include only supplied arguments.

| `realtimeUpdatesOnly` | `days` | Wire message |
|---|---|---|
| null | null | `str+{}` |
| true | null | `str+{"realtimeUpdatesOnly":true}` |
| null | 3 | `str+{"days":3}` |
| true | 3 | `str+{"realtimeUpdatesOnly":true,"days":3}` |

`topicPrefix = "str"`. Routing already works: `IbkrWebSocketClient.ProcessMessage`
keys subscribers on the pre-`+` prefix of the frame's `topic`.

## 4. Model — `TradeExecution`

New public record in `src/IbkrConduit/Streaming/StreamingModels.cs`, following the
existing streaming-model conventions (`[JsonPropertyName]`, `[JsonExtensionData]`,
`[ExcludeFromCodeCoverage]` — it is a trivial DTO).

Type choices are faithful to the `str` wire format (**not** the REST `Trade` model):

| Property | JSON name | Type | Notes |
|---|---|---|---|
| `ExecutionId` | `execution_id` | `string` | Natural dedupe key |
| `Symbol` | `symbol` | `string` | |
| `SupportsTaxOpt` | `supports_tax_opt` | `string?` | Client Portal only |
| `Side` | `side` | `string` | BUY / SELL |
| `OrderDescription` | `order_description` | `string?` | `{SIDE} {SIZE} @ {PRICE} on {EXCHANGE}` |
| `TradeTime` | `trade_time` | `string?` | `YYYYMMDD-HH:mm:ss` UTC, kept raw |
| `TradeTimeR` | `trade_time_r` | `long?` | Epoch (ms); `long` to avoid int overflow |
| `Size` | `size` | `decimal` | JSON number |
| `OrderRef` | `order_ref` | `string?` | cOID from placement |
| `Price` | `price` | `decimal` | IBKR sends quoted; parsed via `AllowReadingFromString` |
| `Exchange` | `exchange` | `string?` | |
| `NetAmount` | `net_amount` | `decimal` | Total after multiplier |
| `Account` | `account` | `string` | |
| `AccountCode` | `accountCode` | `string?` | |
| `CompanyName` | `company_name` | `string?` | |
| `ContractDescription1` | `contract_description_1` | `string?` | Underlying symbol |
| `ContractDescription2` | `contract_description_2` | `string?` | Full derivative description |
| `SecType` | `sec_type` | `string?` | |
| `Conid` | `conid` | `int` | |
| `ConidEx` | `conidEx` | `string?` | |
| `OpenClose` | `open_close` | `string?` | `"???"` when position already open |
| `LiquidationTrade` | `liquidation_trade` | `string?` | |
| `IsEventTrading` | `is_event_trading` | `string?` | |
| `AdditionalData` | (extension) | `Dictionary<string, JsonElement>?` | `[JsonExtensionData]` forward-compat |

`Price` is `decimal` (per approved decision) — the mapper deserializes with
`JsonSerializerOptions { NumberHandling = JsonNumberHandling.AllowReadingFromString }`
so the quoted `price` (and any other string-encoded numbers) parse cleanly.

## 5. Fan-out infrastructure

`ChannelObservable<T>` is strictly 1 frame → 1 item and stays **unchanged**.

Add a sibling internal type `FanOutChannelObservable<T>` in
`src/IbkrConduit/Streaming/`:

- Constructor: `(ChannelReader<JsonElement> reader, Func<JsonElement, IEnumerable<T>> mapper)`.
- Pump: for each frame read from the channel, iterate the mapper's result and
  call `observer.OnNext` per element. Identical completion/error/cancellation
  semantics to `ChannelObservable<T>` (OnCompleted on channel completion or
  cancellation; OnError on unexpected exception).

Mapper `TradeExecutionMapper.MapMany(JsonElement frame) : IEnumerable<TradeExecution>`:

- If `frame` has no `args` property, or `args` is not a JSON array → yield nothing.
- Otherwise, for each array element, `JsonSerializer.Deserialize<TradeExecution>`
  (with the `AllowReadingFromString` options); skip any element that deserializes
  to `null`.

`StreamingOperations.TradeExecutionsAsync` wires it together:

```csharp
var (reader, _) = await _webSocketClient.SubscribeTopicAsync(subscribeMessage, "str", cancellationToken);
return new FanOutChannelObservable<TradeExecution>(reader, TradeExecutionMapper.MapMany);
```

## 6. Data flow

```
IBKR str frame
  → IbkrWebSocketClient.ProcessMessage (routes whole envelope by prefix "str")
  → per-subscriber bounded Channel<JsonElement>
  → FanOutChannelObservable<TradeExecution> pump
  → TradeExecutionMapper.MapMany splits args[]
  → observer.OnNext(execution)  // once per execution
```

## 7. Reconnect & dedupe

Reuses the existing subscription-replay machinery: the `str+{…}` message is held
in `_activeSubscriptions` and resent automatically on reconnect (session refresh,
tickle-watchdog, transport drop). Because IBKR replays `days` of history on each
(re)subscribe, duplicate executions across reconnects are expected. `execution_id`
is the dedupe key — same contract as `BulletinEvent.Id`. The library does not
dedupe; the behavior is documented on the API.

## 8. Out of scope

- **`utr` cancel wire message.** The current unsubscribe path removes the local
  subscriber and drops the message from `_activeSubscriptions` but sends no wire
  unsubscribe — true for every topic today. `str` stays consistent. A general
  "send the `u…` counterpart on unsubscribe" capability is a separate,
  cross-cutting enhancement, tracked as a future follow-up.
- Library-side dedupe of replayed executions (documented, consumer's responsibility).
- Parsing `trade_time` / `trade_time_r` into `DateTimeOffset` (raw values exposed).

## 9. Observability

Follows the existing streaming convention: `StreamingOperations` methods do not
open their own `Activity`. The span is `IbkrConduit.WebSocket.Subscribe`, opened
in `IbkrWebSocketClient.SubscribeTopicAsync` and tagged `topic="str"` and
`TenantId`. `messages.received` is already counted per-topic (`topic="str"`).
This matches `OrderUpdatesAsync` and the other streaming methods; the
`*Operations` per-method span rule applies to the REST operations classes, which
own their HTTP calls.

## 10. Testing (TDD — red → green per task)

### Unit — `StreamingOperationsTests`
- `TradeExecutionsAsync_NoArgs_BuildsCorrectTopicMessage` → `str+{}`, prefix `str`.
- `TradeExecutionsAsync_RealtimeOnly_BuildsCorrectTopicMessage` → `str+{"realtimeUpdatesOnly":true}`.
- `TradeExecutionsAsync_WithDays_BuildsCorrectTopicMessage` → `str+{"days":3}`.
- `TradeExecutionsAsync_RealtimeOnlyAndDays_BuildsCorrectTopicMessage` → combined.
- `TradeExecutionsAsync_FrameWithMultipleExecutions_EmitsOnePerExecution` → a
  frame with a 2-element `args` yields two `OnNext` with correct field values.
- `TradeExecutionsAsync_FrameWithNoArgs_EmitsNothing`.

### Unit — `TradeExecutionMapperTests`
- Field-fidelity: all 23 fields mapped from a representative frame element,
  including `price` (string) → `decimal`.
- Unknown property lands in `AdditionalData`.
- Non-array / missing `args` → empty.

### Integration — WireMock + mock WebSocket (existing streaming harness)
- Subscribe → server pushes a 2-execution `str` frame → both executions observed
  with correct fields.
- Reconnect replays the `str` subscription (existing reconnect-resubscription
  test pattern; no 401-recovery test — that path is REST-only).

### E2E — `[EnvironmentFact]`, `[Collection("IBKR E2E")]`
- Via the full DI pipeline (`AddIbkrClient` + `OAuthCredentialsFactory.FromEnvironment`):
  connect, `TradeExecutionsAsync(days: 1)`, observe any historical executions
  from the paper account. Follows the existing WebSocket E2E convention.

## 11. Documentation

- Add the `str` row to design-doc §12.5 "Key WebSocket Topics":
  `Trade executions | str+{} (opts: realtimeUpdatesOnly, days) | Real-time execution/fill records`.
- Update `docs/implementation-status.md` (Milestone 5 addendum or a small
  "Streaming — Trade Executions" entry).

## 12. Files touched

| File | Change |
|---|---|
| `src/IbkrConduit/Streaming/StreamingModels.cs` | Add `TradeExecution` record |
| `src/IbkrConduit/Streaming/Mappers/TradeExecutionMapper.cs` | New — `MapMany` |
| `src/IbkrConduit/Streaming/FanOutChannelObservable.cs` | New — array fan-out observable |
| `src/IbkrConduit/Client/IStreamingOperations.cs` | Add `TradeExecutionsAsync` |
| `src/IbkrConduit/Client/StreamingOperations.cs` | Implement `TradeExecutionsAsync` |
| `tests/IbkrConduit.Tests.Unit/Streaming/StreamingOperationsTests.cs` | Add tests |
| `tests/IbkrConduit.Tests.Unit/Streaming/TradeExecutionMapperTests.cs` | New |
| `tests/IbkrConduit.Tests.Integration/…` | Add `str` integration test |
| `docs/ibkr_conduit_design.md` | §12.5 topic row |
| `docs/implementation-status.md` | Status entry |
