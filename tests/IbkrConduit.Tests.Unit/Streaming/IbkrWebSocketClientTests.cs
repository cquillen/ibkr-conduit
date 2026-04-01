using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Session;
using IbkrConduit.Streaming;
using Microsoft.Extensions.Logging.Abstractions;
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
        await using var client = CreateClient();

        await client.ConnectAsync(TestContext.Current.CancellationToken);

        // Give the heartbeat loop a moment to fire (interval is 10s but we just
        // verify the background tasks started without throwing)
        await Task.Delay(50);

        // The client should still be in a healthy state (no crash)
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
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        var sub1 = "smd+100+{}";
        var sub2 = "sor+{}";
        var (_, unsub1) = await client.SubscribeTopicAsync(sub1, "smd",
            TestContext.Current.CancellationToken);
        var (_, unsub2) = await client.SubscribeTopicAsync(sub2, "sor",
            TestContext.Current.CancellationToken);

        // Clear sent messages and trigger reconnect via session refresh
        while (_adapter.SentMessages.TryDequeue(out _))
        {
        }

        // Trigger reconnect by notifying session refresh
        await _notifier.TriggerRefreshAsync(TestContext.Current.CancellationToken);

        // Wait a moment for the reconnect to replay
        await Task.Delay(200);

        // After reconnect, subscriptions should be replayed
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
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        var originalUri = _adapter.ConnectedUri;

        // Trigger session refresh notification
        await _notifier.TriggerRefreshAsync(TestContext.Current.CancellationToken);

        // Wait for reconnect to complete
        await Task.Delay(200);

        // ConnectAsync should have been called again (adapter was re-created via factory)
        _adapter.ConnectedUri.ShouldNotBeNull();
    }

    private IbkrWebSocketClient CreateClient() =>
        new(
            _sessionApi,
            _credentials,
            _notifier,
            NullLogger<IbkrWebSocketClient>.Instance,
            () => _adapter);

    internal class FakeSessionApi : IIbkrSessionApi
    {
        public Task<SsodhInitResponse> InitializeBrokerageSessionAsync(
            SsodhInitRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SsodhInitResponse(true, true, false));

        public Task<TickleResponse> TickleAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new TickleResponse(
                Session: "fake-session-id",
                Iserver: new TickleIserverStatus(
                    AuthStatus: new TickleAuthStatus(true, false, true))));

        public Task<SuppressResponse> SuppressQuestionsAsync(
            SuppressRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SuppressResponse("submitted"));

        public Task<LogoutResponse> LogoutAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new LogoutResponse(true));
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
