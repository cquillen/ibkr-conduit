using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Session;
using IbkrConduit.Streaming;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class IbkrWebSocketClientTests
{
    private readonly FakeWebSocketAdapter _adapter = new();
    private readonly FakeSessionApi _sessionApi = new();
    private readonly FakeLifecycleNotifier _notifier = new();
    private readonly IbkrOAuthCredentials _credentials;

    public IbkrWebSocketClientTests()
    {
        _credentials = new IbkrOAuthCredentials(
            TenantId: "test-tenant",
            ConsumerKey: "TESTKEY01",
            AccessToken: "test_access_token",
            EncryptedAccessTokenSecret: "dGVzdA==",
            SignaturePrivateKey: System.Security.Cryptography.RSA.Create(2048),
            EncryptionPrivateKey: System.Security.Cryptography.RSA.Create(2048),
            DhPrime: new System.Numerics.BigInteger(23));
    }

    [Fact]
    public async Task ConnectAsync_SetsUrlAndHeaders()
    {
        await using var client = CreateClient();

        await client.ConnectAsync(TestContext.Current.CancellationToken);

        _adapter.ConnectedUri.ShouldNotBeNull();
        _adapter.ConnectedUri!.ToString().ShouldContain("oauth_token=test_access_token");
        _adapter.RequestHeaders.ShouldContainKey("Cookie");
        _adapter.RequestHeaders.ShouldContainKey("User-Agent");
        _adapter.RequestHeaders["User-Agent"].ShouldBe("ClientPortalGW/1");
    }

    [Fact]
    public async Task ConnectAsync_StartsHeartbeatAndMessagePump()
    {
        var fakeTime = new FakeTimeProvider();
        await using var client = CreateClient(fakeTime);

        await client.ConnectAsync(TestContext.Current.CancellationToken);

        // Heartbeat loop started but won't tick until clock is advanced.
        _adapter.State.ShouldBe(System.Net.WebSockets.WebSocketState.Open);
    }

    [Fact]
    public async Task MessagePump_RoutesMessagesByTopicPrefix()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        var (reader, unsubscribe) = await client.SubscribeTopicAsync(
            "smd+265598+{\"fields\":[\"31\"]}", "smd",
            TestContext.Current.CancellationToken);

        _adapter.EnqueueServerMessage("""{"topic":"smd+265598","31":"150.25"}""");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var msg = await reader.ReadAsync(cts.Token);

        msg.GetProperty("topic").GetString().ShouldBe("smd+265598");
        unsubscribe();
    }

    [Fact]
    public async Task MessagePump_IgnoresUnknownTopics()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        var (reader, unsubscribe) = await client.SubscribeTopicAsync(
            "smd+123+{}", "smd",
            TestContext.Current.CancellationToken);

        // Send a message with an unknown topic -- should not crash
        _adapter.EnqueueServerMessage("""{"topic":"xyz+unknown","data":"test"}""");
        // Then send a matching message
        _adapter.EnqueueServerMessage("""{"topic":"smd+123","31":"100"}""");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var msg = await reader.ReadAsync(cts.Token);

        msg.GetProperty("topic").GetString().ShouldBe("smd+123");
        unsubscribe();
    }

    [Fact]
    public async Task SubscribeTopicAsync_SendsSubscribeMessage()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        var subscribeMsg = "smd+265598+{\"fields\":[\"31\"]}";
        var (_, unsubscribe) = await client.SubscribeTopicAsync(
            subscribeMsg, "smd",
            TestContext.Current.CancellationToken);

        _adapter.SentMessages.ShouldContain(subscribeMsg);
        unsubscribe();
    }

    [Fact]
    public async Task SubscribeTopicAsync_WhenConnected_SendsSubscribeMessage()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        var (_, unsubscribe) = await client.SubscribeTopicAsync(
            "smd+123+{}", "smd",
            TestContext.Current.CancellationToken);

        _adapter.SentMessages.ShouldContain("smd+123+{}");
        unsubscribe();
    }

    [Fact]
    public async Task ReconnectAsync_ReplaysActiveSubscriptions()
    {
        var fakeTime = new FakeTimeProvider();
        await using var client = CreateClient(fakeTime);
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        var sub1 = "smd+100+{}";
        var sub2 = "sor+{}";
        var (_, unsub1) = await client.SubscribeTopicAsync(sub1, "smd",
            TestContext.Current.CancellationToken);
        var (_, unsub2) = await client.SubscribeTopicAsync(sub2, "sor",
            TestContext.Current.CancellationToken);

        while (_adapter.SentMessages.TryDequeue(out _))
        {
        }

        // Run trigger in background so we can pump the clock concurrently.
        var ct = TestContext.Current.CancellationToken;
        var reconnectTask = Task.Run(
            () => _notifier.TriggerRefreshAsync(ct), ct);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!reconnectTask.IsCompleted && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        await reconnectTask;

        var sent = _adapter.SentMessages.ToArray();
        sent.ShouldContain(sub1);
        sent.ShouldContain(sub2);

        unsub1();
        unsub2();
    }

    [Fact]
    public async Task DisconnectAsync_StopsHeartbeatAndPump()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        // Dispose triggers disconnect
        await client.DisposeAsync();

        _adapter.State.ShouldNotBe(System.Net.WebSockets.WebSocketState.Open);
    }

    [Fact]
    public async Task DisposeAsync_UnsubscribesFromNotifier()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        await client.DisposeAsync();

        _notifier.SubscriptionDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task OnSessionRefreshed_TriggersReconnect()
    {
        var fakeTime = new FakeTimeProvider();
        await using var client = CreateClient(fakeTime);
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        var ct = TestContext.Current.CancellationToken;
        var reconnectTask = Task.Run(
            () => _notifier.TriggerRefreshAsync(ct), ct);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!reconnectTask.IsCompleted && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        await reconnectTask;

        _adapter.ConnectedUri.ShouldNotBeNull();
    }

    [Fact]
    public async Task HeartbeatSendFailure_TriggersReconnect()
    {
        var fakeTime = new FakeTimeProvider();
        await using var client = CreateClient(fakeTime);
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        _adapter.FailSendAfterCount = 0;

        // Pump: advance 1s at a time. After 10s heartbeat fires → send fails →
        // reconnect spawned. After 1 more second reconnect delay fires → connect again.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (_adapter.ConnectCallCount < 2 && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        _adapter.ConnectCallCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_DoesNotThrow()
    {
        var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        await client.DisposeAsync();
        await client.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_CompletesSubscriberChannels()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        var (reader, _) = await client.SubscribeTopicAsync(
            "smd+123+{}", "smd",
            TestContext.Current.CancellationToken);

        await client.DisposeAsync();

        reader.Completion.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task SubscribeAfterDispose_ThrowsObjectDisposedException()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = CreateClient();
        await client.ConnectAsync(ct);

        await client.DisposeAsync();

        await Should.ThrowAsync<ObjectDisposedException>(
            () => client.SubscribeTopicAsync("smd+123+{}", "smd", ct));
    }

    [Fact]
    public async Task MalformedJson_DroppedWithoutCrash()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        var (reader, unsubscribe) = await client.SubscribeTopicAsync(
            "smd+265598+{\"fields\":[\"31\"]}", "smd",
            TestContext.Current.CancellationToken);

        _adapter.EnqueueServerMessage("not json");
        _adapter.EnqueueServerMessage("""{"topic":"smd+265598","31":"150.25"}""");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var msg = await reader.ReadAsync(cts.Token);

        msg.GetProperty("topic").GetString().ShouldBe("smd+265598");
        unsubscribe();
    }

    [Fact]
    public async Task InternalTopics_NotDeliveredToSubscribers()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        var (reader, unsubscribe) = await client.SubscribeTopicAsync(
            "smd+123+{}", "smd",
            TestContext.Current.CancellationToken);

        _adapter.EnqueueServerMessage("""{"topic":"tic"}""");
        _adapter.EnqueueServerMessage("""{"topic":"smd+123","31":"100"}""");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var msg = await reader.ReadAsync(cts.Token);

        msg.GetProperty("topic").GetString().ShouldBe("smd+123");
        unsubscribe();
    }

    [Fact]
    public async Task ServerCloseFrame_TriggersReconnect()
    {
        var fakeTime = new FakeTimeProvider();
        await using var client = CreateClient(fakeTime);
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        // Wait for the message pump task to start and reach ReceiveAsync before signalling close.
        // Without this, the pump may see State != Open at the top of its loop and exit without
        // scheduling ReconnectAsync.
        await _adapter.WaitForReceiveAsync(TestContext.Current.CancellationToken);
        _adapter.SignalClose();

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (_adapter.ConnectCallCount < 2 && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        _adapter.ConnectCallCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ReconnectFailure_DoesNotCrash()
    {
        var fakeTime = new FakeTimeProvider();
        await using var client = CreateClient(fakeTime);
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        _adapter.FailOnConnect = true;

        // Wait for the message pump task to start and reach ReceiveAsync before signalling close.
        await _adapter.WaitForReceiveAsync(TestContext.Current.CancellationToken);
        _adapter.SignalClose();

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (_adapter.ConnectCallCount < 2 && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        _adapter.State.ShouldNotBe(System.Net.WebSockets.WebSocketState.Open);
    }

    [Fact]
    public async Task SessionRefreshAfterDispose_DoesNotReconnect()
    {
        var ct = TestContext.Current.CancellationToken;
        var fakeTime = new FakeTimeProvider();
        await using var client = CreateClient(fakeTime);
        await client.ConnectAsync(ct);

        var count = _adapter.ConnectCallCount;

        await client.DisposeAsync();

        // OnSessionRefreshedAsync returns immediately when disposed — no reconnect.
        await _notifier.TriggerRefreshAsync(ct);
        await Task.Yield();

        _adapter.ConnectCallCount.ShouldBe(count);
    }

    [Fact]
    public async Task Heartbeat_FiresAtConfiguredInterval()
    {
        // Pin the contract: the heartbeat interval is constructor-injected,
        // not a hardcoded const. With a 5-second interval, advancing the fake
        // clock by 5s should produce a "tic" send.
        var fakeTime = new FakeTimeProvider();
        await using var client = CreateClient(fakeTime, heartbeatIntervalSeconds: 5);
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        _adapter.SentMessages.ShouldNotContain("tic", "no heartbeat should fire before the interval elapses");

        // Advance the fake clock past the configured interval, yielding so
        // the heartbeat task picks up the new time.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!_adapter.SentMessages.Any(m => m == "tic") && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        _adapter.SentMessages.ShouldContain("tic");
    }

    [Fact]
    public async Task ReceiveMessage_StampsLastMessageReceivedAt_FromTimeProvider()
    {
        var fakeTime = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        await using var client = CreateClient(fakeTime);
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        client.LastMessageReceivedAt.ShouldBeNull();

        _adapter.EnqueueServerMessage("{\"topic\":\"smd+100\",\"data\":{}}");

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (client.LastMessageReceivedAt is null && DateTime.UtcNow < deadline)
        {
            await Task.Yield();
        }

        client.LastMessageReceivedAt.ShouldBe(fakeTime.GetUtcNow());
    }

    [Fact]
    public async Task SubscribeTopicAsync_SubscriberFallsBehind_DropsOldestNotNewest()
    {
        // Use a small buffer so we can fill it without writing 256 messages.
        await using var client = new IbkrWebSocketClient(
            _sessionApi,
            _credentials,
            _notifier,
            NullLogger<IbkrWebSocketClient>.Instance,
            () => _adapter,
            heartbeatIntervalSeconds: 30,
            streamingBufferSize: 4,
            timeProvider: null);

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var ct = TestContext.Current.CancellationToken;

        var (reader, _) = await client.SubscribeTopicAsync(
            "smd+265598+{}", "smd", ct);

        // Drain the startup signal: the pump started and called ReceiveAsync once already.
        await _adapter.WaitForReceiveAsync(ct);

        // Inject 6 messages — buffer is 4, so the first 2 should be dropped.
        for (var i = 1; i <= 6; i++)
        {
            _adapter.EnqueueServerMessage($"{{\"topic\":\"smd+265598\",\"seq\":{i}}}");
        }

        // Wait for all 6 messages to be processed: each message causes the pump to
        // loop back to ReceiveAsync, releasing one signal per message.
        for (var i = 0; i < 6; i++)
        {
            await _adapter.WaitForReceiveAsync(ct);
        }

        // Drain everything currently buffered and verify the OLDEST 2 were dropped.
        var received = new List<int>();
        while (reader.TryRead(out var element))
        {
            received.Add(element.GetProperty("seq").GetInt32());
        }

        received.Count.ShouldBeLessThanOrEqualTo(4);
        received.ShouldContain(6); // newest survived
        received.ShouldNotContain(1); // oldest dropped
        received.ShouldNotContain(2); // second-oldest dropped
    }

    [Fact]
    public async Task ConnectAsync_ReplaysActiveSubscriptions()
    {
        // Pre-seed _activeSubscriptions by calling SubscribeTopicAsync after
        // a manual prior connect, then disconnect, then reconnect via ConnectAsync.
        // This validates that ConnectCoreAsync's replay path runs on initial connect
        // (it already runs on reconnect; we want to prove the same code serves both).
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        await client.SubscribeTopicAsync(
            "smd+265598+{}", "smd", TestContext.Current.CancellationToken);

        // Reset the adapter's send tracking by counting messages sent so far.
        var sentBeforeReconnect = _adapter.SentMessages.Count;

        // Trigger a reconnect via session-refresh, which calls ConnectCoreAsync again.
        await _notifier.TriggerRefreshAsync(TestContext.Current.CancellationToken);
        await Task.Yield();

        // Wait briefly for the reconnect's replay to complete.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (_adapter.SentMessages.Count <= sentBeforeReconnect && DateTime.UtcNow < deadline)
        {
            await Task.Yield();
        }

        _adapter.SentMessages.ShouldContain("smd+265598+{}");
    }

    [Fact]
    public async Task SubscribeTopicAsync_BeforeConnect_DoesNotSendMessage()
    {
        await using var client = CreateClient();

        // Do NOT call ConnectAsync first.
        await client.SubscribeTopicAsync(
            "smd+265598+{}", "smd", TestContext.Current.CancellationToken);

        _adapter.ConnectCallCount.ShouldBe(0);
        _adapter.SentMessages.ShouldNotContain("smd+265598+{}");
    }

    [Fact]
    public async Task SubscribeBeforeConnect_ThenConnectAsync_SendsQueuedMessage()
    {
        await using var client = CreateClient();

        await client.SubscribeTopicAsync(
            "smd+265598+{}", "smd", TestContext.Current.CancellationToken);
        _adapter.SentMessages.ShouldNotContain("smd+265598+{}");

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!_adapter.SentMessages.Contains("smd+265598+{}") && DateTime.UtcNow < deadline)
        {
            await Task.Yield();
        }

        _adapter.SentMessages.ShouldContain("smd+265598+{}");
    }

    [Fact]
    public async Task ReceiveMessage_AfterClockAdvance_StampsNewTime()
    {
        var start = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var fakeTime = new FakeTimeProvider(start);
        await using var client = CreateClient(fakeTime);
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        _adapter.EnqueueServerMessage("{\"topic\":\"smd+100\",\"data\":{}}");
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (client.LastMessageReceivedAt is null && DateTime.UtcNow < deadline)
        {
            await Task.Yield();
        }

        var first = client.LastMessageReceivedAt;
        first.ShouldBe(start);

        fakeTime.Advance(TimeSpan.FromMinutes(7));

        _adapter.EnqueueServerMessage("{\"topic\":\"smd+100\",\"data\":{}}");
        deadline = DateTime.UtcNow.AddSeconds(5);
        while (client.LastMessageReceivedAt == first && DateTime.UtcNow < deadline)
        {
            await Task.Yield();
        }

        client.LastMessageReceivedAt.ShouldBe(start.AddMinutes(7));
    }

    [Fact]
    public async Task RegisterUnsolicitedTopic_ReturnsReader_AndDoesNotSendMessage()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var sentBefore = _adapter.SentMessages.Count;

        var (reader, _) = client.RegisterUnsolicitedTopic("sts");

        reader.ShouldNotBeNull();
        _adapter.SentMessages.Count.ShouldBe(sentBefore);
    }

    [Fact]
    public async Task ProcessMessage_StsTopic_RoutesToRegisteredSubscriber()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var (reader, _) = client.RegisterUnsolicitedTopic("sts");

        _adapter.EnqueueServerMessage("""{"topic":"sts","args":{"authenticated":true}}""");

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (reader.Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Yield();
        }

        reader.TryRead(out var element).ShouldBeTrue();
        element.GetProperty("topic").GetString().ShouldBe("sts");
    }

    [Fact]
    public async Task ProcessMessage_SystemTopic_RoutesToRegisteredSubscriber()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var (reader, _) = client.RegisterUnsolicitedTopic("system");

        _adapter.EnqueueServerMessage("""{"topic":"system","success":"alice"}""");

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (reader.Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Yield();
        }

        reader.TryRead(out var element).ShouldBeTrue();
        element.GetProperty("topic").GetString().ShouldBe("system");
    }

    [Fact]
    public async Task ProcessMessage_ActTopic_RoutesToRegisteredSubscriber()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var (reader, _) = client.RegisterUnsolicitedTopic("act");

        _adapter.EnqueueServerMessage("""{"topic":"act","args":{"selectedAccount":"DU123"}}""");

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (reader.Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Yield();
        }

        reader.TryRead(out var element).ShouldBeTrue();
        element.GetProperty("topic").GetString().ShouldBe("act");
    }

    [Fact]
    public async Task ProcessMessage_TicTopic_StillIgnoredEvenWithSubscriber()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var (reader, _) = client.RegisterUnsolicitedTopic("tic");

        _adapter.EnqueueServerMessage("""{"topic":"tic"}""");
        await Task.Delay(200, TestContext.Current.CancellationToken); // give the pump a chance

        reader.Count.ShouldBe(0);
    }

    [Fact]
    public async Task SendTextAsync_LogsOutgoingMessageAtTrace()
    {
        var logger = new CapturingLogger();
        await using var client = CreateClient(logger: logger);
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        var (_, unsubscribe) = await client.SubscribeTopicAsync(
            "smd+265598+{\"fields\":[\"31\"]}", "smd",
            TestContext.Current.CancellationToken);

        logger.Messages.ShouldContain(m =>
            m.Level == LogLevel.Trace
            && m.Formatted.Contains("WebSocket send", StringComparison.Ordinal)
            && m.Formatted.Contains("smd+265598+{\"fields\":[\"31\"]}", StringComparison.Ordinal));
        unsubscribe();
    }

    [Fact]
    public async Task ReceivePump_LogsIncomingMessageAtTrace()
    {
        var logger = new CapturingLogger();
        await using var client = CreateClient(logger: logger);
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        _adapter.EnqueueServerMessage("""{"topic":"smd+265598","31":"150.25"}""");
        await _adapter.WaitForReceiveAsync(TestContext.Current.CancellationToken);

        logger.Messages.ShouldContain(m =>
            m.Level == LogLevel.Trace
            && m.Formatted.Contains("WebSocket receive", StringComparison.Ordinal)
            && m.Formatted.Contains("\"topic\":\"smd+265598\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendTextAsync_WhenTraceDisabled_DoesNotFormatPayload()
    {
        var logger = new CapturingLogger(minimumLevel: LogLevel.Debug);
        await using var client = CreateClient(logger: logger);
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        var (_, unsubscribe) = await client.SubscribeTopicAsync(
            "smd+265598+{\"fields\":[\"31\"]}", "smd",
            TestContext.Current.CancellationToken);

        logger.Messages.ShouldNotContain(m =>
            m.Formatted.Contains("WebSocket send", StringComparison.Ordinal));
        unsubscribe();
    }

    private IbkrWebSocketClient CreateClient(
        TimeProvider? timeProvider = null,
        int heartbeatIntervalSeconds = 30,
        int streamingBufferSize = 256,
        ILogger<IbkrWebSocketClient>? logger = null) =>
        new(
            _sessionApi,
            _credentials,
            _notifier,
            logger ?? NullLogger<IbkrWebSocketClient>.Instance,
            () => _adapter,
            heartbeatIntervalSeconds,
            streamingBufferSize,
            timeProvider);

    private sealed class CapturingLogger(LogLevel minimumLevel = LogLevel.Trace) : ILogger<IbkrWebSocketClient>
    {
        public List<(LogLevel Level, string Formatted)> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= minimumLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }
            Messages.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();
            public void Dispose() { }
        }
    }

    internal class FakeSessionApi : IIbkrSessionApi
    {
        public Task<SsodhInitResponse> InitializeBrokerageSessionAsync(
            SsodhInitRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SsodhInitResponse(true, true, false, true, null, null, null, null));

        public Task<TickleResponse> TickleAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new TickleResponse(
                Session: "fake-session-id",
                Hmds: null,
                Iserver: new TickleIserverStatus(
                    AuthStatus: new TickleAuthStatus(true, false, true, true, null, null, null, null))));

        public Task<SuppressResponse> SuppressQuestionsAsync(
            SuppressRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SuppressResponse("submitted"));

        public Task<LogoutResponse> LogoutAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new LogoutResponse(true));

        public Task<SuppressResetResponse> ResetSuppressedQuestionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SuppressResetResponse(Status: "submitted"));

        public Task<AuthStatusResponse> GetAuthStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AuthStatusResponse(true, false, true, true, null, null, null, null, null, null));

    }

    internal class FakeLifecycleNotifier : ISessionLifecycleNotifier
    {
        private Func<CancellationToken, Task>? _callback;

        public bool SubscriptionDisposed { get; private set; }

        public IDisposable Subscribe(Func<CancellationToken, Task> onSessionRefreshed)
        {
            _callback = onSessionRefreshed;
            return new CallbackDisposable(() => SubscriptionDisposed = true);
        }

        public Task NotifyAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public async Task TriggerRefreshAsync(CancellationToken cancellationToken)
        {
            if (_callback != null)
            {
                await _callback(cancellationToken);
            }
        }

        private sealed class CallbackDisposable(Action onDispose) : IDisposable
        {
            public void Dispose() => onDispose();
        }
    }
}
