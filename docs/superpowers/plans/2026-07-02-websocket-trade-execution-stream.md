# WebSocket Trade Execution Stream (`str`) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a real-time trade-execution stream (`IStreamingOperations.TradeExecutionsAsync`) that subscribes to IBKR's WebSocket `str` topic and emits one `TradeExecution` per fill.

**Architecture:** Follows the existing solicited-topic pattern (like `OrderUpdatesAsync`) but adds the library's first array-fan-out: the `str` frame carries `args` as an array of executions, so a new `FanOutChannelObservable<T>` emits one item per array element. A dedicated `TradeExecution` model (23 fields, distinct from the 8-field REST `Trade`) with `price` parsed as `decimal`.

**Tech Stack:** C#/.NET 10, `System.Text.Json`, `System.Threading.Channels`, xUnit v3 + Shouldly (MTP runner). Spec: `docs/superpowers/specs/2026-07-02-websocket-trade-execution-stream-design.md`.

**Deviation from spec (§10):** The spec's integration test ("mock server pushes a 2-execution `str` frame") is replaced by a **real-client end-to-end test** in the Unit project (Task 4) driving the real `IbkrWebSocketClient` + real `StreamingOperations` via `FakeWebSocketAdapter.EnqueueServerMessage`. Rationale: `MockWebSocketServer` never originates frames (it only drains), so the DI-stack harness cannot push a `str` frame without new infra; the real-client end-to-end test exercises the full vertical (real `ProcessMessage` routing → real mapper → real fan-out → observable) at higher fidelity than a mock-server round-trip. The `str`-specific send path (subscribe message) is already covered by unit tests. If a true DI-stack test is later desired, extend `MockWebSocketServer` to record/originate frames as a separate task.

---

## File Structure

| File | Responsibility |
|---|---|
| `src/IbkrConduit/Streaming/StreamingModels.cs` (modify) | Add `TradeExecution` record |
| `src/IbkrConduit/Streaming/Mappers/TradeExecutionMapper.cs` (create) | `MapMany`: split `args[]` → `IEnumerable<TradeExecution>` |
| `src/IbkrConduit/Streaming/FanOutChannelObservable.cs` (create) | Observable that emits one item per element of a `Func<JsonElement, IEnumerable<T>>` result |
| `src/IbkrConduit/Client/IStreamingOperations.cs` (modify) | Add `TradeExecutionsAsync` |
| `src/IbkrConduit/Client/StreamingOperations.cs` (modify) | Implement `TradeExecutionsAsync` |
| `tests/IbkrConduit.Tests.Unit/Streaming/TradeExecutionMapperTests.cs` (create) | Mapper fidelity + edge cases |
| `tests/IbkrConduit.Tests.Unit/Streaming/FanOutChannelObservableTests.cs` (create) | Fan-out emission semantics |
| `tests/IbkrConduit.Tests.Unit/Streaming/StreamingOperationsTests.cs` (modify) | Topic-message building + fan-out emission via fake client |
| `tests/IbkrConduit.Tests.Unit/Streaming/IbkrWebSocketClientTests.cs` (modify) | Real-client end-to-end emission |
| `docs/ibkr_conduit_design.md` (modify) | §12.5 topic row |
| `docs/implementation-status.md` (modify) | Status entry |

---

## Task 1: `TradeExecution` model + `TradeExecutionMapper`

**Files:**
- Modify: `src/IbkrConduit/Streaming/StreamingModels.cs` (append record)
- Create: `src/IbkrConduit/Streaming/Mappers/TradeExecutionMapper.cs`
- Test: `tests/IbkrConduit.Tests.Unit/Streaming/TradeExecutionMapperTests.cs`

- [ ] **Step 1: Write the failing mapper tests**

Create `tests/IbkrConduit.Tests.Unit/Streaming/TradeExecutionMapperTests.cs`:

