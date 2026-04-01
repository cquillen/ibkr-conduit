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

    /// <summary>
    /// Retrieves a regulatory snapshot for a single contract.
    /// <para>
    /// <strong>WARNING:</strong> This endpoint incurs a $0.01 USD fee per request on both
    /// live and paper trading accounts. Use sparingly.
    /// </para>
    /// </summary>
    /// <param name="conid">The contract identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<MarketDataSnapshot> GetRegulatorySnapshotAsync(int conid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from market data for a specific contract.
    /// </summary>
    /// <param name="conid">The contract identifier to unsubscribe.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<UnsubscribeResponse> UnsubscribeAsync(int conid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from all market data subscriptions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<UnsubscribeAllResponse> UnsubscribeAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a market scanner using the iserver backend.
    /// Returns a maximum of 50 contracts.
    /// </summary>
    /// <param name="request">The scanner request parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ScannerResponse> RunScannerAsync(ScannerRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves available scanner parameters including scanner types, instruments,
    /// locations, and filters.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ScannerParameters> GetScannerParametersAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a market scanner using the HMDS backend.
    /// Returns a maximum of 250 contracts.
    /// </summary>
    /// <param name="request">The HMDS scanner request parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HmdsScannerResponse> RunHmdsScannerAsync(HmdsScannerRequest request,
        CancellationToken cancellationToken = default);
}
