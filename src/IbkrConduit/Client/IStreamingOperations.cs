using IbkrConduit.Streaming;

namespace IbkrConduit.Client;

/// <summary>
/// Provides real-time WebSocket streaming operations for market data, orders, P&amp;L,
/// account summary, and account ledger updates.
/// </summary>
public interface IStreamingOperations
{
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