```csharp
using System.Linq;
using System.Text.Json;
using IbkrConduit.Streaming;
using IbkrConduit.Streaming.Mappers;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class TradeExecutionMapperTests
{
    private const string TwoExecutionFrame = """
        {
          "topic":"str",
          "args":[
            {
              "execution_id":"0000e0d5.1","symbol":"AAPL","supports_tax_opt":"1",
              "side":"B","order_description":"BUY 100 @ 150.25 on NASDAQ",
              "trade_time":"20260702-14:30:05","trade_time_r":1751466605000,
              "size":100,"order_ref":"my-coid-1","price":"150.25","exchange":"NASDAQ",
              "net_amount":15025.0,"account":"DU111","accountCode":"DU111",
              "company_name":"APPLE INC","contract_description_1":"AAPL",
              "contract_description_2":"","sec_type":"STK","conid":265598,
              "conidEx":"265598","open_close":"???","liquidation_trade":"0",
              "is_event_trading":"0"
            },
            {
              "execution_id":"0000e0d5.2","symbol":"MSFT","side":"S",
              "size":50,"price":"420.10","conid":272093,"account":"DU111",
              "net_amount":21005.0,"trade_time_r":1751466606000
            }
          ]
        }
        """;

    [Fact]
    public void MapMany_FrameWithTwoExecutions_ReturnsBothWithFieldsMapped()
    {
        var frame = JsonDocument.Parse(TwoExecutionFrame).RootElement;

        var executions = TradeExecutionMapper.MapMany(frame).ToList();

        executions.Count.ShouldBe(2);
        var first = executions[0];
        first.ExecutionId.ShouldBe("0000e0d5.1");
        first.Symbol.ShouldBe("AAPL");
        first.Side.ShouldBe("B");
        first.Size.ShouldBe(100m);
        first.Price.ShouldBe(150.25m);          // "150.25" (string) parsed to decimal
        first.NetAmount.ShouldBe(15025.0m);
        first.Conid.ShouldBe(265598);
        first.ConidEx.ShouldBe("265598");
        first.OrderRef.ShouldBe("my-coid-1");
        first.Exchange.ShouldBe("NASDAQ");
        first.TradeTime.ShouldBe("20260702-14:30:05");
        first.TradeTimeR.ShouldBe(1751466605000);
        first.OpenClose.ShouldBe("???");
        first.SecType.ShouldBe("STK");
        first.CompanyName.ShouldBe("APPLE INC");
        executions[1].Symbol.ShouldBe("MSFT");
        executions[1].Price.ShouldBe(420.10m);
    }

    [Fact]
    public void MapMany_UnknownField_LandsInAdditionalData()
    {
        var frame = JsonDocument.Parse(
            """{"topic":"str","args":[{"execution_id":"x","brand_new_field":"42"}]}""").RootElement;

        var execution = TradeExecutionMapper.MapMany(frame).Single();

        execution.AdditionalData.ShouldNotBeNull();
        execution.AdditionalData!.ShouldContainKey("brand_new_field");
    }

    [Fact]
    public void MapMany_MissingArgs_ReturnsEmpty()
    {
        var frame = JsonDocument.Parse("""{"topic":"str"}""").RootElement;

        TradeExecutionMapper.MapMany(frame).ShouldBeEmpty();
    }

    [Fact]
    public void MapMany_ArgsNotAnArray_ReturnsEmpty()
    {
        var frame = JsonDocument.Parse("""{"topic":"str","args":{"execution_id":"x"}}""").RootElement;

        TradeExecutionMapper.MapMany(frame).ShouldBeEmpty();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail (compile failure — types don't exist)**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-class "*TradeExecutionMapperTests*"`
Expected: FAIL — build error, `TradeExecution` / `TradeExecutionMapper` not found.

- [ ] **Step 3: Add the `TradeExecution` record**

Append to `src/IbkrConduit/Streaming/StreamingModels.cs` (after the `AccountLedgerUpdate` record, before the `SessionStatusEvent` record — keep it with the other financial DTOs):

