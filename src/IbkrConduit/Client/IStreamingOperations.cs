using IbkrConduit.Streaming;

namespace IbkrConduit.Client;

/// <summary>
/// Provides real-time WebSocket streaming operations for market data, orders, P&amp;L,
/// account summary, and account ledger updates.
/// </summary>
public interface IStreamingOperations
{
    /// <summary>
    /// Opens the WebSocket connection. Must be called after configuring all
    /// subscriptions (<see cref="MarketDataAsync"/>, <see cref="OrderUpdatesAsync"/>,
    /// etc.) so that subscribers are in place before IBKR's initial-on-connect
    /// messages arrive.
    ///
    /// Idempotent: calling on an already-connected client is a no-op.
    /// Re-calling after disconnect re-opens the connection and replays all
    /// active subscriptions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the underlying WebSocket connection is currently open.
    /// Use this to surface a real connection-state indicator in monitoring UIs
    /// instead of inferring connectivity from message-arrival timing.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Timestamp of the last received WebSocket message, or null if no messages
    /// have been received yet. Useful for staleness detection in quiet markets,
    /// where the connection is still healthy but no ticks are arriving.
    /// </summary>
    DateTimeOffset? LastMessageReceivedAt { get; }

    /// <summary>
    /// Subscribes to consumer-visible WebSocket connection-lifecycle events
    /// (<see cref="ConnectionDisconnected"/> / <see cref="ConnectionReconnected"/>). Every reconnect
    /// path emits a <see cref="ConnectionDisconnected"/> at the start of the gap and a
    /// <see cref="ConnectionReconnected"/> (listing the replayed topics) once the socket is back and
    /// subscriptions are replayed. Use this to bound a coverage gap and trigger REST reconciliation
    /// deterministically instead of inferring an outage from message staleness. Subscribe before
    /// <see cref="ConnectAsync"/>; the returned <see cref="IIbkrSubscription{T}.Stream"/> is
    /// single-observer.
    /// </summary>
    /// <returns>A subscription handle whose <see cref="IIbkrSubscription{T}.Stream"/> pushes connection-lifecycle events.</returns>
    IIbkrSubscription<ConnectionEvent> SubscribeConnectionEvents();

    /// <summary>
    /// Subscribes to brokerage authentication-state changes. Call this method before
    /// <see cref="ConnectAsync"/> to receive the initial-on-connect state.
    /// </summary>
    /// <returns>A subscription handle whose <see cref="IIbkrSubscription{T}.Stream"/> pushes session-status events.</returns>
    IIbkrSubscription<SessionStatusEvent> SubscribeSessionStatus();

    /// <summary>Subscribes to urgent bulletins about exchange issues, system problems, or trading information.</summary>
    /// <returns>A subscription handle whose <see cref="IIbkrSubscription{T}.Stream"/> pushes bulletin events.</returns>
    IIbkrSubscription<BulletinEvent> SubscribeBulletins();

    /// <summary>Subscribes to brief messages regarding trading activity. Distinct from <see cref="IIbkrClient.Notifications"/> which is the FYI/alerts HTTP API.</summary>
    /// <returns>A subscription handle whose <see cref="IIbkrSubscription{T}.Stream"/> pushes trading-notification events.</returns>
    IIbkrSubscription<NotificationEvent> SubscribeTradingNotifications();

    /// <summary>Subscribes to system-level events: initial connection confirmation and periodic 10-second server heartbeats. Call this method before <see cref="ConnectAsync"/> to receive the initial username confirmation.</summary>
    /// <returns>A subscription handle whose <see cref="IIbkrSubscription{T}.Stream"/> pushes system events.</returns>
    IIbkrSubscription<SystemEvent> SubscribeSystemEvents();

    /// <summary>Subscribes to account configuration updates: account list, capabilities, allowed asset types. Not financial data — see <see cref="PnlUpdate"/> / <see cref="AccountSummaryUpdate"/> / <see cref="AccountLedgerUpdate"/>. Call this method before <see cref="ConnectAsync"/> to receive the initial account configuration.</summary>
    /// <returns>A subscription handle whose <see cref="IIbkrSubscription{T}.Stream"/> pushes account-status events.</returns>
    IIbkrSubscription<AccountStatusEvent> SubscribeAccountStatus();

