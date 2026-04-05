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
    /// Validates that the configured credentials can establish a session with the IBKR API.
    /// Performs LST acquisition, session initialization, and auth status verification.
    /// Call at startup for fail-fast credential validation.
    /// Throws <see cref="IbkrConduit.Errors.IbkrConfigurationException"/> with a descriptive message if validation fails.
    /// </summary>
    Task ValidateConnectionAsync(CancellationToken cancellationToken = default);
}
