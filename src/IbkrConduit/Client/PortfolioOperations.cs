using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Serialization;
using IbkrConduit.Diagnostics;
using IbkrConduit.Errors;
using IbkrConduit.Portfolio;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Client;

/// <summary>
/// Portfolio operations that delegate to the underlying Refit API.
/// </summary>
internal partial class PortfolioOperations : IPortfolioOperations
{
    private readonly IIbkrPortfolioApi _api;
    private readonly IbkrClientOptions _options;
    private readonly ILogger<PortfolioOperations> _logger;

    /// <summary>
    /// Creates a new <see cref="PortfolioOperations"/> instance.
    /// </summary>
    /// <param name="api">The Refit portfolio API client.</param>
    /// <param name="options">Client options.</param>
    /// <param name="logger">Logger instance.</param>
    public PortfolioOperations(IIbkrPortfolioApi api, IbkrClientOptions options, ILogger<PortfolioOperations> logger)
    {
        _api = api;
        _options = options;
        _logger = logger;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "{Operation} completed with status {StatusCode}")]
    private static partial void LogOperationCompleted(ILogger logger, string operation, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Operation} failed: {ErrorType} (status {StatusCode})")]
    private static partial void LogOperationFailed(ILogger logger, string operation, string errorType, int? statusCode);

    /// <inheritdoc />
    public async Task<Result<List<Account>>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetAccounts");
        var response = await _api.GetAccountsAsync(cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetAccounts");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<List<Position>>> GetPositionsAsync(string accountId, int page = 0,
        bool? waitForSecDef = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetPositions");
        activity?.SetTag(LogFields.AccountId, accountId);
        activity?.SetTag("page", page);
        var response = await _api.GetPositionsAsync(accountId, page, waitForSecDef: waitForSecDef, cancellationToken: cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetPositions");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<Dictionary<string, AccountSummaryEntry>>> GetAccountSummaryAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetAccountSummary");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.GetAccountSummaryAsync(accountId, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetAccountSummary");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<Dictionary<string, LedgerEntry>>> GetLedgerAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetLedger");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.GetLedgerAsync(accountId, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetLedger");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<AccountInfo>> GetAccountInfoAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetAccountInfo");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.GetAccountInfoAsync(accountId, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetAccountInfo");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<AccountAllocation>> GetAccountAllocationAsync(string accountId,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetAllocation");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.GetAccountAllocationAsync(accountId, model, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetAccountAllocation");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<List<Position>>> GetPositionByConidAsync(string accountId, string conid,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetPositionByConid");
        activity?.SetTag(LogFields.AccountId, accountId);
        activity?.SetTag(LogFields.Conid, conid);
        var response = await _api.GetPositionByConidAsync(accountId, conid, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetPositionByConid");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<PositionContractInfo>> GetPositionAndContractInfoAsync(string conid,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetPositionContractInfo");
        activity?.SetTag(LogFields.Conid, conid);
        var response = await _api.GetPositionAndContractInfoAsync(conid, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetPositionAndContractInfo");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<bool>> InvalidatePortfolioCacheAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.InvalidateCache");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.InvalidatePortfolioCacheAsync(accountId, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var result = Result<bool>.Success(true);
            LogResult(result, "InvalidatePortfolioCache");
            return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
        }

        var rawBody = response.Error?.Content ?? "";
        var error = new IbkrApiError(response.StatusCode, rawBody, rawBody, response.RequestMessage?.RequestUri?.AbsolutePath);
        var failResult = Result<bool>.Failure(error);
        LogResult(failResult, "InvalidatePortfolioCache");
        return _options.ThrowOnApiError ? failResult.EnsureSuccess() : failResult;
    }

    /// <inheritdoc />
    public async Task<Result<AccountPerformance>> GetAccountPerformanceAsync(List<string> accountIds, PerformancePeriod period,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetPerformance");
        var periodStr = GetEnumMemberValue(period);
        activity?.SetTag("period", periodStr);
        var response = await _api.GetAccountPerformanceAsync(new PerformanceRequest(accountIds, periodStr), cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetAccountPerformance");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<TransactionHistory>> GetTransactionHistoryAsync(List<string> accountIds,
        List<string> conids, string currency, int? days = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetTransactionHistory");
        activity?.SetTag("currency", currency);
        activity?.SetTag("days", days);
        var response = await _api.GetTransactionHistoryAsync(
            new TransactionHistoryRequest(accountIds, conids, currency, days), cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetTransactionHistory");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<AccountAllocation>> GetConsolidatedAllocationAsync(List<string> accountIds,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetConsolidatedAllocation");
        var response = await _api.GetConsolidatedAllocationAsync(
            new ConsolidatedAllocationRequest(accountIds), cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetConsolidatedAllocation");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<List<ComboPosition>>> GetComboPositionsAsync(string accountId, bool? nocache = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetComboPositions");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.GetComboPositionsAsync(accountId, nocache, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetComboPositions");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<List<Position>>> GetRealTimePositionsAsync(string accountId,
        string? model = null, string? sort = null, SortDirection? direction = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetRealTimePositions");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.GetRealTimePositionsAsync(accountId, model, sort, direction, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetRealTimePositions");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<List<SubAccount>>> GetSubAccountsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetSubAccounts");
        var response = await _api.GetSubAccountsAsync(cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetSubAccounts");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<List<SubAccount>>> GetSubAccountsPagedAsync(int page = 0,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetSubAccountsPaged");
        activity?.SetTag("page", page);
        var response = await _api.GetSubAccountsPagedAsync(page, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetSubAccountsPaged");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<AllPeriodsPerformance>> GetAllPeriodsPerformanceAsync(List<string> accountIds,
        string? param = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetAllPeriodsPerformance");
        var response = await _api.GetAllPeriodsPerformanceAsync(
            new AllPeriodsRequest(accountIds), param, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetAllPeriodsPerformance");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<PartitionedPnl>> GetPartitionedPnlAsync(CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetPartitionedPnl");
        var response = await _api.GetPartitionedPnlAsync(cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetPartitionedPnl");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    private static string GetEnumMemberValue<T>(T value)
        where T : Enum
    {
        var member = typeof(T).GetMember(value.ToString())[0];
        var attr = member.GetCustomAttribute<EnumMemberAttribute>();
        return attr?.Value ?? value.ToString();
    }

    private void LogResult<T>(Result<T> result, string operation)
    {
        if (result.IsSuccess)
        {
            LogOperationCompleted(_logger, operation, 200);
        }
        else
        {
            LogOperationFailed(_logger, operation, result.Error.GetType().Name, (int?)result.Error.StatusCode);
        }
    }
}
