using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Streaming;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class StreamingOperationsTests
{
    [Fact]
    public void MarketData_BuildsCorrectTopicMessage()
    {
        var (ops, wsClient) = CreateOperations();

        ops.MarketData(265598, new[] { "31", "84", "86" }, TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("smd+265598+{\"fields\":[\"31\",\"84\",\"86\"]}");
        wsClient.LastTopicPrefix.ShouldBe("smd");
    }

    [Fact]
    public void OrderUpdates_WithoutDays_BuildsCorrectTopicMessage()
    {
        var (ops, wsClient) = CreateOperations();

        ops.OrderUpdates(cancellationToken: TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("sor+{}");
        wsClient.LastTopicPrefix.ShouldBe("sor");
    }

    [Fact]
    public void OrderUpdates_WithDays_BuildsCorrectTopicMessage()
    {
        var (ops, wsClient) = CreateOperations();

        ops.OrderUpdates(days: 3, cancellationToken: TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("sor+{\"days\":3}");
        wsClient.LastTopicPrefix.ShouldBe("sor");
    }

    [Fact]
    public void ProfitAndLoss_BuildsCorrectTopicMessage()
    {
        var (ops, wsClient) = CreateOperations();

        ops.ProfitAndLoss(TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("spl+{}");
        wsClient.LastTopicPrefix.ShouldBe("spl");
    }

    [Fact]
    public void AccountSummary_BuildsCorrectTopicMessage()
    {
        var (ops, wsClient) = CreateOperations();

        ops.AccountSummary(TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("ssd+{}");
        wsClient.LastTopicPrefix.ShouldBe("ssd");
    }

    [Fact]
    public void AccountLedger_BuildsCorrectTopicMessage()
    {
        var (ops, wsClient) = CreateOperations();

        ops.AccountLedger(TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("sld+{}");
        wsClient.LastTopicPrefix.ShouldBe("sld");
    }

    [Fact]
    public async Task MarketData_MapperExtractsFieldsFromJson()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var observable = ops.MarketData(265598, new[] { "31" }, ct);
        var received = new TaskCompletionSource<MarketDataTick>();
        using var sub = observable.Subscribe(new TestObserver<MarketDataTick>(
            onNext: t => received.TrySetResult(t)));

        var json = JsonDocument.Parse("""{"topic":"smd+265598","conid":265598,"_updated":1234567890,"31":"456.78"}""").RootElement;
        await wsClient.Channel.Writer.WriteAsync(json, ct);

        var tick = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        tick.Conid.ShouldBe(265598);
        tick.Updated.ShouldBe(1234567890);
        tick.Fields.ShouldNotBeNull();
        tick.Fields!["31"].ShouldBe("456.78");
    }

    [Fact]
    public async Task OrderUpdates_MapperDeserializesJson()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var observable = ops.OrderUpdates(cancellationToken: ct);
        var received = new TaskCompletionSource<OrderUpdate>();
        using var sub = observable.Subscribe(new TestObserver<OrderUpdate>(
            onNext: o => received.TrySetResult(o)));

        var json = JsonDocument.Parse("""{"topic":"sor","orderId":"123","conid":265598,"symbol":"AAPL","side":"BUY","size":100,"orderType":"LMT","price":150.0,"status":"Filled","filledQuantity":100,"remainingQuantity":0}""").RootElement;
        await wsClient.Channel.Writer.WriteAsync(json, ct);

        var order = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        order.OrderId.ShouldBe("123");
        order.Symbol.ShouldBe("AAPL");
        order.Status.ShouldBe("Filled");
    }

    [Fact]
    public async Task ProfitAndLoss_MapperDeserializesJson()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var observable = ops.ProfitAndLoss(ct);
        var received = new TaskCompletionSource<PnlUpdate>();
        using var sub = observable.Subscribe(new TestObserver<PnlUpdate>(
            onNext: p => received.TrySetResult(p)));

        var json = JsonDocument.Parse("""{"topic":"spl","acctId":"DU123","dpl":100.50,"upl":200.25,"rpl":50.75,"nl":50000.0}""").RootElement;
        await wsClient.Channel.Writer.WriteAsync(json, ct);

        var pnl = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        pnl.AccountId.ShouldBe("DU123");
        pnl.DailyPnl.ShouldBe(100.50m);
        pnl.NetLiquidation.ShouldBe(50000.0m);
    }

    private static (StreamingOperations Operations, FakeWebSocketClient Client) CreateOperations()
    {
        var wsClient = new FakeWebSocketClient();
        var ops = new StreamingOperations(wsClient);
        return (ops, wsClient);
    }

    internal sealed class FakeWebSocketClient : IIbkrWebSocketClient
    {
        public string? LastSubscribeMessage { get; private set; }
        public string? LastTopicPrefix { get; private set; }
        public Channel<JsonElement> Channel { get; } = System.Threading.Channels.Channel.CreateUnbounded<JsonElement>();

        public bool IsConnected => true;
        public int ActiveSubscriptionCount => 0;
        public DateTimeOffset? LastMessageReceivedAt => null;

        public Task ConnectAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<(ChannelReader<JsonElement> Reader, Action Unsubscribe)> SubscribeTopicAsync(
            string subscribeMessage,
            string topicPrefix,
            CancellationToken cancellationToken)
        {
            LastSubscribeMessage = subscribeMessage;
            LastTopicPrefix = topicPrefix;
            return Task.FromResult<(ChannelReader<JsonElement>, Action)>((Channel.Reader, () => { }));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TestObserver<T> : IObserver<T>
    {
        private readonly Action<T>? _onNext;
        private readonly Action<Exception>? _onError;
        private readonly Action? _onCompleted;

        public TestObserver(
            Action<T>? onNext = null,
            Action<Exception>? onError = null,
            Action? onCompleted = null)
        {
            _onNext = onNext;
            _onError = onError;
            _onCompleted = onCompleted;
        }

        public void OnNext(T value) => _onNext?.Invoke(value);
        public void OnError(Exception error) => _onError?.Invoke(error);
        public void OnCompleted() => _onCompleted?.Invoke();
    }
}
