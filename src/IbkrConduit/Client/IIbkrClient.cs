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
}
