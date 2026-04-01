using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using IbkrConduit.Contracts;
using IbkrConduit.Diagnostics;

namespace IbkrConduit.Client;

/// <summary>
/// Contract lookup operations that delegate to the underlying Refit API.
/// </summary>
[ExcludeFromCodeCoverage]
public class ContractOperations : IContractOperations
{
    private readonly IIbkrContractApi _api;

    /// <summary>
    /// Creates a new <see cref="ContractOperations"/> instance.
    /// </summary>
    /// <param name="api">The Refit contract API client.</param>
    public ContractOperations(IIbkrContractApi api) => _api = api;

    /// <inheritdoc />
    public async Task<List<ContractSearchResult>> SearchBySymbolAsync(
        string symbol, CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.SearchBySymbol");
        activity?.SetTag(LogFields.Symbol, symbol);
        return await _api.SearchBySymbolAsync(symbol, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ContractDetails> GetContractDetailsAsync(
        string conid, CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetDetails");
        activity?.SetTag(LogFields.Conid, conid);
        return await _api.GetContractDetailsAsync(conid, cancellationToken);
    }
}
