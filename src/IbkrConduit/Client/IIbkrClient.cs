using IbkrConduit.Health;

namespace IbkrConduit.Client;

/// <summary>
/// Unified client for the IBKR API. The single entry point for consumers.
/// </summary>
public interface IIbkrClient : IAsyncDisposable
{
    /// <summary>
    /// Portfolio operations (accounts, positions, summary, ledger, allocation, performance, transactions).
    /// </summary>
    IPortfolioOperations Portfolio { get; }

    /// <summary>
    /// Contract lookup operations (symbol search, contract details).
    /// </summary>
    IContractOperations Contracts { get; }

    /// <summary>
    /// Order management operations (place, cancel, query).
    /// </summary>
    IOrderOperations Orders { get; }

    /// <summary>
    /// Market data operations (snapshots with pre-flight handling, historical bars).
    /// </summary>
    IMarketDataOperations MarketData { get; }

    /// <summary>
    /// Real-time WebSocket streaming operations (market data, orders, P&amp;L, account summary, account ledger).
    /// </summary>
    IStreamingOperations Streaming { get; }

    /// <summary>
    /// Flex Web Service operations (query execution, trade confirmations, activity statements).
    /// </summary>
    IFlexOperations Flex { get; }

    /// <summary>
    /// Account operations (iserver account switching, search, info).
    /// </summary>
    IAccountOperations Accounts { get; }

    /// <summary>
    /// Alert (MTA) operations (create, list, get, delete).
    /// </summary>
    IAlertOperations Alerts { get; }

    /// <summary>
    /// Watchlist operations (create, list, get, delete).
    /// </summary>
    IWatchlistOperations Watchlists { get; }

    /// <summary>
    /// FYI notification operations (settings, delivery options, notifications).
    /// </summary>
    IFyiOperations Notifications { get; }

    /// <summary>
    /// Event contract (ForecastEx) operations (category tree, markets, rules, details, schedules).
    /// </summary>
    IEventContractOperations EventContracts { get; }

    /// <summary>
    /// Gets the current health status of the IBKR connection, aggregating signals
    /// from the brokerage session, WebSocket streaming, OAuth token, rate limiters,
    /// and API call tracking.
    /// </summary>
    /// <param name="activeProbe">When true, makes a live API call to check session status. Default: false.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IbkrHealthStatus> GetHealthStatusAsync(
        bool activeProbe = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the configured credentials can establish a session with the IBKR API.
    /// Optionally validates the Flex Web Service token by executing a query using the first
    /// configured query ID from <see cref="IbkrConduit.Session.IbkrClientOptions.FlexQueries"/>. Flex validation
    /// runs a real query, which takes a few seconds. Pass <paramref name="validateFlex"/> as
    /// <c>false</c> to skip Flex validation for faster startup.
    /// </summary>
    /// <param name="validateFlex">
    /// When <c>true</c> (default), validates the Flex token if one is configured and at least
    /// one query ID is set in <see cref="IbkrConduit.Session.IbkrClientOptions.FlexQueries"/>. When <c>false</c>,
    /// skips Flex validation regardless of configuration.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ValidateConnectionAsync(bool validateFlex = true, CancellationToken cancellationToken = default);
}
