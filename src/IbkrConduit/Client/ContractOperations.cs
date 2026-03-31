using IbkrConduit.Contracts;

namespace IbkrConduit.Client;

/// <summary>
/// Contract lookup operations that delegate to the underlying Refit API.
/// </summary>
public class ContractOperations : IContractOperations
{
    private readonly IIbkrContractApi _api;

    /// <summary>
    /// Creates a new <see cref="ContractOperations"/> instance.
    /// </summary>
    /// <param name="api">The Refit contract API client.</param>
    public ContractOperations(IIbkrContractApi api) => _api = api;

    /// <inheritdoc />
    public Task<List<ContractSearchResult>> SearchBySymbolAsync(
        string symbol, CancellationToken cancellationToken = default) =>
        _api.SearchBySymbolAsync(symbol, cancellationToken);

    /// <inheritdoc />
    public Task<ContractDetails> GetContractDetailsAsync(
        string conid, CancellationToken cancellationToken = default) =>
        _api.GetContractDetailsAsync(conid, cancellationToken);
}