```csharp
/// <summary>
/// A real-time trade execution (fill) from the WebSocket <c>str</c> topic. One item
/// is emitted per execution. On subscribe IBKR replays historical executions (up to
/// the requested <c>days</c>) and repeats them after any reconnect, so consumers
/// should dedupe on <see cref="ExecutionId"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public record TradeExecution
{
    /// <summary>Execution identifier of the specific trade. Natural dedupe key.</summary>
    [JsonPropertyName("execution_id")]
    public string ExecutionId { get; init; } = string.Empty;

    /// <summary>Ticker symbol of the traded contract.</summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Whether the contract supports the tax optimizer (Client Portal only).</summary>
    [JsonPropertyName("supports_tax_opt")]
    public string? SupportsTaxOpt { get; init; }

    /// <summary>Trade side (buy or sell).</summary>
    [JsonPropertyName("side")]
    public string Side { get; init; } = string.Empty;

    /// <summary>Full order description, formatted "{SIDE} {SIZE} @ {PRICE} on {EXCHANGE}".</summary>
    [JsonPropertyName("order_description")]
    public string? OrderDescription { get; init; }

    /// <summary>Trade date-time in UTC, formatted "YYYYMMDD-HH:mm:ss". Kept raw.</summary>
    [JsonPropertyName("trade_time")]
    public string? TradeTime { get; init; }

    /// <summary>Trade date-time of the execution in epoch milliseconds.</summary>
    [JsonPropertyName("trade_time_r")]
    public long? TradeTimeR { get; init; }

    /// <summary>Quantity of shares traded.</summary>
    [JsonPropertyName("size")]
    public decimal Size { get; init; }

    /// <summary>Custom order identifier (cOID) supplied at order placement, if any.</summary>
    [JsonPropertyName("order_ref")]
    public string? OrderRef { get; init; }

    /// <summary>Execution price. IBKR sends this as a quoted string; parsed to decimal.</summary>
    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    /// <summary>Exchange the order executed at.</summary>
    [JsonPropertyName("exchange")]
    public string? Exchange { get; init; }

    /// <summary>Total amount traded after applying the contract multiplier.</summary>
    [JsonPropertyName("net_amount")]
    public decimal NetAmount { get; init; }

    /// <summary>Account the order was traded on.</summary>
    [JsonPropertyName("account")]
    public string Account { get; init; } = string.Empty;

    /// <summary>Account code the order was traded on.</summary>
    [JsonPropertyName("accountCode")]
    public string? AccountCode { get; init; }

    /// <summary>Title of the company for the contract.</summary>
    [JsonPropertyName("company_name")]
    public string? CompanyName { get; init; }

    /// <summary>Underlying symbol of the contract.</summary>
    [JsonPropertyName("contract_description_1")]
    public string? ContractDescription1 { get; init; }

    /// <summary>Full description of the derivative.</summary>
    [JsonPropertyName("contract_description_2")]
    public string? ContractDescription2 { get; init; }

    /// <summary>Security type traded (e.g., STK, OPT, FUT).</summary>
    [JsonPropertyName("sec_type")]
    public string? SecType { get; init; }

    /// <summary>Contract identifier for the traded contract.</summary>
    [JsonPropertyName("conid")]
    public int Conid { get; init; }

    /// <summary>The conidEx of the order if specified; otherwise the conid.</summary>
    [JsonPropertyName("conidEx")]
    public string? ConidEx { get; init; }

    /// <summary>Whether the execution was a closing trade. "???" when the position was already open but not a closing order.</summary>
    [JsonPropertyName("open_close")]
    public string? OpenClose { get; init; }

    /// <summary>Whether the trade resulted from a liquidation.</summary>
    [JsonPropertyName("liquidation_trade")]
    public string? LiquidationTrade { get; init; }

    /// <summary>Whether the order can be used with EventTrader.</summary>
    [JsonPropertyName("is_event_trading")]
    public string? IsEventTrading { get; init; }

    /// <summary>Additional data not mapped to known properties.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
```

- [ ] **Step 4: Create the mapper**

Create `src/IbkrConduit/Streaming/Mappers/TradeExecutionMapper.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrConduit.Streaming.Mappers;

/// <summary>
/// Maps a <c>str</c> WebSocket frame to zero or more <see cref="TradeExecution"/> records
/// by fanning out the frame's <c>args</c> array. IBKR sends <c>price</c> as a quoted
/// string, so <see cref="_options"/> enables reading numbers from strings.
/// </summary>
internal static class TradeExecutionMapper
{
    private static readonly JsonSerializerOptions _options = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>Yields one <see cref="TradeExecution"/> per element of the frame's <c>args</c> array. Missing or non-array <c>args</c> yields nothing.</summary>
    public static IEnumerable<TradeExecution> MapMany(JsonElement frame)
    {
        if (!frame.TryGetProperty("args", out var args) || args.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var element in args.EnumerateArray())
        {
            var execution = element.Deserialize<TradeExecution>(_options);
            if (execution is not null)
            {
                yield return execution;
            }
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-class "*TradeExecutionMapperTests*"`
Expected: PASS (4 tests).

