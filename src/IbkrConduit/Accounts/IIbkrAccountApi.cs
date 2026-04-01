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

    /// <summary>
    /// Sets the dynamic account for subsequent requests.
    /// </summary>
    [Post("/v1/api/iserver/dynaccount")]
    Task<DynAccountResponse> SetDynAccountAsync(
        [Body] DynAccountRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for accounts matching the specified pattern.
    /// </summary>
    [Get("/v1/api/iserver/account/search/{pattern}")]
    Task<List<AccountSearchResult>> SearchAccountsAsync(
        string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves account information for the specified account.
    /// </summary>
    [Get("/v1/api/iserver/account/{accountId}")]
    Task<IserverAccountInfo> GetAccountInfoAsync(
        string accountId, CancellationToken cancellationToken = default);
}
