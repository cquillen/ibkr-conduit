using IbkrConduit.Portfolio;

namespace IbkrConduit.Client;

/// <summary>
/// Portfolio operations on the IBKR API.
/// </summary>
public interface IPortfolioOperations
{
    /// <summary>
    /// Retrieves the list of accounts for the authenticated user.
    /// </summary>
    Task<List<Account>> GetAccountsAsync(CancellationToken cancellationToken = default);
}