- [ ] **Step 6: Commit**

```bash
git add src/IbkrConduit/Streaming/StreamingModels.cs src/IbkrConduit/Streaming/Mappers/TradeExecutionMapper.cs tests/IbkrConduit.Tests.Unit/Streaming/TradeExecutionMapperTests.cs
git commit -m "feat(streaming): add TradeExecution model and str-frame mapper"
```

---

## Task 2: `FanOutChannelObservable<T>`

**Files:**
- Create: `src/IbkrConduit/Streaming/FanOutChannelObservable.cs`
- Test: `tests/IbkrConduit.Tests.Unit/Streaming/FanOutChannelObservableTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/IbkrConduit.Tests.Unit/Streaming/FanOutChannelObservableTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using IbkrConduit.Streaming;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class FanOutChannelObservableTests
{
    // Mapper: return one int per element of the frame's "args" array.
    private static IEnumerable<int> MapArgs(JsonElement frame) =>
        frame.GetProperty("args").EnumerateArray().Select(e => e.GetInt32());

    [Fact]
    public async Task Subscribe_FrameWithThreeElements_EmitsOnePerElement()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new FanOutChannelObservable<int>(channel.Reader, MapArgs);

        var received = new List<int>();
        var done = new TaskCompletionSource();
        using var sub = observable.Subscribe(new CollectingObserver<int>(v =>
        {
            received.Add(v);
            if (received.Count == 3)
            {
                done.TrySetResult();
            }
        }));

        await channel.Writer.WriteAsync(
            JsonDocument.Parse("""{"args":[10,20,30]}""").RootElement, ct);

        await done.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        received.ShouldBe(new[] { 10, 20, 30 });
    }

    [Fact]
    public async Task Subscribe_EmptyArgs_EmitsNothingButStaysAlive()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new FanOutChannelObservable<int>(channel.Reader, MapArgs);

        var received = new List<int>();
        var got = new TaskCompletionSource<int>();
        using var sub = observable.Subscribe(new CollectingObserver<int>(v =>
        {
            received.Add(v);
            got.TrySetResult(v);
        }));

        await channel.Writer.WriteAsync(
            JsonDocument.Parse("""{"args":[]}""").RootElement, ct);
        await channel.Writer.WriteAsync(
            JsonDocument.Parse("""{"args":[99]}""").RootElement, ct);

        var value = await got.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        value.ShouldBe(99);
        received.ShouldBe(new[] { 99 });
    }

    [Fact]
    public async Task Subscribe_ChannelCompletes_CallsOnCompleted()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new FanOutChannelObservable<int>(channel.Reader, MapArgs);

        var completed = new TaskCompletionSource();
        using var sub = observable.Subscribe(
            new CollectingObserver<int>(_ => { }, onCompleted: () => completed.TrySetResult()));

        channel.Writer.Complete();

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        completed.Task.IsCompletedSuccessfully.ShouldBeTrue();
    }

    private sealed class CollectingObserver<T>(Action<T> onNext, Action? onCompleted = null) : IObserver<T>
    {
        public void OnNext(T value) => onNext(value);
        public void OnError(Exception error) { }
        public void OnCompleted() => onCompleted?.Invoke();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-class "*FanOutChannelObservableTests*"`
Expected: FAIL — build error, `FanOutChannelObservable` not found.

- [ ] **Step 3: Create the observable**

Create `src/IbkrConduit/Streaming/FanOutChannelObservable.cs`:

