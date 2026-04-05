using Refit;

namespace IbkrConduit.MarketData;

/// <summary>
/// Refit interface for IBKR market data endpoints.
/// </summary>
internal interface IIbkrMarketDataApi
{
    /// <summary>
    /// Retrieves market data snapshots for the specified contract IDs and fields.
    /// </summary>
    /// <param name="conids">Comma-separated contract IDs.</param>
    /// <param name="fields">Comma-separated field IDs (see <see cref="MarketDataFields"/>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Get("/v1/api/iserver/marketdata/snapshot")]
    Task<IApiResponse<List<MarketDataSnapshotRaw>>> GetSnapshotAsync(
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
    Task<IApiResponse<HistoricalDataResponse>> GetHistoryAsync(
        [Query] string conid, [Query] string period,
        [Query] string bar, [Query] bool? outsideRth = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a regulatory snapshot for a single contract.
    /// <para>
    /// <strong>WARNING:</strong> This endpoint incurs a $0.01 USD fee per request on both
    /// live and paper trading accounts. Use sparingly.
    /// </para>
    /// </summary>
    /// <param name="conid">The contract identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Get("/v1/api/md/regsnapshot")]
    Task<IApiResponse<MarketDataSnapshotRaw>> GetRegulatorySnapshotAsync(
        [Query] int conid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from market data for a specific contract.
    /// </summary>
    /// <param name="request">The unsubscribe request containing the contract ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Post("/v1/api/iserver/marketdata/unsubscribe")]
    Task<IApiResponse<UnsubscribeResponse>> UnsubscribeAsync(
        [Body] UnsubscribeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from all market data subscriptions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Get("/v1/api/iserver/marketdata/unsubscribeall")]
    Task<IApiResponse<UnsubscribeAllResponse>> UnsubscribeAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a market scanner using the iserver backend.
    /// Returns a maximum of 50 contracts.
    /// </summary>
    /// <param name="request">The scanner request parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Post("/v1/api/iserver/scanner/run")]
    Task<IApiResponse<ScannerResponse>> RunScannerAsync(
        [Body] ScannerRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves available scanner parameters including scanner types, instruments,
    /// locations, and filters.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Get("/v1/api/iserver/scanner/params")]
    Task<IApiResponse<ScannerParameters>> GetScannerParametersAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a market scanner using the HMDS backend.
    /// Returns a maximum of 250 contracts.
    /// </summary>
    /// <param name="request">The HMDS scanner request parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Post("/v1/api/hmds/scanner")]
    Task<IApiResponse<HmdsScannerResponse>> RunHmdsScannerAsync(
        [Body] HmdsScannerRequest request,
        CancellationToken cancellationToken = default);
}
