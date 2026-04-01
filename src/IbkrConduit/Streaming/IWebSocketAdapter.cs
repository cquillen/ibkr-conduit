using System.Net;
using System.Net.WebSockets;

namespace IbkrConduit.Streaming;

/// <summary>
/// Abstraction over <see cref="ClientWebSocket"/> to enable unit testing of WebSocket logic.
/// </summary>
internal interface IWebSocketAdapter : IAsyncDisposable
{
    /// <summary>Gets the current state of the WebSocket connection.</summary>
    WebSocketState State { get; }

    /// <summary>Connects to a WebSocket server.</summary>
    Task ConnectAsync(Uri uri, CancellationToken cancellationToken);

    /// <summary>Sends data over the WebSocket.</summary>
    Task SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType,
        bool endOfMessage, CancellationToken cancellationToken);

    /// <summary>Receives data from the WebSocket.</summary>
    ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(
        Memory<byte> buffer, CancellationToken cancellationToken);

    /// <summary>Closes the WebSocket connection.</summary>
    Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
        CancellationToken cancellationToken);

    /// <summary>Sets a request header for the WebSocket handshake.</summary>
    void SetRequestHeader(string name, string value);

    /// <summary>Sets or gets the proxy for the WebSocket connection.</summary>
    IWebProxy? Proxy { set; }
}
