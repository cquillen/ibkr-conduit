using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using IbkrConduit.Auth;
using IbkrConduit.Diagnostics;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Streaming;

/// <summary>
/// Internal WebSocket client that manages the connection to the IBKR WebSocket API,
/// heartbeat, message pump, and topic-based routing to subscribers.
/// </summary>
internal sealed partial class IbkrWebSocketClient : IIbkrWebSocketClient
{
    private const string _webSocketBaseUrl = "wss://api.ibkr.com/v1/api/ws";
    private const int _reconnectDelayMs = 1000;
    private const int _receiveBufferSize = 8192;

    private static readonly Counter<long> _messagesReceived =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.websocket.messages.received");

    private static readonly Counter<long> _messagesSent =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.websocket.messages.sent");

    private static readonly Counter<long> _reconnectCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.websocket.reconnect.count");

    private static readonly Counter<long> _heartbeatCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.websocket.heartbeat.count");

    private readonly IIbkrSessionApi _sessionApi;
    private readonly IbkrOAuthCredentials _credentials;
    private readonly ISessionLifecycleNotifier _notifier;
    private readonly ILogger<IbkrWebSocketClient> _logger;
    private readonly Func<IWebSocketAdapter> _webSocketFactory;
    private readonly int _heartbeatIntervalSeconds;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, List<ChannelWriter<JsonElement>>> _subscribers = new();
    private readonly List<string> _activeSubscriptions = [];
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly object _subscriptionLock = new();

    private IWebSocketAdapter? _webSocket;
    private CancellationTokenSource? _heartbeatCts;
    private CancellationTokenSource? _messagePumpCts;
    private IDisposable? _notifierSubscription;
    private readonly CancellationTokenSource _disposeCts = new();

    /// <summary>
    /// Stores the ticks value of the last received message timestamp, or 0 if none.
    /// Uses Interlocked for thread-safe access since DateTimeOffset? cannot be volatile.
    /// </summary>
    private long _lastMessageReceivedAtTicks;

    private volatile bool _disposed;

    /// <summary>
    /// Creates a new <see cref="IbkrWebSocketClient"/>.
    /// </summary>
    /// <param name="sessionApi">Session API for tickle calls.</param>
    /// <param name="credentials">OAuth credentials containing the access token.</param>
    /// <param name="notifier">Session lifecycle notifier for reconnect on re-auth.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="webSocketFactory">Factory for creating WebSocket adapter instances.</param>
    /// <param name="heartbeatIntervalSeconds">Seconds between "tic" ping messages used to keep the WebSocket session alive. IBKR requires at least one per minute.</param>
    /// <param name="timeProvider">Time provider for delays; defaults to <see cref="TimeProvider.System"/>.</param>
    public IbkrWebSocketClient(
        IIbkrSessionApi sessionApi,
        IbkrOAuthCredentials credentials,
        ISessionLifecycleNotifier notifier,
        ILogger<IbkrWebSocketClient> logger,
        Func<IWebSocketAdapter> webSocketFactory,
        int heartbeatIntervalSeconds,
        TimeProvider? timeProvider = null)
    {
        _sessionApi = sessionApi;
        _credentials = credentials;
        _notifier = notifier;
        _logger = logger;
        _webSocketFactory = webSocketFactory;
        _heartbeatIntervalSeconds = heartbeatIntervalSeconds;
        _timeProvider = timeProvider ?? TimeProvider.System;

        IbkrConduitDiagnostics.Meter.CreateObservableGauge(
            "ibkr.conduit.websocket.connection_state",
            () => _webSocket is { State: WebSocketState.Open } ? 1 : 0);

        _notifierSubscription = _notifier.Subscribe(OnSessionRefreshedAsync);
    }

    /// <inheritdoc />
    public bool IsConnected
    {
        get
        {
            var ws = _webSocket;
            return ws is { State: WebSocketState.Open };
        }
    }

    /// <inheritdoc />
    public int ActiveSubscriptionCount
    {
        get
        {
            lock (_subscriptionLock)
            {
                return _activeSubscriptions.Count;
            }
        }
    }

