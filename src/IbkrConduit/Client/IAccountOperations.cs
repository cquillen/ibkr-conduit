using IbkrConduit.Accounts;

namespace IbkrConduit.Client;

/// <summary>
/// Account operations on the IBKR iserver API.
/// </summary>
public interface IAccountOperations
{
    /// <summary>
    /// Retrieves the list of brokerage accounts available for iserver endpoints.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IserverAccountsResponse> GetAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches the active brokerage account.
    /// </summary>
    /// <param name="accountId">The account identifier to switch to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<SwitchAccountResponse> SwitchAccountAsync(string accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the dynamic account for subsequent requests.
    /// </summary>
    /// <param name="accountId">The account identifier to set as dynamic.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<DynAccountResponse> SetDynAccountAsync(string accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for accounts matching the specified pattern.
    /// </summary>
    /// <param name="pattern">The search pattern.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<List<AccountSearchResult>> SearchAccountsAsync(string pattern,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves account information for the specified account.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IserverAccountInfo> GetAccountInfoAsync(string accountId,
        CancellationToken cancellationToken = default);
}
