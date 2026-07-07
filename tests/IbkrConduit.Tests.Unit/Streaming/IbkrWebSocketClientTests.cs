using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Diagnostics;
using IbkrConduit.Session;
using IbkrConduit.Streaming;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Refit;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class IbkrWebSocketClientTests
{
    private readonly FakeWebSocketAdapter _adapter = new();
    private readonly FakeSessionApi _sessionApi = new();
    private readonly FakeSessionManager _sessionManager = new();
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
    public async Task ConnectAsync_EstablishesBrokerageSessionBeforeConnecting()
    {
        await using var client = CreateClient();

        // Capture whether the WebSocket had already connected at the moment the brokerage
        // session was ensured. The session MUST be established first (via ssodh/init);
        // otherwise IBKR rejects iserver-dependent subscriptions (sor/str) with
        // "Missing iserver bridge". Seed true so a never-called ensure fails the assertion.
        var adapterConnectedWhenSessionEnsured = true;
        _sessionManager.OnEnsureInitialized =
            () => adapterConnectedWhenSessionEnsured = _adapter.ConnectedUri is not null;

        await client.ConnectAsync(TestContext.Current.CancellationToken);

        _sessionManager.EnsureInitializedCallCount.ShouldBe(1);
        adapterConnectedWhenSessionEnsured.ShouldBeFalse();
    }

    [Fact]
    public async Task ConnectAsync_AfterDispose_ThrowsWithoutEstablishingSession()
    {
        var client = CreateClient();
        await client.DisposeAsync();

        await Should.ThrowAsync<ObjectDisposedException>(
            async () => await client.ConnectAsync(TestContext.Current.CancellationToken));

        // The dispose guard must short-circuit before any session work — a disposed
        // client should not perform network I/O via EnsureInitializedAsync.
        _sessionManager.EnsureInitializedCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ConnectAsync_WithCustomWebSocketBaseUrl_UsesConfiguredBaseForUri()
    {
        const string customBase = "wss://custom.test/v1/api/ws";
        await using var client = CreateClient(webSocketBaseUrl: customBase);

        await client.ConnectAsync(TestContext.Current.CancellationToken);

        _adapter.ConnectedUri.ShouldNotBeNull();
        _adapter.ConnectedUri!.ToString().ShouldStartWith(customBase);
    }

    [Fact]
    public async Task ConnectAsync_TickleWrappedTransportFault_ThrowsOriginalHttpRequestException()
    {
        // Refit 11 wraps the raw Task<T> tickle call's transport fault in ApiRequestException.
        // ConnectAsync must surface the original HttpRequestException, not the wrapper.
        await using var client = CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.ibkr.com/v1/api/tickle");
        var inner = new HttpRequestException("connection refused");
        _sessionApi.NextTickleException = new ApiRequestException(
            request, HttpMethod.Get, new RefitSettings(), inner);

        var ex = await Should.ThrowAsync<HttpRequestException>(
            () => client.ConnectAsync(TestContext.Current.CancellationToken));
        ex.ShouldBeSameAs(inner);
    }

    [Fact]
    public async Task ConnectAsync_TickleWrappedCancellation_PropagatesOperationCanceled()
    {
        // A wrapped OperationCanceledException with a cancelled caller token must still
        // surface as OperationCanceledException (not ApiRequestException).
        await using var client = CreateClient();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.ibkr.com/v1/api/tickle");
        var inner = new OperationCanceledException(cts.Token);
        _sessionApi.NextTickleException = new ApiRequestException(
            request, HttpMethod.Get, new RefitSettings(), inner);

        await Should.ThrowAsync<OperationCanceledException>(
            () => client.ConnectAsync(cts.Token));
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
            "smd+265598+{\"fields\":[\"31\"]}", "smd", "umd+265598+{}",
            TestContext.Current.CancellationToken);

        _adapter.EnqueueServerMessage("""{"topic":"smd+265598","31":"150.25"}""");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var msg = await reader.ReadAsync(cts.Token);

        msg.GetProperty("topic").GetString().ShouldBe("smd+265598");
        await unsubscribe(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MessagePump_IgnoresUnknownTopics()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        var (reader, unsubscribe) = await client.SubscribeTopicAsync(
            "smd+123+{}", "smd", null,
            TestContext.Current.CancellationToken);

        // Send a message with an unknown topic -- should not crash
        _adapter.EnqueueServerMessage("""{"topic":"xyz+unknown","data":"test"}""");
        // Then send a matching message
        _adapter.EnqueueServerMessage("""{"topic":"smd+123","31":"100"}""");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var msg = await reader.ReadAsync(cts.Token);

        msg.GetProperty("topic").GetString().ShouldBe("smd+123");
        await unsubscribe(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SubscribeTopicAsync_SendsSubscribeMessage()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        var subscribeMsg = "smd+265598+{\"fields\":[\"31\"]}";
        var (_, unsubscribe) = await client.SubscribeTopicAsync(
            subscribeMsg, "smd", "umd+265598+{}",
            TestContext.Current.CancellationToken);

        _adapter.SentMessages.ShouldContain(subscribeMsg);
        await unsubscribe(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SubscribeTopicAsync_WhenConnected_SendsSubscribeMessage()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        var (_, unsubscribe) = await client.SubscribeTopicAsync(
            "smd+123+{}", "smd", null,
            TestContext.Current.CancellationToken);

        _adapter.SentMessages.ShouldContain("smd+123+{}");
        await unsubscribe(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ReconnectAsync_ReplaysActiveSubscriptions()
    {
        var fakeTime = new FakeTimeProvider();
        await using var client = CreateClient(fakeTime);
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        var sub1 = "smd+100+{}";
        var sub2 = "sor+{}";
        var (_, unsub1) = await client.SubscribeTopicAsync(sub1, "smd", null,
            TestContext.Current.CancellationToken);
        var (_, unsub2) = await client.SubscribeTopicAsync(sub2, "sor", "uor+{}",
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

        await unsub1(TestContext.Current.CancellationToken);
        await unsub2(TestContext.Current.CancellationToken);
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
            "smd+123+{}", "smd", null,
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
            () => client.SubscribeTopicAsync("smd+123+{}", "smd", null, ct));
    }

    [Fact]
    public async Task MalformedJson_DroppedWithoutCrash()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        var (reader, unsubscribe) = await client.SubscribeTopicAsync(
            "smd+265598+{\"fields\":[\"31\"]}", "smd", null,
            TestContext.Current.CancellationToken);

        _adapter.EnqueueServerMessage("not json");
        _adapter.EnqueueServerMessage("""{"topic":"smd+265598","31":"150.25"}""");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var msg = await reader.ReadAsync(cts.Token);

        msg.GetProperty("topic").GetString().ShouldBe("smd+265598");
        await unsubscribe(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task InternalTopics_NotDeliveredToSubscribers()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        var (reader, unsubscribe) = await client.SubscribeTopicAsync(
            "smd+123+{}", "smd", null,
            TestContext.Current.CancellationToken);

        _adapter.EnqueueServerMessage("""{"topic":"tic"}""");
        _adapter.EnqueueServerMessage("""{"topic":"smd+123","31":"100"}""");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var msg = await reader.ReadAsync(cts.Token);

        msg.GetProperty("topic").GetString().ShouldBe("smd+123");
        await unsubscribe(TestContext.Current.CancellationToken);
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
        var tenant = new TenantContext("test");
        await using var client = new IbkrWebSocketClient(
            _sessionApi,
            _sessionManager,
            _credentials,
            _notifier,
            NullLogger<IbkrWebSocketClient>.Instance,
            () => _adapter,
            heartbeatIntervalSeconds: 30,
            streamingBufferSize: 4,
            tenant: tenant,
            metrics: new StreamingMetrics(tenant),
            timeProvider: null);

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var ct = TestContext.Current.CancellationToken;

        var (reader, _) = await client.SubscribeTopicAsync(
            "smd+265598+{}", "smd", null, ct);

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
            "smd+265598+{}", "smd", null, TestContext.Current.CancellationToken);

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
            "smd+265598+{}", "smd", null, TestContext.Current.CancellationToken);

        _adapter.ConnectCallCount.ShouldBe(0);
        _adapter.SentMessages.ShouldNotContain("smd+265598+{}");
    }

    [Fact]
    public async Task SubscribeBeforeConnect_ThenConnectAsync_SendsQueuedMessage()
    {
        await using var client = CreateClient();

        await client.SubscribeTopicAsync(
            "smd+265598+{}", "smd", null, TestContext.Current.CancellationToken);
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
            "smd+265598+{\"fields\":[\"31\"]}", "smd", null,
            TestContext.Current.CancellationToken);

        logger.Messages.ShouldContain(m =>
            m.Level == LogLevel.Trace
            && m.Formatted.Contains("WebSocket send", StringComparison.Ordinal)
            && m.Formatted.Contains("smd+265598+{\"fields\":[\"31\"]}", StringComparison.Ordinal));
        await unsubscribe(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ReceivePump_LogsIncomingMessageAtTrace()
    {
        var logger = new CapturingLogger();
        await using var client = CreateClient(logger: logger);
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        _adapter.EnqueueServerMessage("""{"topic":"smd+265598","31":"150.25"}""");
        await _adapter.WaitForReceiveAsync(TestContext.Current.CancellationToken);

        // WaitForReceiveAsync signals when the pump's ReceiveAsync call returns,
        // but the pump still has work to do after that (decode bytes, fire
        // LogIncomingMessage, dispatch to subscribers). Poll briefly for the
        // expected log entry rather than asserting immediately — on slow CI
        // runners the post-receive continuation may not have run yet when
        // the test thread resumes.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && !logger.Messages.Any(m =>
            m.Level == LogLevel.Trace
            && m.Formatted.Contains("WebSocket receive", StringComparison.Ordinal)
            && m.Formatted.Contains("\"topic\":\"smd+265598\"", StringComparison.Ordinal)))
        {
            await Task.Yield();
        }

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
            "smd+265598+{\"fields\":[\"31\"]}", "smd", null,
            TestContext.Current.CancellationToken);

        logger.Messages.ShouldNotContain(m =>
            m.Formatted.Contains("WebSocket send", StringComparison.Ordinal));
        await unsubscribe(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OnTickleSucceeded_WhenConnected_DoesNotReconnect()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var connectsBefore = _adapter.ConnectCallCount;

        await _notifier.TriggerTickleSucceededAsync(TestContext.Current.CancellationToken);

        _adapter.ConnectCallCount.ShouldBe(connectsBefore);
    }

    [Fact]
    public async Task OnTickleSucceeded_WhenDisconnected_TriggersReconnect()
    {
        var fakeTime = new FakeTimeProvider();
        await using var client = CreateClient(fakeTime);
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        // Wait for the message pump to reach ReceiveAsync, then make every
        // future ConnectAsync fail so IsConnected stays false even after the
        // pump's own reconnect attempt completes. This isolates the watchdog
        // as the trigger for any post-pump-reconnect connect attempts.
        await _adapter.WaitForReceiveAsync(TestContext.Current.CancellationToken);
        _adapter.FailOnConnect = true;
        _adapter.SignalClose();

        // Wait for the message pump's own reconnect attempt to complete (and
        // fail) — it will increment ConnectCallCount once.
        var ct = TestContext.Current.CancellationToken;
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (_adapter.ConnectCallCount < 2 && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }
        _adapter.ConnectCallCount.ShouldBeGreaterThanOrEqualTo(2);

        client.IsConnected.ShouldBeFalse();
        var connectsBefore = _adapter.ConnectCallCount;

        // Now fire the watchdog. With the watchdog wired, this should trigger
        // ANOTHER reconnect attempt, incrementing ConnectCallCount further.
        // Without the watchdog wiring, CallCount stays put.
        var watchdogTask = Task.Run(
            () => _notifier.TriggerTickleSucceededAsync(ct), ct);

        deadline = DateTime.UtcNow.AddSeconds(5);
        while (_adapter.ConnectCallCount <= connectsBefore && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        await watchdogTask;
        _adapter.ConnectCallCount.ShouldBeGreaterThan(connectsBefore);
    }

    [Fact]
    public async Task OnTickleSucceeded_AfterDispose_DoesNothing()
    {
        var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        await client.DisposeAsync();
        var connectsBefore = _adapter.ConnectCallCount;

        await _notifier.TriggerTickleSucceededAsync(TestContext.Current.CancellationToken);

        _adapter.ConnectCallCount.ShouldBe(connectsBefore);
    }

    [Fact]
    public async Task OnTickleSucceeded_ReconnectThrows_ExceptionSwallowed()
    {
        var fakeTime = new FakeTimeProvider();
        await using var client = CreateClient(fakeTime);
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        await _adapter.WaitForReceiveAsync(TestContext.Current.CancellationToken);
        _adapter.SignalClose();

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (client.IsConnected && DateTime.UtcNow < deadline)
        {
            await Task.Yield();
        }

        // Force the next reconnect's session API call (Tickle in ConnectCoreAsync) to throw
        _sessionApi.NextTickleShouldThrow = true;

        // Run trigger in background so we can pump the clock for the reconnect delay.
        var ct = TestContext.Current.CancellationToken;
        var watchdogTask = Task.Run(
            () => _notifier.TriggerTickleSucceededAsync(ct), ct);

        deadline = DateTime.UtcNow.AddSeconds(5);
        while (!watchdogTask.IsCompleted && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        // Should not throw out of the watchdog
        await watchdogTask;
    }

    [Fact]
    public async Task Reconnect_MessagePumpAndTickleFireConcurrently_BothSerializedViaConnectLock()
    {
        var fakeTime = new FakeTimeProvider();
        await using var client = CreateClient(fakeTime);
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        await _adapter.WaitForReceiveAsync(TestContext.Current.CancellationToken);
        var connectsBefore = _adapter.ConnectCallCount;

        // Fire two reconnect triggers near-simultaneously: the message-pump catch
        // will kick one off after SignalClose; the watchdog kicks the second.
        _adapter.SignalClose();

        var ct = TestContext.Current.CancellationToken;
        var watchdogTask = Task.Run(
            () => _notifier.TriggerTickleSucceededAsync(ct), ct);

        // Wait for any in-flight reconnect to settle, advancing the clock so
        // the reconnect delay completes.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while ((!watchdogTask.IsCompleted || _adapter.ConnectCallCount <= connectsBefore)
            && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        await watchdogTask;

        // _connectLock should serialize the attempts. We don't assert exact count
        // (could be 1 or 2 depending on timing) — the smoke test is "no deadlock,
        // no exception, eventually a reconnect attempt happens".
        _adapter.ConnectCallCount.ShouldBeGreaterThan(connectsBefore);
    }

    [Fact]
    public async Task TradeExecutions_EndToEnd_EmitsOnePerExecutionFromStrFrame()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var client = CreateClient();
        await client.ConnectAsync(ct);

        var ops = new StreamingOperations(client, NullLoggerFactory.Instance, new IbkrConduit.Health.SessionHealthState(), new StreamingMetrics(new TenantContext("test")));
        var subscription = await ops.TradeExecutionsAsync(cancellationToken: ct);

        var received = new List<TradeExecution>();
        var done = new TaskCompletionSource();
        using var sub = subscription.Stream.Subscribe(new EndToEndObserver(e =>
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
    public async Task Unsubscribe_DifferentCancelKeys_CancelIndependently()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        var (_, unsub7) = await client.SubscribeTopicAsync(
            "smd+7+{\"fields\":[\"31\"]}", "smd", "umd+7+{}", TestContext.Current.CancellationToken);
        var (_, unsub8) = await client.SubscribeTopicAsync(
            "smd+8+{\"fields\":[\"31\"]}", "smd", "umd+8+{}", TestContext.Current.CancellationToken);
        while (_adapter.SentMessages.TryDequeue(out _)) { }

        await unsub7(TestContext.Current.CancellationToken);

        _adapter.SentMessages.ShouldContain("umd+7+{}");
        _adapter.SentMessages.ShouldNotContain("umd+8+{}");
        client.ActiveSubscriptionCount.ShouldBe(1);
    }

    [Fact]
    public async Task Unsubscribe_SendFailure_DoesNotThrowAndRemovesSubscription()
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

    [Fact]
    public async Task SubscribeTopicAsync_BufferOverflow_IncrementsDropCounterAndLogsOnce()
    {
        // FIL-1: an overflow eviction must be observable — it increments
        // ibkr.conduit.streaming.frames.dropped (cause=overflow) and logs a Warning exactly once
        // per topic per connection (log-throttle), never silently.
        var ct = TestContext.Current.CancellationToken;
        const string tenantId = "ws-overflow-tenant";
        using var drops = new MeterDropCapture(tenantId);
        var logger = new CapturingLogger();
        var tenant = new TenantContext(tenantId);

        await using var client = new IbkrWebSocketClient(
            _sessionApi,
            _sessionManager,
            _credentials,
            _notifier,
            logger,
            () => _adapter,
            heartbeatIntervalSeconds: 30,
            streamingBufferSize: 4,
            tenant: tenant,
            metrics: new StreamingMetrics(tenant),
            timeProvider: null);

        await client.ConnectAsync(ct);

        var (reader, _) = await client.SubscribeTopicAsync("smd+265598+{}", "smd", null, ct);

        // Drain the startup signal: the pump started and called ReceiveAsync once already.
        await _adapter.WaitForReceiveAsync(ct);

        // Inject more frames than the buffer holds while nobody reads -> overflow evictions.
        for (var i = 1; i <= 8; i++)
        {
            _adapter.EnqueueServerMessage($"{{\"topic\":\"smd+265598\",\"seq\":{i}}}");
        }
        for (var i = 0; i < 8; i++)
        {
            await _adapter.WaitForReceiveAsync(ct);
        }

        drops.Drops.ShouldContain(("smd", "overflow"));
        logger.Messages
            .Count(m => m.Level == LogLevel.Warning
                && m.Formatted.Contains("smd", StringComparison.Ordinal)
                && m.Formatted.Contains("overflow", StringComparison.Ordinal))
            .ShouldBe(1);
        reader.ShouldNotBeNull();
    }

    [Fact]
    public async Task Reconnect_EmitsDisconnectedThenReconnectedWithReplayedTopics()
    {
        // FIL-4: every reconnect emits a consumer-visible Disconnected/Reconnected pair, with the
        // reconnect listing the replayed topics, so a consumer can bound the gap and reconcile.
        var ct = TestContext.Current.CancellationToken;
        var fakeTime = new FakeTimeProvider();
        await using var client = CreateClient(fakeTime);
        await client.ConnectAsync(ct);

        var (events, _) = client.RegisterConnectionEvents();

        // A solicited subscription so the reconnect has a topic to replay.
        await client.SubscribeTopicAsync("sor+{}", "sor", "uor+{}", ct);

        var reconnectTask = Task.Run(() => _notifier.TriggerRefreshAsync(ct), ct);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!reconnectTask.IsCompleted && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }
        await reconnectTask;

        var first = await ReadEventAsync(events, ct);
        var second = await ReadEventAsync(events, ct);

        first.ShouldBeOfType<ConnectionDisconnected>().Reason.ShouldBe("session_refresh");
        second.ShouldBeOfType<ConnectionReconnected>().ReplayedTopics.ShouldContain("sor");
    }

    private static async Task<ConnectionEvent> ReadEventAsync(ChannelReader<ConnectionEvent> reader, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        return await reader.ReadAsync(cts.Token);
    }

    private sealed class EndToEndObserver(Action<TradeExecution> onNext) : IObserver<TradeExecution>
    {
        public void OnNext(TradeExecution value) => onNext(value);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }

    private IbkrWebSocketClient CreateClient(
        TimeProvider? timeProvider = null,
        int heartbeatIntervalSeconds = 30,
        int streamingBufferSize = 256,
        ILogger<IbkrWebSocketClient>? logger = null,
        string? webSocketBaseUrl = null)
    {
        var tenant = new TenantContext("test");
        return new IbkrWebSocketClient(
            _sessionApi,
            _sessionManager,
            _credentials,
            _notifier,
            logger ?? NullLogger<IbkrWebSocketClient>.Instance,
            () => _adapter,
            heartbeatIntervalSeconds,
            streamingBufferSize,
            tenant,
            new StreamingMetrics(tenant),
            timeProvider,
            webSocketBaseUrl);
    }

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
        public bool NextTickleShouldThrow { get; set; }

        /// <summary>If set, <see cref="TickleAsync"/> throws this exception (cleared after one throw).</summary>
        public Exception? NextTickleException { get; set; }

        public Task<SsodhInitResponse> InitializeBrokerageSessionAsync(
            SsodhInitRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SsodhInitResponse(true, true, false, true, null, null, null, null));

        public Task<TickleResponse> TickleAsync(CancellationToken cancellationToken = default)
        {
            if (NextTickleException != null)
            {
                var ex = NextTickleException;
                NextTickleException = null;
                throw ex;
            }
            if (NextTickleShouldThrow)
            {
                NextTickleShouldThrow = false;
                throw new System.Net.Http.HttpRequestException("Simulated tickle failure");
            }
            return Task.FromResult(new TickleResponse(
                Session: "fake-session-id",
                Hmds: null,
                Iserver: new TickleIserverStatus(
                    AuthStatus: new TickleAuthStatus(true, false, true, true, null, null, null, null))));
        }

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
        private readonly List<Func<CancellationToken, Task>> _tickleSubscribers = [];
        private Func<CancellationToken, Task>? _callback;

        public bool SubscriptionDisposed { get; private set; }

        public IDisposable Subscribe(Func<CancellationToken, Task> onSessionRefreshed)
        {
            _callback = onSessionRefreshed;
            return new CallbackDisposable(() => SubscriptionDisposed = true);
        }

        public Task NotifyAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public IDisposable SubscribeTickleSucceeded(Func<CancellationToken, Task> onTickleSucceeded)
        {
            _tickleSubscribers.Add(onTickleSucceeded);
            return new CallbackDisposable(() => _tickleSubscribers.Remove(onTickleSucceeded));
        }

        public async Task NotifyTickleSucceededAsync(CancellationToken cancellationToken)
        {
            foreach (var subscriber in _tickleSubscribers.ToArray())
            {
                await subscriber(cancellationToken);
            }
        }

        public Task TriggerTickleSucceededAsync(CancellationToken cancellationToken) =>
            NotifyTickleSucceededAsync(cancellationToken);

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
