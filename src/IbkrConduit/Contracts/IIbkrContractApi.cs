using Refit;

namespace IbkrConduit.Contracts;

/// <summary>
/// Refit interface for IBKR contract lookup endpoints.
/// </summary>
public interface IIbkrContractApi
{
    /// <summary>
    /// Searches for contracts by symbol name.
    /// </summary>
    [Get("/v1/api/iserver/secdef/search")]
    Task<List<ContractSearchResult>> SearchBySymbolAsync([Query] string symbol);

    /// <summary>
    /// Retrieves detailed contract information by contract ID.
    /// </summary>
    [Get("/v1/api/iserver/contract/{conid}/info")]
    Task<ContractDetails> GetContractDetailsAsync(string conid);
}
