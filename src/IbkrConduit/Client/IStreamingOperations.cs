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
    /// Pushed when the brokerage authentication state changes. Subscribe before
    /// <see cref="ConnectAsync"/> to receive the initial-on-connect state.
    /// </summary>
    IObservable<SessionStatusEvent> SessionStatus { get; }

    /// <summary>Urgent bulletins about exchange issues, system problems, or trading information.</summary>
    IObservable<BulletinEvent> Bulletins { get; }

    /// <summary>Brief messages regarding trading activity. Distinct from <see cref="IIbkrClient.Notifications"/> which is the FYI/alerts HTTP API.</summary>
    IObservable<NotificationEvent> TradingNotifications { get; }

    /// <summary>
    /// Subscribes to real-time market data for the specified contract.
    /// </summary>
    /// <param name="conid">Contract identifier.</param>
    /// <param name="fields">Array of field IDs (use <see cref="MarketData.MarketDataFields"/> constants).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that resolves to an observable stream of market data ticks.</returns>
    Task<IObservable<MarketDataTick>> MarketDataAsync(int conid, string[] fields, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to real-time order updates.
    /// </summary>
    /// <param name="days">Optional number of days of order history to include.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that resolves to an observable stream of order updates.</returns>
    Task<IObservable<OrderUpdate>> OrderUpdatesAsync(int? days = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to real-time profit and loss updates.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that resolves to an observable stream of P&amp;L updates.</returns>
    Task<IObservable<PnlUpdate>> ProfitAndLossAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to real-time account summary updates.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that resolves to an observable stream of account summary updates.</returns>
    Task<IObservable<AccountSummaryUpdate>> AccountSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to real-time account ledger updates.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that resolves to an observable stream of account ledger updates.</returns>
    Task<IObservable<AccountLedgerUpdate>> AccountLedgerAsync(CancellationToken cancellationToken = default);
}
