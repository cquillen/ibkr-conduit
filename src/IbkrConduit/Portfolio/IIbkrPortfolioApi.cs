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
    Task<List<Account>> GetAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves positions for the specified account and page.
    /// </summary>
    [Get("/v1/api/portfolio/{accountId}/positions/{page}")]
    Task<List<Position>> GetPositionsAsync(
        string accountId, int page = 0,
        [Query] string? model = null, [Query] string? sort = null,
        [Query] string? direction = null, [Query] string? period = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the account summary with key-value entries.
    /// </summary>
    [Get("/v1/api/portfolio/{accountId}/summary")]
    Task<Dictionary<string, AccountSummaryEntry>> GetAccountSummaryAsync(
        string accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the ledger (cash balances by currency) for the specified account.
    /// </summary>
    [Get("/v1/api/portfolio/{accountId}/ledger")]
    Task<Dictionary<string, LedgerEntry>> GetLedgerAsync(
        string accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves account metadata/info for the specified account.
    /// </summary>
    [Get("/v1/api/portfolio/{accountId}/meta")]
    Task<AccountInfo> GetAccountInfoAsync(
        string accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves allocation information for the specified account.
    /// </summary>
    [Get("/v1/api/portfolio/{accountId}/allocation")]
    Task<AccountAllocation> GetAccountAllocationAsync(
        string accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves position information for a specific contract in the specified account.
    /// </summary>
    [Get("/v1/api/portfolio/{accountId}/position/{conid}")]
    Task<List<Position>> GetPositionByConidAsync(
        string accountId, string conid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves position and contract info for a specific contract across all accounts.
    /// </summary>
    [Get("/v1/api/portfolio/positions/{conid}")]
    Task<PositionContractInfo> GetPositionAndContractInfoAsync(
        string conid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the server-side portfolio cache for the specified account.
    /// </summary>
    [Post("/v1/api/portfolio/{accountId}/positions/invalidate")]
    Task InvalidatePortfolioCacheAsync(
        string accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves account performance data for the specified accounts and period.
    /// </summary>
    [Post("/v1/api/pa/performance")]
    Task<AccountPerformance> GetAccountPerformanceAsync(
        [Body] PerformanceRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves transaction history for the specified accounts and contracts.
    /// </summary>
    [Post("/v1/api/pa/transactions")]
    Task<TransactionHistory> GetTransactionHistoryAsync(
        [Body] TransactionHistoryRequest request, CancellationToken cancellationToken = default);
}
