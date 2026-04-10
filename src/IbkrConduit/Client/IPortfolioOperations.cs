using IbkrConduit.Errors;
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
    Task<Result<List<Account>>> GetAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves positions for the specified account and page.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="page">The page number (default 0).</param>
    /// <param name="waitForSecDef">When true, waits for security definition data before returning.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<List<Position>>> GetPositionsAsync(string accountId, int page = 0,
        bool? waitForSecDef = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the account summary with key-value entries.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<Dictionary<string, AccountSummaryEntry>>> GetAccountSummaryAsync(string accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the ledger (cash balances by currency) for the specified account.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<Dictionary<string, LedgerEntry>>> GetLedgerAsync(string accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves account metadata/info for the specified account.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<AccountInfo>> GetAccountInfoAsync(string accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves allocation information for the specified account.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="model">Optional model portfolio filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<AccountAllocation>> GetAccountAllocationAsync(string accountId,
        string? model = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves position information for a specific contract in the specified account.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="conid">The contract identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<List<Position>>> GetPositionByConidAsync(string accountId, string conid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves position and contract info for a specific contract across all accounts.
    /// </summary>
    /// <param name="conid">The contract identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<PositionContractInfo>> GetPositionAndContractInfoAsync(string conid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the server-side portfolio cache for the specified account.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<bool>> InvalidatePortfolioCacheAsync(string accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves account performance data for the specified accounts and period.
    /// </summary>
    /// <param name="accountIds">The account IDs to query.</param>
    /// <param name="period">The time period (e.g., "1D", "1W", "1M", "1Y").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<AccountPerformance>> GetAccountPerformanceAsync(List<string> accountIds, string period,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves transaction history for the specified accounts and contracts.
    /// </summary>
    /// <param name="accountIds">The account IDs to query.</param>
    /// <param name="conids">The contract IDs to filter by.</param>
    /// <param name="currency">The currency code.</param>
    /// <param name="days">Number of days of history to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<TransactionHistory>> GetTransactionHistoryAsync(List<string> accountIds,
        List<string> conids, string currency, int? days = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves consolidated allocation across multiple accounts.
    /// </summary>
    /// <param name="accountIds">The account IDs to consolidate allocation for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<AccountAllocation>> GetConsolidatedAllocationAsync(List<string> accountIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves combination (spread) positions for the specified account.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="nocache">Whether to bypass the server cache.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<List<ComboPosition>>> GetComboPositionsAsync(string accountId, bool? nocache = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves real-time positions (bypasses server cache) for the specified account.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="model">Optional model code.</param>
    /// <param name="sort">Optional sort column.</param>
    /// <param name="direction">Optional sort direction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<List<Position>>> GetRealTimePositionsAsync(string accountId,
        string? model = null, string? sort = null, SortDirection? direction = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves sub-accounts for FA/IBroker users.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<List<SubAccount>>> GetSubAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves sub-accounts (paginated) for FA/IBroker users.
    /// </summary>
    /// <param name="page">The page number (default 0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<List<SubAccount>>> GetSubAccountsPagedAsync(int page = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves performance data across all available time periods.
    /// </summary>
    /// <param name="accountIds">The account IDs to query.</param>
    /// <param name="param">Optional query parameter for filtering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<AllPeriodsPerformance>> GetAllPeriodsPerformanceAsync(List<string> accountIds,
        string? param = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves P&amp;L partitioned by account and model.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<PartitionedPnl>> GetPartitionedPnlAsync(CancellationToken cancellationToken = default);
}
