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

}