```csharp
using System.Text.Json;
using System.Threading.Channels;

namespace IbkrConduit.Streaming;

/// <summary>
/// <see cref="IObservable{T}"/> backed by a <see cref="ChannelReader{T}"/> of raw frames,
/// where each frame maps to zero or more <typeparamref name="T"/> items. One
/// <see cref="IObserver{T}.OnNext"/> is raised per mapped item. Used for topics whose
/// frame carries an <c>args</c> array (e.g. <c>str</c> trade executions), in contrast to
/// <see cref="ChannelObservable{T}"/> which maps one frame to exactly one item.
/// </summary>
/// <typeparam name="T">The type of items emitted to observers.</typeparam>
internal sealed class FanOutChannelObservable<T> : IObservable<T>
{
    private readonly ChannelReader<JsonElement> _reader;
    private readonly Func<JsonElement, IEnumerable<T>> _mapper;

    /// <summary>Creates a new <see cref="FanOutChannelObservable{T}"/>.</summary>
    /// <param name="reader">The channel reader to consume frames from.</param>
    /// <param name="mapper">Function mapping one frame to zero or more items.</param>
    public FanOutChannelObservable(ChannelReader<JsonElement> reader, Func<JsonElement, IEnumerable<T>> mapper)
    {
        _reader = reader;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<T> observer)
    {
        var cts = new CancellationTokenSource();
        _ = PumpAsync(observer, cts.Token);
        return new CancellationDisposable(cts);
    }

    private async Task PumpAsync(IObserver<T> observer, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in _reader.ReadAllAsync(cancellationToken))
            {
                foreach (var item in _mapper(frame))
                {
                    observer.OnNext(item);
                }
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

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-class "*FanOutChannelObservableTests*"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/IbkrConduit/Streaming/FanOutChannelObservable.cs tests/IbkrConduit.Tests.Unit/Streaming/FanOutChannelObservableTests.cs
git commit -m "feat(streaming): add FanOutChannelObservable for array-shaped frames"
```

---

## Task 3: `TradeExecutionsAsync` on `IStreamingOperations`

**Files:**
- Modify: `src/IbkrConduit/Client/IStreamingOperations.cs`
- Modify: `src/IbkrConduit/Client/StreamingOperations.cs`
- Test: `tests/IbkrConduit.Tests.Unit/Streaming/StreamingOperationsTests.cs`

- [ ] **Step 1: Write the failing tests**

Add these tests to the `StreamingOperationsTests` class in
`tests/IbkrConduit.Tests.Unit/Streaming/StreamingOperationsTests.cs` (after
`OrderUpdatesAsync_WithDays_BuildsCorrectTopicMessage`):

```csharp
    [Fact]
    public async Task TradeExecutionsAsync_NoArgs_BuildsCorrectTopicMessage()
    {
        var (ops, wsClient) = CreateOperations();

        await ops.TradeExecutionsAsync(cancellationToken: TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("str+{}");
        wsClient.LastTopicPrefix.ShouldBe("str");
    }

    [Fact]
    public async Task TradeExecutionsAsync_RealtimeOnly_BuildsCorrectTopicMessage()
    {
        var (ops, wsClient) = CreateOperations();

        await ops.TradeExecutionsAsync(realtimeUpdatesOnly: true, cancellationToken: TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("str+{\"realtimeUpdatesOnly\":true}");
        wsClient.LastTopicPrefix.ShouldBe("str");
    }

    [Fact]
    public async Task TradeExecutionsAsync_WithDays_BuildsCorrectTopicMessage()
    {
        var (ops, wsClient) = CreateOperations();

        await ops.TradeExecutionsAsync(days: 3, cancellationToken: TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("str+{\"days\":3}");
        wsClient.LastTopicPrefix.ShouldBe("str");
    }

    [Fact]
    public async Task TradeExecutionsAsync_RealtimeOnlyAndDays_BuildsCorrectTopicMessage()
    {
        var (ops, wsClient) = CreateOperations();

        await ops.TradeExecutionsAsync(realtimeUpdatesOnly: true, days: 3, cancellationToken: TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("str+{\"realtimeUpdatesOnly\":true,\"days\":3}");
        wsClient.LastTopicPrefix.ShouldBe("str");
    }

    [Fact]
    public async Task TradeExecutionsAsync_FrameWithMultipleExecutions_EmitsOnePerExecution()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var observable = await ops.TradeExecutionsAsync(cancellationToken: ct);
        var received = new System.Collections.Generic.List<TradeExecution>();
        var done = new TaskCompletionSource();
        using var sub = observable.Subscribe(new TestObserver<TradeExecution>(
            onNext: e =>
            {
                received.Add(e);
                if (received.Count == 2)
                {
                    done.TrySetResult();
                }
            }));

        var json = JsonDocument.Parse("""
            {"topic":"str","args":[
              {"execution_id":"e1","symbol":"AAPL","price":"150.25","size":100,"conid":265598},
              {"execution_id":"e2","symbol":"MSFT","price":"420.10","size":50,"conid":272093}
            ]}
            """).RootElement;
        await wsClient.Channel.Writer.WriteAsync(json, ct);

        await done.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        received.Count.ShouldBe(2);
        received[0].ExecutionId.ShouldBe("e1");
        received[0].Price.ShouldBe(150.25m);
        received[1].Symbol.ShouldBe("MSFT");
    }

    [Fact]
    public async Task TradeExecutionsAsync_FrameWithNoArgs_EmitsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var observable = await ops.TradeExecutionsAsync(cancellationToken: ct);
        var got = new TaskCompletionSource<TradeExecution>();
        using var sub = observable.Subscribe(new TestObserver<TradeExecution>(
            onNext: e => got.TrySetResult(e)));

        // A no-args str frame, then a valid one — only the valid execution should arrive.
        await wsClient.Channel.Writer.WriteAsync(
            JsonDocument.Parse("""{"topic":"str"}""").RootElement, ct);
        await wsClient.Channel.Writer.WriteAsync(
            JsonDocument.Parse("""{"topic":"str","args":[{"execution_id":"only"}]}""").RootElement, ct);

        var execution = await got.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        execution.ExecutionId.ShouldBe("only");
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-class "*StreamingOperationsTests*"`
Expected: FAIL — build error, `TradeExecutionsAsync` not defined on `IStreamingOperations`.

