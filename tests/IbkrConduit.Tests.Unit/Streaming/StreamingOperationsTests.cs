using System;
using System.Collections.Concurrent;
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
    public async Task MarketDataAsync_BuildsCorrectTopicMessage()
    {
        var (ops, wsClient) = CreateOperations();

        await ops.MarketDataAsync(265598, new[] { "31", "84", "86" }, TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("smd+265598+{\"fields\":[\"31\",\"84\",\"86\"]}");
        wsClient.LastTopicPrefix.ShouldBe("smd");
    }

    [Fact]
    public async Task OrderUpdatesAsync_WithoutDays_BuildsCorrectTopicMessage()
    {
        var (ops, wsClient) = CreateOperations();

        await ops.OrderUpdatesAsync(cancellationToken: TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("sor+{}");
        wsClient.LastTopicPrefix.ShouldBe("sor");
    }

    [Fact]
    public async Task OrderUpdatesAsync_WithDays_BuildsCorrectTopicMessage()
    {
        var (ops, wsClient) = CreateOperations();

        await ops.OrderUpdatesAsync(days: 3, cancellationToken: TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("sor+{\"days\":3}");
        wsClient.LastTopicPrefix.ShouldBe("sor");
    }

    [Fact]
    public async Task ProfitAndLossAsync_BuildsCorrectTopicMessage()
    {
        var (ops, wsClient) = CreateOperations();

        await ops.ProfitAndLossAsync(TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("spl+{}");
        wsClient.LastTopicPrefix.ShouldBe("spl");
    }

    [Fact]
    public async Task AccountSummaryAsync_BuildsCorrectTopicMessage()
    {
        var (ops, wsClient) = CreateOperations();

        await ops.AccountSummaryAsync(TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("ssd+{}");
        wsClient.LastTopicPrefix.ShouldBe("ssd");
    }

    [Fact]
    public async Task AccountLedgerAsync_BuildsCorrectTopicMessage()
    {
        var (ops, wsClient) = CreateOperations();

        await ops.AccountLedgerAsync(TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("sld+{}");
        wsClient.LastTopicPrefix.ShouldBe("sld");
    }

    [Fact]
    public async Task MarketDataAsync_MapperExtractsFieldsFromJson()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var observable = await ops.MarketDataAsync(265598, new[] { "31" }, ct);
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
    public async Task OrderUpdatesAsync_MapperDeserializesJson()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var observable = await ops.OrderUpdatesAsync(cancellationToken: ct);
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
    public async Task ProfitAndLossAsync_MapperDeserializesJson()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var observable = await ops.ProfitAndLossAsync(ct);
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

    [Fact]
    public async Task ConnectAsync_DelegatesToWebSocketClient()
    {
        var (ops, wsClient) = CreateOperations();

        await ((IStreamingOperations)ops).ConnectAsync(TestContext.Current.CancellationToken);

        wsClient.ConnectCallCount.ShouldBe(1);
    }

    [Fact]
    public void IsConnected_DelegatesToUnderlyingWebSocketClient()
    {
        var (ops, wsClient) = CreateOperations();

        wsClient.IsConnected = true;
        ((IStreamingOperations)ops).IsConnected.ShouldBeTrue();

        wsClient.IsConnected = false;
        ((IStreamingOperations)ops).IsConnected.ShouldBeFalse();
    }

    [Fact]
    public void LastMessageReceivedAt_DelegatesToUnderlyingWebSocketClient()
    {
        var (ops, wsClient) = CreateOperations();

        ((IStreamingOperations)ops).LastMessageReceivedAt.ShouldBeNull();

        var stamp = new DateTimeOffset(2026, 4, 30, 12, 0, 0, TimeSpan.Zero);
        wsClient.LastMessageReceivedAt = stamp;
        ((IStreamingOperations)ops).LastMessageReceivedAt.ShouldBe(stamp);
    }

    [Fact]
    public async Task SessionStatus_DeliversTypedEventOnTopicMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var observable = ((IStreamingOperations)ops).SessionStatus;
        var received = new TaskCompletionSource<SessionStatusEvent>();
        using var sub = observable.Subscribe(new TestObserver<SessionStatusEvent>(
            onNext: e => received.TrySetResult(e)));

        var json = JsonDocument.Parse("""{"topic":"sts","args":{"authenticated":true}}""").RootElement;
        await wsClient.UnsolicitedChannels["sts"].Writer.WriteAsync(json, ct);

        var evt = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        evt.Authenticated.ShouldBeTrue();
    }

    [Fact]
    public async Task SessionStatus_DeliversAuthenticatedFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var observable = ((IStreamingOperations)ops).SessionStatus;
        var received = new TaskCompletionSource<SessionStatusEvent>();
        using var sub = observable.Subscribe(new TestObserver<SessionStatusEvent>(
            onNext: e => received.TrySetResult(e)));

        var json = JsonDocument.Parse("""{"topic":"sts","args":{"authenticated":false}}""").RootElement;
        await wsClient.UnsolicitedChannels["sts"].Writer.WriteAsync(json, ct);

        var evt = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        evt.Authenticated.ShouldBeFalse();
    }

    [Fact]
    public async Task Bulletins_DeliversTypedEventOnTopicMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var observable = ((IStreamingOperations)ops).Bulletins;
        var received = new TaskCompletionSource<BulletinEvent>();
        using var sub = observable.Subscribe(new TestObserver<BulletinEvent>(
            onNext: e => received.TrySetResult(e)));

        var json = JsonDocument.Parse("""{"topic":"blt","args":{"id":"B-42","message":"Exchange XYZ delayed"}}""").RootElement;
        await wsClient.UnsolicitedChannels["blt"].Writer.WriteAsync(json, ct);

        var evt = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        evt.Id.ShouldBe("B-42");
        evt.Message.ShouldBe("Exchange XYZ delayed");
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

        public bool IsConnected { get; set; } = true;
        public int ActiveSubscriptionCount => 0;
        public DateTimeOffset? LastMessageReceivedAt { get; set; }

        public int ConnectCallCount { get; private set; }

        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            ConnectCallCount++;
            return Task.CompletedTask;
        }

        public Task<(ChannelReader<JsonElement> Reader, Action Unsubscribe)> SubscribeTopicAsync(
            string subscribeMessage,
            string topicPrefix,
            CancellationToken cancellationToken)
        {
            LastSubscribeMessage = subscribeMessage;
            LastTopicPrefix = topicPrefix;
            return Task.FromResult<(ChannelReader<JsonElement>, Action)>((Channel.Reader, () => { }));
        }

        public ConcurrentDictionary<string, Channel<JsonElement>> UnsolicitedChannels { get; } = new();

        public (ChannelReader<JsonElement> Reader, Action Unsubscribe) RegisterUnsolicitedTopic(string topicPrefix)
        {
            var channel = UnsolicitedChannels.GetOrAdd(
                topicPrefix,
                _ => System.Threading.Channels.Channel.CreateUnbounded<JsonElement>());
            return (channel.Reader, () => { });
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