    /// <inheritdoc />
    public DateTimeOffset? LastMessageReceivedAt
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastMessageReceivedAtTicks);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    /// <summary>
    /// Connects to the IBKR WebSocket API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await _connectLock.WaitAsync(cancellationToken);
        try
        {
            await ConnectCoreAsync(cancellationToken);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <summary>
    /// Subscribes to a WebSocket topic and returns a <see cref="ChannelReader{T}"/> for receiving messages,
    /// plus an unsubscribe action.
    /// </summary>
    /// <param name="subscribeMessage">The subscribe message to send on the WebSocket.</param>
    /// <param name="topicPrefix">The topic prefix for routing (e.g., "smd", "sor").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple of the channel reader and an unsubscribe action.</returns>
    public async Task<(ChannelReader<JsonElement> Reader, Action Unsubscribe)> SubscribeTopicAsync(
        string subscribeMessage,
        string topicPrefix,
        CancellationToken cancellationToken)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.WebSocket.Subscribe");
        activity?.SetTag(LogFields.Topic, topicPrefix);

        var ws = _webSocket;
        if (ws == null || ws.State != WebSocketState.Open)
        {
            await ConnectAsync(cancellationToken);
        }

        var channel = Channel.CreateUnbounded<JsonElement>();

        var writers = _subscribers.GetOrAdd(topicPrefix, _ => []);
        lock (writers)
        {
            writers.Add(channel.Writer);
        }

        lock (_subscriptionLock)
        {
            _activeSubscriptions.Add(subscribeMessage);
        }

        await SendTextAsync(subscribeMessage, cancellationToken);

        return (channel.Reader, () => Unsubscribe(topicPrefix, channel.Writer, subscribeMessage));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _notifierSubscription?.Dispose();
        _notifierSubscription = null;

        await _disposeCts.CancelAsync();
        await DisconnectAsync();

        // Complete all channel writers
        foreach (var kvp in _subscribers)
        {
            lock (kvp.Value)
            {
                foreach (var writer in kvp.Value)
                {
                    writer.TryComplete();
                }
            }
        }

        _subscribers.Clear();
        _connectLock.Dispose();
        _disposeCts.Dispose();
    }

    private async Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            return;
        }

        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.WebSocket.Connect");
        activity?.SetTag("url", _webSocketBaseUrl);

        LogConnecting();

        var tickleResponse = await _sessionApi.TickleAsync(cancellationToken);

        var uri = new Uri($"{_webSocketBaseUrl}?oauth_token={_credentials.AccessToken}");
        var ws = _webSocketFactory();
        ws.SetRequestHeader("Cookie", $"api={tickleResponse.Session}");
        ws.SetRequestHeader("User-Agent", "ClientPortalGW/1");

        // Use system proxy if configured (e.g., HTTPS_PROXY environment variable)
        ws.Proxy = System.Net.WebRequest.DefaultWebProxy;

        await ws.ConnectAsync(uri, cancellationToken);
        _webSocket = ws;

        LogConnected();

        StartHeartbeat();
        StartMessagePump();
    }

    private async Task DisconnectAsync()
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.WebSocket.Disconnect");

        _heartbeatCts?.Cancel();
        _messagePumpCts?.Cancel();

        var ws = _webSocket;
        _webSocket = null;

        if (ws != null && ws.State == WebSocketState.Open)
        {
            try
            {
                await ws.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Closing",
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                LogDisconnectError(ex);
            }
        }

        if (ws != null)
        {
            await ws.DisposeAsync();
        }

        _heartbeatCts?.Dispose();
        _heartbeatCts = null;
        _messagePumpCts?.Dispose();
        _messagePumpCts = null;
    }

    private void StartHeartbeat()
    {
        _heartbeatCts?.Cancel();
        _heartbeatCts?.Dispose();
        _heartbeatCts = new CancellationTokenSource();
        var ct = _heartbeatCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_heartbeatIntervalSeconds), _timeProvider, ct);
                    try
                    {
                        await SendTextAsync("tic", ct);
                        _heartbeatCount.Add(1);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        LogHeartbeatError(ex);
                        _ = Task.Run(() => ReconnectAsync(_disposeCts.Token));
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
        }, ct);
    }

    private void StartMessagePump()
    {
        _messagePumpCts?.Cancel();
        _messagePumpCts?.Dispose();
        _messagePumpCts = new CancellationTokenSource();
        var ct = _messagePumpCts.Token;

        _ = Task.Run(async () =>
        {
            var buffer = new byte[_receiveBufferSize];
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var ws = _webSocket;
                    if (ws == null || ws.State != WebSocketState.Open)
                    {
                        break;
                    }

                    try
                    {
                        using var ms = new MemoryStream();
                        ValueWebSocketReceiveResult result;
                        do
                        {
                            result = await ws.ReceiveAsync(
                                buffer.AsMemory(), ct);
                            ms.Write(buffer, 0, result.Count);
                        }
                        while (!result.EndOfMessage);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            LogWebSocketClosed();
                            _ = Task.Run(() => ReconnectAsync(_disposeCts.Token));
                            break;
                        }

                        Interlocked.Exchange(ref _lastMessageReceivedAtTicks, _timeProvider.GetUtcNow().UtcTicks);

                        var text = Encoding.UTF8.GetString(ms.ToArray());
                        ProcessMessage(text);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (WebSocketException ex)
                    {
                        LogWebSocketError(ex);
                        _ = Task.Run(() => ReconnectAsync(_disposeCts.Token));
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
        }, ct);
    }

    private void ProcessMessage(string text)
    {
        JsonElement root;
        try
        {
            root = JsonDocument.Parse(text).RootElement;
        }
        catch (JsonException ex)
        {
            LogMessageParseError(ex);
            return;
        }

        if (!root.TryGetProperty("topic", out var topicElement))
        {
            return;
        }

        var topic = topicElement.GetString();
        if (topic == null)
        {
            return;
        }

        // Handle internal topics
        if (topic == "tic" || topic == "system" || topic == "sts")
        {
            return;
        }

        // Extract prefix: "smd+265598" -> "smd", "sor" -> "sor"
        var plusIndex = topic.IndexOf('+');
        var prefix = plusIndex >= 0 ? topic[..plusIndex] : topic;

        _messagesReceived.Add(1, new KeyValuePair<string, object?>(LogFields.Topic, prefix));

        if (_subscribers.TryGetValue(prefix, out var writers))
        {
            lock (writers)
            {
                foreach (var writer in writers)
                {
                    writer.TryWrite(root);
                }
            }
        }
    }

    private async Task ReconnectAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        await _connectLock.WaitAsync(cancellationToken);
        try
        {
            using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.WebSocket.Reconnect");
            activity?.SetTag(LogFields.Trigger, "connection_lost");

            _reconnectCount.Add(1, new KeyValuePair<string, object?>(LogFields.Trigger, "connection_lost"));
            LogReconnecting();

            await DisconnectAsync();

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(_reconnectDelayMs), _timeProvider, cancellationToken);
                await ConnectCoreAsync(cancellationToken);

                // Replay active subscriptions
                string[] subscriptions;
                lock (_subscriptionLock)
                {
                    subscriptions = [.. _activeSubscriptions];
                }

                foreach (var sub in subscriptions)
                {
                    await SendTextAsync(sub, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                LogReconnectError(ex);
            }
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private async Task OnSessionRefreshedAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        LogSessionRefreshed();
        await ReconnectAsync(cancellationToken);
    }

    private async Task SendTextAsync(string message, CancellationToken cancellationToken)
    {
        var ws = _webSocket;
        if (ws == null || ws.State != WebSocketState.Open)
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(message);
        await ws.SendAsync(
            bytes.AsMemory(),
            WebSocketMessageType.Text,
            true,
            cancellationToken);

        _messagesSent.Add(1);
    }

    private void Unsubscribe(string topicPrefix, ChannelWriter<JsonElement> writer, string subscribeMessage)
    {
        if (_subscribers.TryGetValue(topicPrefix, out var writers))
        {
            lock (writers)
            {
                writers.Remove(writer);
            }
        }

        writer.TryComplete();

        lock (_subscriptionLock)
        {
            _activeSubscriptions.Remove(subscribeMessage);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Connecting to IBKR WebSocket")]
    private partial void LogConnecting();

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to IBKR WebSocket")]
    private partial void LogConnected();

    [LoggerMessage(Level = LogLevel.Warning, Message = "WebSocket disconnect error")]
    private partial void LogDisconnectError(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Heartbeat send failed")]
    private partial void LogHeartbeatError(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "WebSocket connection closed by server")]
    private partial void LogWebSocketClosed();

    [LoggerMessage(Level = LogLevel.Warning, Message = "WebSocket receive error")]
    private partial void LogWebSocketError(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to parse WebSocket message")]
    private partial void LogMessageParseError(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reconnecting to IBKR WebSocket")]
    private partial void LogReconnecting();

    [LoggerMessage(Level = LogLevel.Warning, Message = "WebSocket reconnect failed")]
    private partial void LogReconnectError(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Session refreshed, triggering WebSocket reconnect")]
    private partial void LogSessionRefreshed();
}
