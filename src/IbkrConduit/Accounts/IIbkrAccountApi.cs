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
    /// Retrieves signature and ownership information for an account.
    /// </summary>
    [Get("/v1/api/acesws/{accountId}/signatures-and-owners")]
    Task<SignaturesAndOwnersResponse> GetSignaturesAndOwnersAsync(
        string accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for accounts matching a pattern (DYNACCT feature required).
    /// </summary>
    [Get("/v1/api/iserver/account/search/{pattern}")]
    Task<DynamicAccountSearchResponse> SearchDynamicAccountAsync(
        string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the active dynamic account (DYNACCT feature required).
    /// </summary>
    [Post("/v1/api/iserver/dynaccount")]
    Task<SetDynamicAccountResponse> SetDynamicAccountAsync(
        [Body] SetDynamicAccountRequest request, CancellationToken cancellationToken = default);

}
