using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using IbkrConduit.Diagnostics;
using IbkrConduit.Portfolio;

namespace IbkrConduit.Client;

/// <summary>
/// Portfolio operations that delegate to the underlying Refit API.
/// </summary>
[ExcludeFromCodeCoverage]
public class PortfolioOperations : IPortfolioOperations
{
    private readonly IIbkrPortfolioApi _api;

    /// <summary>
    /// Creates a new <see cref="PortfolioOperations"/> instance.
    /// </summary>
    /// <param name="api">The Refit portfolio API client.</param>
    public PortfolioOperations(IIbkrPortfolioApi api) => _api = api;

    /// <inheritdoc />
    public async Task<List<Account>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetAccounts");
        return await _api.GetAccountsAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<Position>> GetPositionsAsync(string accountId, int page = 0,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetPositions");
        activity?.SetTag(LogFields.AccountId, accountId);
        activity?.SetTag("page", page);
        return await _api.GetPositionsAsync(accountId, page, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, AccountSummaryEntry>> GetAccountSummaryAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetAccountSummary");
        activity?.SetTag(LogFields.AccountId, accountId);
        return await _api.GetAccountSummaryAsync(accountId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, LedgerEntry>> GetLedgerAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetLedger");
        activity?.SetTag(LogFields.AccountId, accountId);
        return await _api.GetLedgerAsync(accountId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AccountInfo> GetAccountInfoAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetAccountInfo");
        activity?.SetTag(LogFields.AccountId, accountId);
        return await _api.GetAccountInfoAsync(accountId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AccountAllocation> GetAccountAllocationAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetAllocation");
        activity?.SetTag(LogFields.AccountId, accountId);
        return await _api.GetAccountAllocationAsync(accountId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<Position>> GetPositionByConidAsync(string accountId, string conid,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetPositionByConid");
        activity?.SetTag(LogFields.AccountId, accountId);
        activity?.SetTag(LogFields.Conid, conid);
        return await _api.GetPositionByConidAsync(accountId, conid, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PositionContractInfo> GetPositionAndContractInfoAsync(string conid,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetPositionContractInfo");
        activity?.SetTag(LogFields.Conid, conid);
        return await _api.GetPositionAndContractInfoAsync(conid, cancellationToken);
    }

    /// <inheritdoc />
    public async Task InvalidatePortfolioCacheAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.InvalidateCache");
        activity?.SetTag(LogFields.AccountId, accountId);
        await _api.InvalidatePortfolioCacheAsync(accountId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AccountPerformance> GetAccountPerformanceAsync(List<string> accountIds, string period,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetPerformance");
        activity?.SetTag("period", period);
        return await _api.GetAccountPerformanceAsync(new PerformanceRequest(accountIds, period), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TransactionHistory> GetTransactionHistoryAsync(List<string> accountIds,
        List<string> conids, string currency, int? days = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetTransactionHistory");
        activity?.SetTag("currency", currency);
        activity?.SetTag("days", days);
        return await _api.GetTransactionHistoryAsync(
            new TransactionHistoryRequest(accountIds, conids, currency, days), cancellationToken);
    }
}
