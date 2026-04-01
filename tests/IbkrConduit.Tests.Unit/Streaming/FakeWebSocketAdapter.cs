using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Streaming;

namespace IbkrConduit.Tests.Unit.Streaming;

/// <summary>
/// In-memory WebSocket adapter for testing. Messages are exchanged via queues.
/// </summary>
internal sealed class FakeWebSocketAdapter : IWebSocketAdapter
{
    private readonly ConcurrentQueue<byte[]> _inboundMessages = new();
    private readonly ConcurrentQueue<string> _sentMessages = new();
    private readonly SemaphoreSlim _inboundSignal = new(0);
    private WebSocketState _state = WebSocketState.None;
    private bool _failOnConnect;
    private int _sendCount;

    public WebSocketState State => _state;

    public ConcurrentQueue<string> SentMessages => _sentMessages;

    public Dictionary<string, string> RequestHeaders { get; } = new();

    public IWebProxy? LastProxy { get; private set; }

    public Uri? ConnectedUri { get; private set; }

    public int? FailSendAfterCount { get; set; }

    public int ConnectCallCount { get; private set; }

    public bool FailOnConnect
    {
        set => _failOnConnect = value;
    }

    public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        ConnectCallCount++;

        if (_failOnConnect)
        {
            throw new WebSocketException("Simulated connection failure");
        }

        ConnectedUri = uri;
        _state = WebSocketState.Open;
        return Task.CompletedTask;
    }

    public Task SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType,
        bool endOfMessage, CancellationToken cancellationToken)
    {
        _sendCount++;

        if (FailSendAfterCount.HasValue && _sendCount > FailSendAfterCount.Value)
        {
            throw new WebSocketException("Simulated send failure");
        }

        var text = Encoding.UTF8.GetString(buffer.Span);
        _sentMessages.Enqueue(text);
        return Task.CompletedTask;
    }

    public async ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(
        Memory<byte> buffer, CancellationToken cancellationToken)
    {
        await _inboundSignal.WaitAsync(cancellationToken);

        if (_state != WebSocketState.Open)
        {
            return new ValueWebSocketReceiveResult(0, WebSocketMessageType.Close, true);
        }

        if (_inboundMessages.TryDequeue(out var data))
        {
            data.CopyTo(buffer);
            return new ValueWebSocketReceiveResult(data.Length, WebSocketMessageType.Text, true);
        }

        return new ValueWebSocketReceiveResult(0, WebSocketMessageType.Text, true);
    }

    public Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
        CancellationToken cancellationToken)
    {
        _state = WebSocketState.Closed;
        return Task.CompletedTask;
    }

    public void SetRequestHeader(string name, string value) =>
        RequestHeaders[name] = value;

    public IWebProxy? Proxy
    {
        set => LastProxy = value;
    }

    /// <summary>Enqueue a message as if it arrived from the server.</summary>
    public void EnqueueServerMessage(string json)
    {
        _inboundMessages.Enqueue(Encoding.UTF8.GetBytes(json));
        _inboundSignal.Release();
    }

    /// <summary>Signal a close frame from the server.</summary>
    public void SignalClose()
    {
        _state = WebSocketState.CloseReceived;
        _inboundSignal.Release();
    }

    public ValueTask DisposeAsync()
    {
        _state = WebSocketState.Closed;
        return ValueTask.CompletedTask;
    }
}
