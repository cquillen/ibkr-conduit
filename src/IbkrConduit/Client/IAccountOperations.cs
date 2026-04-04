using IbkrConduit.Accounts;
using IbkrConduit.Errors;

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
    Task<Result<IserverAccountsResponse>> GetAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches the active brokerage account.
    /// </summary>
    /// <param name="accountId">The account identifier to switch to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<SwitchAccountResponse>> SwitchAccountAsync(string accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves signature and ownership information for an account.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<SignaturesAndOwnersResponse>> GetSignaturesAndOwnersAsync(string accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for accounts matching a pattern (DYNACCT feature required).
    /// </summary>
    /// <param name="pattern">The account search pattern.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<DynamicAccountSearchResponse>> SearchDynamicAccountAsync(string pattern,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the active dynamic account (DYNACCT feature required).
    /// </summary>
    /// <param name="accountId">The account identifier to set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<SetDynamicAccountResponse>> SetDynamicAccountAsync(string accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a high-level account summary overview with balances, margins, and buying power.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<AccountSummaryOverview>> GetAccountSummaryAsync(string accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves available funds broken down by segment.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<Dictionary<string, Dictionary<string, string>>>> GetAccountSummaryAvailableFundsAsync(string accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves balance information broken down by segment.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<Dictionary<string, Dictionary<string, string>>>> GetAccountSummaryBalancesAsync(string accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves margin information broken down by segment.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<Dictionary<string, Dictionary<string, string>>>> GetAccountSummaryMarginsAsync(string accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves market value information broken down by currency.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<Dictionary<string, Dictionary<string, string>>>> GetAccountSummaryMarketValueAsync(string accountId,
        CancellationToken cancellationToken = default);

}
