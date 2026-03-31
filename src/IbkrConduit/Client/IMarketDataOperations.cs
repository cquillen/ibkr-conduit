using IbkrConduit.MarketData;

namespace IbkrConduit.Client;

/// <summary>
/// Market data operations on the IBKR API.
/// </summary>
public interface IMarketDataOperations
{
    /// <summary>
    /// Retrieves market data snapshots for the specified contract IDs and fields.
    /// Transparently handles IBKR pre-flight requirements by detecting empty responses
    /// and retrying after a brief delay.
    /// </summary>
    /// <param name="conids">The contract IDs to request data for.</param>
    /// <param name="fields">The field IDs to request (see <see cref="MarketDataFields"/>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<List<MarketDataSnapshot>> GetSnapshotAsync(int[] conids, string[] fields,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves historical OHLCV bar data for a contract.
    /// </summary>
    /// <param name="conid">The contract ID.</param>
    /// <param name="period">Time period (e.g., "1d", "1w", "1m", "1y").</param>
    /// <param name="bar">Bar size (e.g., "1min", "5min", "1h", "1d").</param>
    /// <param name="outsideRth">Whether to include data outside regular trading hours.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HistoricalDataResponse> GetHistoryAsync(int conid, string period, string bar,
        bool? outsideRth = null, CancellationToken cancellationToken = default);
}
