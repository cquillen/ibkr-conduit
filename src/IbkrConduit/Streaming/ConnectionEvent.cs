namespace IbkrConduit.Streaming;

/// <summary>
/// A consumer-observable WebSocket connection-lifecycle transition, surfaced via
/// <see cref="IbkrConduit.Client.IStreamingOperations.SubscribeConnectionEvents"/>. Per
/// <see href="../../docs/adr/0002-streaming-delivery-guarantee.md">ADR-0002</see>, reconnect/gap
/// transitions are consumer-observable so a consumer can bound a coverage gap and trigger REST
/// reconciliation deterministically instead of inferring an outage from message staleness. The
/// concrete transitions are <see cref="ConnectionDisconnected"/> and
/// <see cref="ConnectionReconnected"/>.
/// </summary>
/// <param name="Timestamp">When the transition was observed by the library (UTC).</param>
public abstract record ConnectionEvent(DateTimeOffset Timestamp);

/// <summary>
/// The WebSocket connection was lost or torn down for a reconnect. Emitted at the start of every
/// reconnect path (server close, receive error, heartbeat failure, session refresh, tickle
/// watchdog). A subsequent <see cref="ConnectionReconnected"/> marks the end of the gap; frames
/// executed during the gap may be missed if the consumer suppressed IBKR's snapshot replay
/// (<c>realtimeUpdatesOnly=true</c>), so treat this as a signal to reconcile via REST.
/// </summary>
/// <param name="Timestamp">When the disconnect was observed (UTC).</param>
/// <param name="Reason">Why the reconnect was triggered (e.g. <c>server_close</c>, <c>receive_error</c>, <c>heartbeat_failure</c>, <c>session_refresh</c>, <c>tickle_watchdog</c>).</param>
public sealed record ConnectionDisconnected(DateTimeOffset Timestamp, string Reason)
    : ConnectionEvent(Timestamp);

/// <summary>
/// The WebSocket reconnected and the active subscriptions were replayed on the new socket. Marks
/// the end of the coverage gap opened by the preceding <see cref="ConnectionDisconnected"/>.
/// </summary>
/// <param name="Timestamp">When the reconnect completed (UTC).</param>
/// <param name="ReplayedTopics">The distinct wire topic prefixes whose subscriptions were replayed on the new connection (e.g. <c>smd</c>, <c>sor</c>, <c>str</c>).</param>
public sealed record ConnectionReconnected(DateTimeOffset Timestamp, IReadOnlyList<string> ReplayedTopics)
    : ConnectionEvent(Timestamp);
