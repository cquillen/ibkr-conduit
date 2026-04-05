using Refit;

namespace IbkrConduit.Portfolio;

/// <summary>
/// Refit interface for IBKR portfolio endpoints.
/// </summary>
internal interface IIbkrPortfolioApi
{
    /// <summary>
    /// Retrieves the list of accounts for the authenticated user.
    /// </summary>
    [Get("/v1/api/portfolio/accounts")]
    Task<IApiResponse<List<Account>>> GetAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves positions for the specified account and page.
    /// </summary>
    [Get("/v1/api/portfolio/{accountId}/positions/{page}")]
    Task<IApiResponse<List<Position>>> GetPositionsAsync(
        string accountId, int page = 0,
        [Query] string? model = null, [Query] string? sort = null,
        [Query] string? direction = null, [Query] string? period = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the account summary with key-value entries.
    /// </summary>
    [Get("/v1/api/portfolio/{accountId}/summary")]
    Task<IApiResponse<Dictionary<string, AccountSummaryEntry>>> GetAccountSummaryAsync(
        string accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the ledger (cash balances by currency) for the specified account.
    /// </summary>
    [Get("/v1/api/portfolio/{accountId}/ledger")]
    Task<IApiResponse<Dictionary<string, LedgerEntry>>> GetLedgerAsync(
        string accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves account metadata/info for the specified account.
    /// </summary>
    [Get("/v1/api/portfolio/{accountId}/meta")]
    Task<IApiResponse<AccountInfo>> GetAccountInfoAsync(
        string accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves allocation information for the specified account.
    /// </summary>
    [Get("/v1/api/portfolio/{accountId}/allocation")]
    Task<IApiResponse<AccountAllocation>> GetAccountAllocationAsync(
        string accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves position information for a specific contract in the specified account.
    /// </summary>
    [Get("/v1/api/portfolio/{accountId}/position/{conid}")]
    Task<IApiResponse<List<Position>>> GetPositionByConidAsync(
        string accountId, string conid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves position and contract info for a specific contract across all accounts.
    /// </summary>
    [Get("/v1/api/portfolio/positions/{conid}")]
    Task<IApiResponse<PositionContractInfo>> GetPositionAndContractInfoAsync(
        string conid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the server-side portfolio cache for the specified account.
    /// </summary>
    [Post("/v1/api/portfolio/{accountId}/positions/invalidate")]
    Task<IApiResponse<string>> InvalidatePortfolioCacheAsync(
        string accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves account performance data for the specified accounts and period.
    /// </summary>
    [Post("/v1/api/pa/performance")]
    Task<IApiResponse<AccountPerformance>> GetAccountPerformanceAsync(
        [Body] PerformanceRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves transaction history for the specified accounts and contracts.
    /// </summary>
    [Post("/v1/api/pa/transactions")]
    Task<IApiResponse<TransactionHistory>> GetTransactionHistoryAsync(
        [Body] TransactionHistoryRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves consolidated allocation across multiple accounts.
    /// </summary>
    [Post("/v1/api/portfolio/allocation")]
    Task<IApiResponse<AccountAllocation>> GetConsolidatedAllocationAsync(
        [Body] ConsolidatedAllocationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves combination (spread) positions for the specified account.
    /// </summary>
    [Get("/v1/api/portfolio/{accountId}/combo/positions")]
    Task<IApiResponse<List<ComboPosition>>> GetComboPositionsAsync(
        string accountId, [Query] bool? nocache = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves real-time positions (bypasses server cache) for the specified account.
    /// </summary>
    [Get("/v1/api/portfolio2/{accountId}/positions")]
    Task<IApiResponse<List<Position>>> GetRealTimePositionsAsync(
        string accountId, [Query] string? model = null,
        [Query] string? sort = null, [Query] string? direction = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves sub-accounts for FA/IBroker users.
    /// </summary>
    [Get("/v1/api/portfolio/subaccounts")]
    Task<IApiResponse<List<SubAccount>>> GetSubAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves sub-accounts (paginated) for FA/IBroker users.
    /// </summary>
    [Get("/v1/api/portfolio/subaccounts2")]
    Task<IApiResponse<List<SubAccount>>> GetSubAccountsPagedAsync(
        [Query] int page = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves performance data across all available time periods.
    /// </summary>
    [Post("/v1/api/pa/allperiods")]
    Task<IApiResponse<AllPeriodsPerformance>> GetAllPeriodsPerformanceAsync(
        [Body] AllPeriodsRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves P&amp;L partitioned by account and model.
    /// </summary>
    [Get("/v1/api/iserver/account/pnl/partitioned")]
    Task<IApiResponse<PartitionedPnl>> GetPartitionedPnlAsync(CancellationToken cancellationToken = default);
}