    /// <summary>
    /// Subscribes to real-time market data for the specified contract.
    /// </summary>
    /// <remarks>
    /// <b>May block on an in-flight reconnect:</b> if a reconnect is currently in progress, this
    /// call waits for it to finish before sending the subscribe message, guaranteeing the
    /// subscription is sent exactly once per connection. A reconnect can take tens of seconds
    /// under a degraded network; pass a <paramref name="cancellationToken"/> with a deadline if
    /// the call must be bounded.
    /// </remarks>
    /// <param name="conid">Contract identifier.</param>
    /// <param name="fields">Array of field IDs (use <see cref="MarketData.MarketDataFields"/> constants).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that resolves to a subscription handle whose <see cref="IIbkrSubscription{T}.Stream"/> emits market data ticks.</returns>
    Task<IIbkrSubscription<MarketDataTick>> MarketDataAsync(int conid, string[] fields, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to real-time order updates over the <c>sor</c> topic. A single WebSocket frame may
    /// carry several order updates; each is emitted as a separate item.
    /// </summary>
    /// <remarks>
    /// <b>Filtered live-orders interaction (§10.6):</b> IBKR suppresses <c>sor</c> order-detail frames
    /// after a <em>filtered</em> <see cref="IOrderOperations.GetLiveOrdersAsync"/> call until a
    /// <c>force=true</c> follow-up clears the cached behavior. This library issues that follow-up
    /// automatically after any filtered call, so subscribing here still delivers order details.
    /// <para>
    /// <b>May block on an in-flight reconnect:</b> if a reconnect is currently in progress, this
    /// call waits for it to finish before sending the subscribe message, guaranteeing the
    /// subscription is sent exactly once per connection. A reconnect can take tens of seconds
    /// under a degraded network; pass a <paramref name="cancellationToken"/> with a deadline if
    /// the call must be bounded.
    /// </para>
    /// </remarks>
    /// <param name="days">Optional number of days of order history to include.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that resolves to a subscription handle whose <see cref="IIbkrSubscription{T}.Stream"/> emits order updates.</returns>
    Task<IIbkrSubscription<OrderUpdate>> OrderUpdatesAsync(int? days = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to the real-time trade execution stream (IBKR <c>str</c> topic).
    /// Emits one item per execution. On subscribe IBKR replays historical executions
    /// (up to <paramref name="days"/>) unless <paramref name="realtimeUpdatesOnly"/> is
    /// true; the same replay occurs after any reconnect, so consumers should dedupe on
    /// <see cref="TradeExecution.ExecutionId"/>.
    /// </summary>
    /// <remarks>
    /// <b>May block on an in-flight reconnect:</b> if a reconnect is currently in progress, this
    /// call waits for it to finish before sending the subscribe message, guaranteeing the
    /// subscription is sent exactly once per connection. A reconnect can take tens of seconds
    /// under a degraded network; pass a <paramref name="cancellationToken"/> with a deadline if
    /// the call must be bounded.
    /// </remarks>
    /// <param name="realtimeUpdatesOnly">When true, suppress historical executions and stream new fills only. Omitted from the wire message when null (IBKR default: false).</param>
    /// <param name="days">Days of historical executions to include on subscribe. Omitted from the wire message when null (IBKR default: 1).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that resolves to a subscription handle whose <see cref="IIbkrSubscription{T}.Stream"/> emits trade executions.</returns>
    Task<IIbkrSubscription<TradeExecution>> TradeExecutionsAsync(
        bool? realtimeUpdatesOnly = null,
        int? days = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to real-time profit and loss updates. A single WebSocket frame may carry
    /// P&amp;L for several accounts; each account is emitted as a separate item.
    /// </summary>
    /// <remarks>
    /// <b>May block on an in-flight reconnect:</b> if a reconnect is currently in progress, this
    /// call waits for it to finish before sending the subscribe message, guaranteeing the
    /// subscription is sent exactly once per connection. A reconnect can take tens of seconds
    /// under a degraded network; pass a <paramref name="cancellationToken"/> with a deadline if
    /// the call must be bounded.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that resolves to a subscription handle whose <see cref="IIbkrSubscription{T}.Stream"/> emits P&amp;L updates.</returns>
    Task<IIbkrSubscription<PnlUpdate>> ProfitAndLossAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to real-time account summary updates for the specified account.
    /// </summary>
    /// <remarks>
    /// <b>May block on an in-flight reconnect:</b> if a reconnect is currently in progress, this
    /// call waits for it to finish before sending the subscribe message, guaranteeing the
    /// subscription is sent exactly once per connection. A reconnect can take tens of seconds
    /// under a degraded network; pass a <paramref name="cancellationToken"/> with a deadline if
    /// the call must be bounded.
    /// </remarks>
    /// <param name="accountId">The account whose summary data to stream (required).</param>
    /// <param name="keys">Optional filter keys to limit the summary rows returned, e.g. "AccruedCash-S", "ExcessLiquidity-S".</param>
    /// <param name="fields">Optional field filter to limit the columns returned, e.g. "currency", "monetaryValue".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that resolves to a subscription handle whose <see cref="IIbkrSubscription{T}.Stream"/> emits account summary updates.</returns>
    Task<IIbkrSubscription<AccountSummaryUpdate>> AccountSummaryAsync(
        string accountId,
        string[]? keys = null,
        string[]? fields = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to real-time account ledger updates for the specified account.
    /// </summary>
    /// <remarks>
    /// <b>May block on an in-flight reconnect:</b> if a reconnect is currently in progress, this
    /// call waits for it to finish before sending the subscribe message, guaranteeing the
    /// subscription is sent exactly once per connection. A reconnect can take tens of seconds
    /// under a degraded network; pass a <paramref name="cancellationToken"/> with a deadline if
    /// the call must be bounded.
    /// </remarks>
    /// <param name="accountId">The account whose ledger data to stream (required).</param>
    /// <param name="keys">Optional filter keys to limit the ledger currencies returned, e.g. "LedgerListUSD", "LedgerListBASE".</param>
    /// <param name="fields">Optional field filter to limit the columns returned, e.g. "cashBalance", "exchangeRate".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that resolves to a subscription handle whose <see cref="IIbkrSubscription{T}.Stream"/> emits account ledger updates.</returns>
    Task<IIbkrSubscription<AccountLedgerUpdate>> AccountLedgerAsync(
        string accountId,
        string[]? keys = null,
        string[]? fields = null,
        CancellationToken cancellationToken = default);
}