- [ ] **Step 3: Add the interface method**

In `src/IbkrConduit/Client/IStreamingOperations.cs`, add after the `OrderUpdatesAsync` method:

```csharp
    /// <summary>
    /// Subscribes to the real-time trade execution stream (IBKR <c>str</c> topic).
    /// Emits one item per execution. On subscribe IBKR replays historical executions
    /// (up to <paramref name="days"/>) unless <paramref name="realtimeUpdatesOnly"/> is
    /// true; the same replay occurs after any reconnect, so consumers should dedupe on
    /// <see cref="TradeExecution.ExecutionId"/>.
    /// </summary>
    /// <param name="realtimeUpdatesOnly">When true, suppress historical executions and stream new fills only. Omitted from the wire message when null (IBKR default: false).</param>
    /// <param name="days">Days of historical executions to include on subscribe. Omitted from the wire message when null (IBKR default: 1).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that resolves to an observable stream of trade executions.</returns>
    Task<IObservable<TradeExecution>> TradeExecutionsAsync(
        bool? realtimeUpdatesOnly = null,
        int? days = null,
        CancellationToken cancellationToken = default);
```

- [ ] **Step 4: Implement the method**

In `src/IbkrConduit/Client/StreamingOperations.cs`, add after `OrderUpdatesAsync`:

```csharp
    /// <inheritdoc />
    public async Task<IObservable<TradeExecution>> TradeExecutionsAsync(
        bool? realtimeUpdatesOnly = null,
        int? days = null,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>();
        if (realtimeUpdatesOnly.HasValue)
        {
            parts.Add($"\"realtimeUpdatesOnly\":{(realtimeUpdatesOnly.Value ? "true" : "false")}");
        }
        if (days.HasValue)
        {
            parts.Add($"\"days\":{days.Value}");
        }
        var subscribeMessage = $"str+{{{string.Join(",", parts)}}}";

        var (reader, _) = await _webSocketClient.SubscribeTopicAsync(subscribeMessage, "str", cancellationToken);

        return new FanOutChannelObservable<TradeExecution>(reader, TradeExecutionMapper.MapMany);
    }
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-class "*StreamingOperationsTests*"`
Expected: PASS (all existing + 6 new).

- [ ] **Step 6: Commit**

```bash
git add src/IbkrConduit/Client/IStreamingOperations.cs src/IbkrConduit/Client/StreamingOperations.cs tests/IbkrConduit.Tests.Unit/Streaming/StreamingOperationsTests.cs
git commit -m "feat(streaming): add TradeExecutionsAsync (str topic) to IStreamingOperations"
```

---

