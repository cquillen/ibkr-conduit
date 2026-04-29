using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Session;
using IbkrConduit.Streaming;
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
    public async Task SubscribeTopicAsync_ConnectsIfNotConnected()
    {
        await using var client = CreateClient();

        // Do not call ConnectAsync -- subscribe should trigger it
        var (_, unsubscribe) = await client.SubscribeTopicAsync(
            "smd+123+{}", "smd",
            TestContext.Current.CancellationToken);

        _adapter.ConnectedUri.ShouldNotBeNull();
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

    private IbkrWebSocketClient CreateClient(TimeProvider? timeProvider = null) =>
        new(
            _sessionApi,
            _credentials,
            _notifier,
            NullLogger<IbkrWebSocketClient>.Instance,
            () => _adapter,
            timeProvider);

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
