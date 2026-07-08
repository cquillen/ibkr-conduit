using IbkrConduit.Errors;
using IbkrConduit.Flex;
using IbkrConduit.Health;
using IbkrConduit.Session;
using IbkrConduit.Streaming;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Client;

/// <summary>
/// Default implementation of <see cref="IIbkrClient"/> that delegates to
/// typed operations interfaces and manages session lifecycle.
/// </summary>
internal partial class IbkrClient : IIbkrClient
{
    private readonly IHealthStatusCollector _healthCollector;
    private readonly ISessionManager _sessionManager;
    private readonly IIbkrWebSocketClient _webSocketClient;
    private readonly IbkrClientOptions _options;
    private readonly ILogger<IbkrClient> _logger;

    /// <summary>
    /// Atomic teardown guard (0 = live, 1 = disposed). Ensures the full-client teardown in
    /// <see cref="DisposeAsync"/> runs exactly once even when invoked twice — e.g. by
    /// <c>await using client</c> plus the owning provider's disposal of this container-owned
    /// singleton (design doc §5.4, PVR-21).
    /// </summary>
    private int _disposed;

    /// <summary>
    /// Creates a new <see cref="IbkrClient"/> instance.
    /// </summary>
    /// <param name="portfolio">Portfolio operations.</param>
    /// <param name="contracts">Contract operations.</param>
    /// <param name="orders">Order operations.</param>
    /// <param name="marketData">Market data operations.</param>
    /// <param name="streaming">Streaming operations.</param>
    /// <param name="flex">Flex Web Service operations.</param>
    /// <param name="accounts">Account operations.</param>
    /// <param name="alerts">Alert operations.</param>
    /// <param name="watchlists">Watchlist operations.</param>
    /// <param name="notifications">FYI notification operations.</param>
    /// <param name="eventContracts">Event contract (ForecastEx) operations.</param>
    /// <param name="healthCollector">Health status collector for aggregated health checks.</param>
    /// <param name="sessionManager">The session manager for lifecycle management.</param>
    /// <param name="webSocketClient">The WebSocket client the facade disconnects and disposes as the first step of its full-client teardown (design doc §5.4).</param>
    /// <param name="options">Client configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    public IbkrClient(
        IPortfolioOperations portfolio,
        IContractOperations contracts,
        IOrderOperations orders,
        IMarketDataOperations marketData,
        IStreamingOperations streaming,
        IFlexOperations flex,
        IAccountOperations accounts,
        IAlertOperations alerts,
        IWatchlistOperations watchlists,
        IFyiOperations notifications,
        IEventContractOperations eventContracts,
        IHealthStatusCollector healthCollector,
        ISessionManager sessionManager,
        IIbkrWebSocketClient webSocketClient,
        IbkrClientOptions options,
        ILogger<IbkrClient> logger)
    {
        Portfolio = portfolio;
        Contracts = contracts;
        Orders = orders;
        MarketData = marketData;
        Streaming = streaming;
        Flex = flex;
        Accounts = accounts;
        Alerts = alerts;
        Watchlists = watchlists;
        Notifications = notifications;
        EventContracts = eventContracts;
        _healthCollector = healthCollector;
        _sessionManager = sessionManager;
        _webSocketClient = webSocketClient;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public IPortfolioOperations Portfolio { get; }

    /// <inheritdoc />
    public IContractOperations Contracts { get; }

    /// <inheritdoc />
    public IOrderOperations Orders { get; }

    /// <inheritdoc />
    public IMarketDataOperations MarketData { get; }

    /// <inheritdoc />
    public IStreamingOperations Streaming { get; }

    /// <inheritdoc />
    public IFlexOperations Flex { get; }

    /// <inheritdoc />
    public IAccountOperations Accounts { get; }

    /// <inheritdoc />
    public IAlertOperations Alerts { get; }

    /// <inheritdoc />
    public IWatchlistOperations Watchlists { get; }

    /// <inheritdoc />
    public IFyiOperations Notifications { get; }

    /// <inheritdoc />
    public IEventContractOperations EventContracts { get; }

    /// <inheritdoc />
    public Task<IbkrHealthStatus> GetHealthStatusAsync(
        bool activeProbe = false, CancellationToken cancellationToken = default) =>
        _healthCollector.GetHealthStatusAsync(activeProbe, cancellationToken);

    /// <inheritdoc />
    public async Task ValidateConnectionAsync(bool validateFlex = true, CancellationToken cancellationToken = default)
    {
        await _sessionManager.EnsureInitializedAsync(cancellationToken);

        if (validateFlex && _options.FlexToken is not null)
        {
            var queryId = _options.FlexQueries.CashTransactionsQueryId
                ?? _options.FlexQueries.TradeConfirmationsQueryId;

            if (queryId is not null)
            {
                await ValidateFlexTokenAsync(queryId, cancellationToken);
            }
            else
            {
                LogFlexValidationSkipped();
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Performs the full-client teardown in the <c>ManagedTenant</c> order (design doc §5.4, PVR-21):
    /// the WebSocket client is disconnected and disposed first, then the session is torn down —
    /// <c>SessionManager.DisposeAsync</c> issues the best-effort logout (unless suppressed for
    /// the manager path) and releases the session's resources. An atomic guard makes the teardown
    /// idempotent, so <c>await using client</c> plus the owning provider's disposal of these
    /// container-owned singletons runs it exactly once — no double-run logout or gauge decrement.
    /// Each disposed component's own <c>DisposeAsync</c> is independently idempotent, so a redundant
    /// provider disposal after this call is a safe no-op.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        // Atomic guard: claim the teardown exactly once. A second invocation (provider disposal
        // after `await using`, or a concurrent dispose) observes the flag already set and returns.
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // 1. WebSocket disconnect/dispose first — stops the socket, heartbeat, and message pump
        //    before the session it rides on is torn down.
        await _webSocketClient.DisposeAsync();

        // 2. Session teardown — the best-effort logout (frees the server-side session slot) followed
        //    by session disposal, both carried out by SessionManager.DisposeAsync, which decrements
        //    the active-session gauge exactly once via its own guard.
        await _sessionManager.DisposeAsync();

        GC.SuppressFinalize(this);
    }

    private async Task ValidateFlexTokenAsync(string queryId, CancellationToken cancellationToken)
    {
        // ERR-2: classify the validation outcome from the structured error under BOTH throw settings.
        // With ThrowOnApiError=false, ExecuteQueryAsync returns a failed Result; with =true it throws
        // IbkrApiException(error) via EnsureSuccess. Extract the error identically either way so the
        // 1012/1013/1015 token mapping and the transport-vs-token classification always apply — the
        // mapping is no longer bypassed when ThrowOnApiError is enabled.
        IbkrError error;
        try
        {
            var result = await Flex.ExecuteQueryAsync(queryId, cancellationToken);
            if (result.IsSuccess)
            {
                return;
            }

            error = result.Error;
        }
        catch (IbkrApiException ex)
        {
            error = ex.Error;
        }

        ClassifyFlexValidationError(error);
    }

    /// <summary>
    /// Classifies a Flex validation failure (ERR-2): a 1012/1013/1015 token error maps to an actionable
    /// <see cref="IbkrConfigurationException"/>; any other Flex query error is logged and treated as
    /// non-fatal (the token itself appears valid); and a non-Flex error is a transient TRANSPORT failure
    /// surfaced truthfully as <see cref="IbkrTransientException"/> — never recast as a token/config problem.
    /// </summary>
    private void ClassifyFlexValidationError(IbkrError error)
    {
        if (error is IbkrFlexError flexError)
        {
            if (flexError.ErrorCode is 1015)
            {
                throw new IbkrConfigurationException(
                    "Flex token is invalid — generate a new token in the IBKR portal (Reports → Flex Queries → Flex Web Configuration).",
                    "FlexToken");
            }

            if (flexError.ErrorCode is 1012)
            {
                throw new IbkrConfigurationException(
                    "Flex token has expired — generate a new token in the IBKR portal (Reports → Flex Queries → Flex Web Configuration).",
                    "FlexToken");
            }

            if (flexError.ErrorCode is 1013)
            {
                throw new IbkrConfigurationException(
                    "Flex token rejected due to IP restriction — check the allowed IP list in the IBKR portal (Reports → Flex Queries → Flex Web Configuration).",
                    "FlexToken");
            }

            LogFlexValidationQueryError(flexError.ErrorCode, flexError.Message ?? "(unknown)");
            return;
        }

        // ERR-2: a non-Flex error is a transport-level failure reaching the Flex Web Service — classify it
        // truthfully as transient (wait-and-retry), NOT a FlexToken configuration problem.
        throw new IbkrTransientException(
            "Could not reach the Flex Web Service to validate the token — this is a transient transport " +
            $"failure, not a token problem. Retry. Error: {error.Message}");
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Flex validation skipped — Flex token is configured but no query IDs set in FlexQueries")]
    private partial void LogFlexValidationSkipped();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Flex validation query returned error {ErrorCode}: {ErrorMessage} — token appears valid but query failed")]
    private partial void LogFlexValidationQueryError(int errorCode, string errorMessage);
}