## Task 4: Real-client end-to-end emission test

**Files:**
- Test: `tests/IbkrConduit.Tests.Unit/Streaming/IbkrWebSocketClientTests.cs` (modify)

This task adds coverage only — no production change. It drives the **real**
`IbkrWebSocketClient` (via the existing `_adapter`, `_sessionApi`, `_notifier`,
`CreateClient` helpers in the test class) wrapped in the **real** `StreamingOperations`,
enqueues a `str` frame from the fake adapter, and asserts two `TradeExecution`s are
observed — exercising real `ProcessMessage` prefix routing → mapper → fan-out → observable.

- [ ] **Step 1: Write the failing test**

Add to the `IbkrWebSocketClientTests` class in
`tests/IbkrConduit.Tests.Unit/Streaming/IbkrWebSocketClientTests.cs`. First add
`using IbkrConduit.Client;` to the top of the file — it is required for
`StreamingOperations` and is the only namespace this test needs that the file does
not already import (`System.Collections.Generic`, `System.Threading.Tasks`, and
`IbkrConduit.Streaming` are already imported).

```csharp
    [Fact]
    public async Task TradeExecutions_EndToEnd_EmitsOnePerExecutionFromStrFrame()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var client = CreateClient();
        await client.ConnectAsync(ct);

        var ops = new StreamingOperations(client);
        var observable = await ops.TradeExecutionsAsync(cancellationToken: ct);

        var received = new List<TradeExecution>();
        var done = new TaskCompletionSource();
        using var sub = observable.Subscribe(new EndToEndObserver(e =>
        {
            received.Add(e);
            if (received.Count == 2)
            {
                done.TrySetResult();
            }
        }));

        _adapter.EnqueueServerMessage("""
            {"topic":"str","args":[
              {"execution_id":"e1","symbol":"AAPL","price":"150.25","size":100,"conid":265598},
              {"execution_id":"e2","symbol":"MSFT","price":"420.10","size":50,"conid":272093}
            ]}
            """);

        await done.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        received.Count.ShouldBe(2);
        received[0].ExecutionId.ShouldBe("e1");
        received[0].Price.ShouldBe(150.25m);
        received[1].Symbol.ShouldBe("MSFT");
    }

    private sealed class EndToEndObserver(Action<TradeExecution> onNext) : IObserver<TradeExecution>
    {
        public void OnNext(TradeExecution value) => onNext(value);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }
```

- [ ] **Step 2: Run the test to verify it passes (production code already complete)**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-method "*TradeExecutions_EndToEnd_EmitsOnePerExecutionFromStrFrame*"`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add tests/IbkrConduit.Tests.Unit/Streaming/IbkrWebSocketClientTests.cs
git commit -m "test(streaming): real-client end-to-end str execution emission"
```

---

## Task 5: Documentation

**Files:**
- Modify: `docs/ibkr_conduit_design.md`
- Modify: `docs/implementation-status.md`

- [ ] **Step 1: Add the `str` row to the WebSocket topics table**

In `docs/ibkr_conduit_design.md`, §12.5 "Key WebSocket Topics", add a row after the
Portfolio (`ssd+{}`) row:

```markdown
| Trade executions | `str+{}` (opts: `realtimeUpdatesOnly`, `days`) | Real-time execution/fill records |
```

- [ ] **Step 2: Add a status entry**

In `docs/implementation-status.md`, under the Milestone 5 table, add a row:

```markdown
| 5.7 | Trade execution stream (`str` topic) + array fan-out observable | Done |
```

- [ ] **Step 3: Commit**

```bash
git add docs/ibkr_conduit_design.md docs/implementation-status.md
git commit -m "docs(streaming): document str trade execution stream"
```

---

## Final verification (run before opening the PR)

- [ ] **Full check** (per CLAUDE.md):

```bash
dotnet build --configuration Release
dotnet test --configuration Release
dotnet format --verify-no-changes
```

Expected: build succeeds with zero warnings (TreatWarningsAsErrors), all tests pass, format clean.

- [ ] **Confirm the new public API surface** is intentional: `TradeExecution` and
  `TradeExecutionsAsync` are the only new public symbols; `FanOutChannelObservable<T>`
  and `TradeExecutionMapper` are `internal`.
