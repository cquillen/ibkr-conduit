using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.WebSockets;

namespace IbkrConduit.Streaming;

/// <summary>
/// Production adapter wrapping <see cref="ClientWebSocket"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class ClientWebSocketAdapter : IWebSocketAdapter
{
    private readonly ClientWebSocket _ws = new();

    /// <inheritdoc />
    public WebSocketState State => _ws.State;

    /// <inheritdoc />
    public Task ConnectAsync(Uri uri, CancellationToken cancellationToken) =>
        _ws.ConnectAsync(uri, cancellationToken);

    /// <inheritdoc />
    public Task SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType,
        bool endOfMessage, CancellationToken cancellationToken) =>
        _ws.SendAsync(buffer, messageType, endOfMessage, cancellationToken).AsTask();

    /// <inheritdoc />
    public ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(
        Memory<byte> buffer, CancellationToken cancellationToken) =>
        _ws.ReceiveAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
        CancellationToken cancellationToken) =>
        _ws.CloseAsync(closeStatus, statusDescription, cancellationToken);

    /// <inheritdoc />
    public void SetRequestHeader(string name, string value) =>
        _ws.Options.SetRequestHeader(name, value);

    /// <inheritdoc />
    public IWebProxy? Proxy
    {
        set => _ws.Options.Proxy = value;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _ws.Dispose();
        return ValueTask.CompletedTask;
    }
}
