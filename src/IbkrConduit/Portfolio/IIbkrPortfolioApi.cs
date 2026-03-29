using Refit;

namespace IbkrConduit.Portfolio;

/// <summary>
/// Refit interface for IBKR portfolio endpoints.
/// </summary>
public interface IIbkrPortfolioApi
{
    /// <summary>
    /// Retrieves the list of accounts for the authenticated user.
    /// </summary>
    [Get("/v1/api/portfolio/accounts")]
    Task<List<Account>> GetAccountsAsync();
}
