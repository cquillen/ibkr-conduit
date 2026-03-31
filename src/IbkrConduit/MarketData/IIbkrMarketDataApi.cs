using Refit;

namespace IbkrConduit.MarketData;

/// <summary>
/// Refit interface for IBKR market data endpoints.
/// </summary>
public interface IIbkrMarketDataApi
{
    /// <summary>
    /// Retrieves market data snapshots for the specified contract IDs and fields.
    /// </summary>
    /// <param name="conids">Comma-separated contract IDs.</param>
    /// <param name="fields">Comma-separated field IDs (see <see cref="MarketDataFields"/>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Get("/v1/api/iserver/marketdata/snapshot")]
    Task<List<MarketDataSnapshotRaw>> GetSnapshotAsync(
        [Query] string conids, [Query] string fields,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves historical OHLCV bar data for a contract.
    /// </summary>
    /// <param name="conid">The contract ID.</param>
    /// <param name="period">Time period (e.g., "1d", "1w", "1m", "1y").</param>
    /// <param name="bar">Bar size (e.g., "1min", "5min", "1h", "1d").</param>
    /// <param name="outsideRth">Whether to include data outside regular trading hours.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Get("/v1/api/iserver/marketdata/history")]
    Task<HistoricalDataResponse> GetHistoryAsync(
        [Query] string conid, [Query] string period,
        [Query] string bar, [Query] bool? outsideRth = null,
        CancellationToken cancellationToken = default);
}
