using Refit;

namespace IbkrConduit.Accounts;

/// <summary>
/// Refit interface for IBKR iserver account endpoints.
/// </summary>
public interface IIbkrAccountApi
{
    /// <summary>
    /// Retrieves the list of brokerage accounts available for iserver endpoints.
    /// </summary>
    [Get("/v1/api/iserver/accounts")]
    Task<IserverAccountsResponse> GetAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches the active brokerage account.
    /// </summary>
    [Post("/v1/api/iserver/account")]
    Task<SwitchAccountResponse> SwitchAccountAsync(
        [Body] SwitchAccountRequest request, CancellationToken cancellationToken = default);

}
