using System.Text.Json;
using System.Threading.Channels;

namespace IbkrConduit.Streaming;

/// <summary>
/// Internal interface for the IBKR WebSocket client, enabling testability.
/// </summary>
internal interface IIbkrWebSocketClient : IAsyncDisposable
{
    /// <summary>Whether the WebSocket connection is currently open.</summary>
    bool IsConnected { get; }

    /// <summary>Number of active topic subscriptions.</summary>
    int ActiveSubscriptionCount { get; }

    /// <summary>Timestamp of the last received WebSocket message, or null.</summary>
    DateTimeOffset? LastMessageReceivedAt { get; }

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
    /// <param name="cancelMessage">
    /// The IBKR unsubscribe message to send when the last subscription for this cancel
    /// message is torn down, or <see langword="null"/> for local-teardown-only topics
    /// (where no wire cancel exists). Multiple subscriptions can share the same cancel
    /// message; the cancel is only sent once the final subscriber referencing it is gone.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A tuple of the channel reader and an asynchronous unsubscribe delegate. Invoking the
    /// delegate removes this subscription and, when it was the last one referencing
    /// <paramref name="cancelMessage"/> and the socket is open, sends the cancel on the wire.
    /// </returns>
    /// <remarks>
    /// If the WebSocket is not yet connected, the subscription is queued in memory
    /// and replayed automatically when <see cref="ConnectAsync"/> is called. No wire
    /// message is sent until the connection is open. The returned channel reader is
    /// usable immediately; messages will start flowing once <see cref="ConnectAsync"/>
    /// completes.
    /// </remarks>
    Task<(ChannelReader<JsonElement> Reader, Func<CancellationToken, ValueTask> Unsubscribe)> SubscribeTopicAsync(
        string subscribeMessage,
        string topicPrefix,
        string? cancelMessage,
        CancellationToken cancellationToken);

    /// <summary>
    /// Registers a subscriber for an unsolicited topic (sts, system, act, blt, ntf).
    /// Does NOT send a subscribe message — IBKR pushes these regardless.
    /// </summary>
    /// <param name="topicPrefix">The topic prefix to listen on (e.g., "sts", "act").</param>
    /// <returns>
    /// A tuple of the channel reader and an asynchronous unsubscribe delegate that performs
    /// local teardown only (no wire cancel is sent for unsolicited topics).
    /// </returns>
    (ChannelReader<JsonElement> Reader, Func<CancellationToken, ValueTask> Unsubscribe) RegisterUnsolicitedTopic(string topicPrefix);
}
