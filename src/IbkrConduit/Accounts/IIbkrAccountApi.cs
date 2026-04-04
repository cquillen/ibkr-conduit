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

    /// <summary>
    /// Retrieves a high-level account summary overview with balances, margins, and buying power.
    /// </summary>
    [Get("/v1/api/iserver/account/{accountId}/summary")]
    Task<AccountSummaryOverview> GetAccountSummaryAsync(
        string accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves available funds broken down by segment (total, securities, commodities, crypto).
    /// </summary>
    [Get("/v1/api/iserver/account/{accountId}/summary/available_funds")]
    Task<Dictionary<string, Dictionary<string, string>>> GetAccountSummaryAvailableFundsAsync(
        string accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves balance information broken down by segment (total, securities, commodities, crypto).
    /// </summary>
    [Get("/v1/api/iserver/account/{accountId}/summary/balances")]
    Task<Dictionary<string, Dictionary<string, string>>> GetAccountSummaryBalancesAsync(
        string accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves margin information broken down by segment (total, securities, commodities, crypto).
    /// </summary>
    [Get("/v1/api/iserver/account/{accountId}/summary/margins")]
    Task<Dictionary<string, Dictionary<string, string>>> GetAccountSummaryMarginsAsync(
        string accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves market value information broken down by currency.
    /// </summary>
    [Get("/v1/api/iserver/account/{accountId}/summary/market_value")]
    Task<Dictionary<string, Dictionary<string, string>>> GetAccountSummaryMarketValueAsync(
        string accountId, CancellationToken cancellationToken = default);

}
