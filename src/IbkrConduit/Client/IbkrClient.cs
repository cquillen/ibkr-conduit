using IbkrConduit.Errors;
using IbkrConduit.Flex;
using IbkrConduit.Health;
using IbkrConduit.Session;
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
    private readonly IbkrClientOptions _options;
    private readonly ILogger<IbkrClient> _logger;

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
    public async ValueTask DisposeAsync()
    {
        await _sessionManager.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private async Task ValidateFlexTokenAsync(string queryId, CancellationToken cancellationToken)
    {
        var result = await Flex.ExecuteQueryAsync(queryId, cancellationToken);

        if (result.IsSuccess)
        {
            return;
        }

        if (result.Error is IbkrFlexError flexError)
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

        throw new IbkrConfigurationException(
            $"Could not reach the Flex Web Service — check network connectivity. Error: {result.Error.Message}",
            "FlexToken");
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Flex validation skipped — Flex token is configured but no query IDs set in FlexQueries")]
    private partial void LogFlexValidationSkipped();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Flex validation query returned error {ErrorCode}: {ErrorMessage} — token appears valid but query failed")]
    private partial void LogFlexValidationQueryError(int errorCode, string errorMessage);
}
