using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using IbkrConduit.Auth;
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
    private const int _heartbeatIntervalSeconds = 10;
    private const int _reconnectDelayMs = 1000;
    private const int _receiveBufferSize = 8192;

    private readonly IIbkrSessionApi _sessionApi;
    private readonly IbkrOAuthCredentials _credentials;
    private readonly ISessionLifecycleNotifier _notifier;
    private readonly ILogger<IbkrWebSocketClient> _logger;
    private readonly ConcurrentDictionary<string, List<ChannelWriter<JsonElement>>> _subscribers = new();
    private readonly List<string> _activeSubscriptions = [];
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly object _subscriptionLock = new();

    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _heartbeatCts;
    private CancellationTokenSource? _messagePumpCts;
    private IDisposable? _notifierSubscription;
    private bool _disposed;

    /// <summary>
    /// Creates a new <see cref="IbkrWebSocketClient"/>.
    /// </summary>
    /// <param name="sessionApi">Session API for tickle calls.</param>
    /// <param name="credentials">OAuth credentials containing the access token.</param>
    /// <param name="notifier">Session lifecycle notifier for reconnect on re-auth.</param>
    /// <param name="logger">Logger instance.</param>
    public IbkrWebSocketClient(
        IIbkrSessionApi sessionApi,
        IbkrOAuthCredentials credentials,
        ISessionLifecycleNotifier notifier,
        ILogger<IbkrWebSocketClient> logger)
    {
        _sessionApi = sessionApi;
        _credentials = credentials;
        _notifier = notifier;
        _logger = logger;

        _notifierSubscription = _notifier.Subscribe(OnSessionRefreshedAsync);
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
        if (_webSocket == null || _webSocket.State != WebSocketState.Open)
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
    }

    private async Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            return;
        }

        LogConnecting();

        var tickleResponse = await _sessionApi.TickleAsync(cancellationToken);

        var uri = new Uri($"{_webSocketBaseUrl}?oauth_token={_credentials.AccessToken}");
        var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("Cookie", $"api={tickleResponse.Session}");
        ws.Options.SetRequestHeader("User-Agent", "ClientPortalGW/1");

        // Use system proxy if configured (e.g., HTTPS_PROXY environment variable)
        ws.Options.Proxy = System.Net.WebRequest.DefaultWebProxy;

        await ws.ConnectAsync(uri, cancellationToken);
        _webSocket = ws;

        LogConnected();

        StartHeartbeat();
        StartMessagePump();
    }

    private async Task DisconnectAsync()
    {
        _heartbeatCts?.Cancel();
        _messagePumpCts?.Cancel();

        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Closing",
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                LogDisconnectError(ex);
            }
        }

        _webSocket?.Dispose();
        _webSocket = null;

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
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_heartbeatIntervalSeconds));
            try
            {
                while (await timer.WaitForNextTickAsync(ct))
                {
                    try
                    {
                        await SendTextAsync("tic", ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        LogHeartbeatError(ex);
                        _ = Task.Run(() => ReconnectAsync(CancellationToken.None));
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
                while (!ct.IsCancellationRequested && _webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    try
                    {
                        using var ms = new MemoryStream();
                        WebSocketReceiveResult result;
                        do
                        {
                            result = await _webSocket.ReceiveAsync(
                                new ArraySegment<byte>(buffer), ct);
                            ms.Write(buffer, 0, result.Count);
                        }
                        while (!result.EndOfMessage);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            LogWebSocketClosed();
                            _ = Task.Run(() => ReconnectAsync(CancellationToken.None));
                            break;
                        }

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
                        _ = Task.Run(() => ReconnectAsync(CancellationToken.None));
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
            LogReconnecting();

            await DisconnectAsync();

            try
            {
                await Task.Delay(_reconnectDelayMs, cancellationToken);
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
        if (_webSocket == null || _webSocket.State != WebSocketState.Open)
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(message);
        await _webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            cancellationToken);
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
