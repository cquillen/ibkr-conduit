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

    /// <summary>
    /// Retrieves positions for the specified account and page.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="page">The page number (default 0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<List<Position>> GetPositionsAsync(string accountId, int page = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the account summary with key-value entries.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Dictionary<string, AccountSummaryEntry>> GetAccountSummaryAsync(string accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the ledger (cash balances by currency) for the specified account.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Dictionary<string, LedgerEntry>> GetLedgerAsync(string accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves account metadata/info for the specified account.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AccountInfo> GetAccountInfoAsync(string accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves allocation information for the specified account.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AccountAllocation> GetAccountAllocationAsync(string accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves position information for a specific contract in the specified account.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="conid">The contract identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<List<Position>> GetPositionByConidAsync(string accountId, string conid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves position and contract info for a specific contract across all accounts.
    /// </summary>
    /// <param name="conid">The contract identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PositionContractInfo> GetPositionAndContractInfoAsync(string conid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the server-side portfolio cache for the specified account.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidatePortfolioCacheAsync(string accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves account performance data for the specified accounts and period.
    /// </summary>
    /// <param name="accountIds">The account IDs to query.</param>
    /// <param name="period">The time period (e.g., "1D", "1W", "1M", "1Y").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AccountPerformance> GetAccountPerformanceAsync(List<string> accountIds, string period,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves transaction history for the specified accounts and contracts.
    /// </summary>
    /// <param name="accountIds">The account IDs to query.</param>
    /// <param name="conids">The contract IDs to filter by.</param>
    /// <param name="currency">The currency code.</param>
    /// <param name="days">Number of days of history to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TransactionHistory> GetTransactionHistoryAsync(List<string> accountIds,
        List<string> conids, string currency, int? days = null,
        CancellationToken cancellationToken = default);
}
