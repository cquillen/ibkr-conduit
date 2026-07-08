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
    private readonly SemaphoreSlim _receiveStartedSignal = new(0);
    private WebSocketState _state = WebSocketState.None;
    private bool _failOnConnect;
    private int _sendCount;
    private int _connectCallCount;
    private int _disposeCallCount;
    private ConnectGate? _connectGate;

    public WebSocketState State => _state;

    public ConcurrentQueue<string> SentMessages => _sentMessages;

    public Dictionary<string, string> RequestHeaders { get; } = new();

    public IWebProxy? LastProxy { get; private set; }

    public Uri? ConnectedUri { get; private set; }

    public int? FailSendAfterCount { get; set; }

    public int ConnectCallCount => Volatile.Read(ref _connectCallCount);

    /// <summary>Number of times <see cref="DisposeAsync"/> has been invoked (STR-6 leak assertions).</summary>
    public int DisposeCallCount => Volatile.Read(ref _disposeCallCount);

    public bool FailOnConnect
    {
        set => _failOnConnect = value;
    }

    /// <summary>
    /// Installs a one-shot gate that parks the next <see cref="ConnectAsync"/> call after it has
    /// entered (holding the client's <c>_connectLock</c>) until the returned gate is released or the
    /// call's cancellation token fires. Used to pin the dispose-vs-in-flight-reconnect race (STR-3).
    /// </summary>
    public ConnectGate InstallConnectGate() => _connectGate = new ConnectGate();

    public async Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _connectCallCount);

        var gate = _connectGate;
        if (gate is not null)
        {
            _connectGate = null; // one-shot
            gate.Entered.TrySetResult();
            await gate.Release.Task.WaitAsync(cancellationToken);
        }

        if (_failOnConnect)
        {
            throw new WebSocketException("Simulated connection failure");
        }

        ConnectedUri = uri;
        _state = WebSocketState.Open;
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
        _receiveStartedSignal.Release();
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

    /// <summary>
    /// Returns a task that completes the next time <see cref="ReceiveAsync"/> is called,
    /// confirming the message pump is running and waiting for data.
    /// </summary>
    public async Task WaitForReceiveAsync(CancellationToken cancellationToken) =>
        await _receiveStartedSignal.WaitAsync(cancellationToken);

    public ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref _disposeCallCount);
        _state = WebSocketState.Closed;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// A one-shot gate for <see cref="ConnectAsync"/>: <see cref="Entered"/> completes when the
    /// gated connect begins; the connect then awaits <see cref="Release"/> (or observes cancellation).
    /// </summary>
    internal sealed class ConnectGate
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
