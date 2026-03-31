using IbkrConduit.Portfolio;

namespace IbkrConduit.Client;

/// <summary>
/// Portfolio operations that delegate to the underlying Refit API.
/// </summary>
public class PortfolioOperations : IPortfolioOperations
{
    private readonly IIbkrPortfolioApi _api;

    /// <summary>
    /// Creates a new <see cref="PortfolioOperations"/> instance.
    /// </summary>
    /// <param name="api">The Refit portfolio API client.</param>
    public PortfolioOperations(IIbkrPortfolioApi api) => _api = api;

    /// <inheritdoc />
    public Task<List<Account>> GetAccountsAsync(CancellationToken cancellationToken = default) =>
        _api.GetAccountsAsync(cancellationToken);

    /// <inheritdoc />
    public Task<List<Position>> GetPositionsAsync(string accountId, int page = 0,
        CancellationToken cancellationToken = default) =>
        _api.GetPositionsAsync(accountId, page, cancellationToken: cancellationToken);

    /// <inheritdoc />
    public Task<Dictionary<string, AccountSummaryEntry>> GetAccountSummaryAsync(string accountId,
        CancellationToken cancellationToken = default) =>
        _api.GetAccountSummaryAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public Task<Dictionary<string, LedgerEntry>> GetLedgerAsync(string accountId,
        CancellationToken cancellationToken = default) =>
        _api.GetLedgerAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public Task<AccountInfo> GetAccountInfoAsync(string accountId,
        CancellationToken cancellationToken = default) =>
        _api.GetAccountInfoAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public Task<AccountAllocation> GetAccountAllocationAsync(string accountId,
        CancellationToken cancellationToken = default) =>
        _api.GetAccountAllocationAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public Task<List<Position>> GetPositionByConidAsync(string accountId, string conid,
        CancellationToken cancellationToken = default) =>
        _api.GetPositionByConidAsync(accountId, conid, cancellationToken);

    /// <inheritdoc />
    public Task<PositionContractInfo> GetPositionAndContractInfoAsync(string conid,
        CancellationToken cancellationToken = default) =>
        _api.GetPositionAndContractInfoAsync(conid, cancellationToken);

    /// <inheritdoc />
    public Task InvalidatePortfolioCacheAsync(string accountId,
        CancellationToken cancellationToken = default) =>
        _api.InvalidatePortfolioCacheAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public Task<AccountPerformance> GetAccountPerformanceAsync(List<string> accountIds, string period,
        CancellationToken cancellationToken = default) =>
        _api.GetAccountPerformanceAsync(new PerformanceRequest(accountIds, period), cancellationToken);

    /// <inheritdoc />
    public Task<TransactionHistory> GetTransactionHistoryAsync(List<string> accountIds,
        List<string> conids, string currency, int? days = null,
        CancellationToken cancellationToken = default) =>
        _api.GetTransactionHistoryAsync(
            new TransactionHistoryRequest(accountIds, conids, currency, days), cancellationToken);
}
