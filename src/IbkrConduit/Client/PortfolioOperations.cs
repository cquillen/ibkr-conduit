using IbkrConduit.Portfolio;

namespace IbkrConduit.Client;

/// <summary>
/// Portfolio operations that delegate to the underlying Refit API.
/// </summary>
public class PortfolioOperations : IPortfolioOperations
{
    private readonly IIbkrPortfolioApi _api;

    /// <summary>
    /// Creates a new <see cref="PortfolioOperations"/> instance.
    /// </summary>
    /// <param name="api">The Refit portfolio API client.</param>
    public PortfolioOperations(IIbkrPortfolioApi api) => _api = api;

    /// <inheritdoc />
    public Task<List<Account>> GetAccountsAsync(CancellationToken cancellationToken = default) =>
        _api.GetAccountsAsync();
}
