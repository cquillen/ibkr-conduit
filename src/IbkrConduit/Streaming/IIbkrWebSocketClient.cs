using System.Text.Json;
using System.Threading.Channels;

namespace IbkrConduit.Streaming;

/// <summary>
/// Internal interface for the IBKR WebSocket client, enabling testability.
/// </summary>
internal interface IIbkrWebSocketClient : IAsyncDisposable
{
    /// <summary>
    /// Connects to the IBKR WebSocket API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Subscribes to a WebSocket topic and returns a channel reader for receiving messages.
    /// </summary>
    /// <param name="subscribeMessage">The subscribe message to send on the WebSocket.</param>
    /// <param name="topicPrefix">The topic prefix for routing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple of the channel reader and an unsubscribe action.</returns>
    Task<(ChannelReader<JsonElement> Reader, Action Unsubscribe)> SubscribeTopicAsync(
        string subscribeMessage,
        string topicPrefix,
        CancellationToken cancellationToken);
}
