using IbkrConduit.Session;

namespace IbkrConduit.Client;

/// <summary>
/// Default implementation of <see cref="IIbkrClient"/> that delegates to
/// typed operations interfaces and manages session lifecycle.
/// </summary>
public class IbkrClient : IIbkrClient
{
    private readonly ISessionManager _sessionManager;

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
    /// <param name="sessionManager">The session manager for lifecycle management.</param>
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
        ISessionManager sessionManager)
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
        _sessionManager = sessionManager;
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
    public async ValueTask DisposeAsync()
    {
        await _sessionManager.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
