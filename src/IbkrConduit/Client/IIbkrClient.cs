namespace IbkrConduit.Client;

/// <summary>
/// Unified client for the IBKR API. The single entry point for consumers.
/// </summary>
public interface IIbkrClient : IAsyncDisposable
{
    /// <summary>
    /// Portfolio operations (account listing).
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
}
