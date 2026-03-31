using IbkrConduit.Contracts;

namespace IbkrConduit.Client;

/// <summary>
/// Contract lookup operations on the IBKR API.
/// </summary>
public interface IContractOperations
{
    /// <summary>
    /// Searches for contracts matching the given symbol.
    /// </summary>
    Task<List<ContractSearchResult>> SearchBySymbolAsync(
        string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves detailed contract information by contract ID.
    /// </summary>
    Task<ContractDetails> GetContractDetailsAsync(
        string conid, CancellationToken cancellationToken = default);
}
