using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using IbkrConduit.Diagnostics;
using IbkrConduit.Errors;
using IbkrConduit.Portfolio;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging;
using Refit;

namespace IbkrConduit.Client;

/// <summary>
/// Portfolio operations that delegate to the underlying Refit API.
/// </summary>
internal partial class PortfolioOperations : IPortfolioOperations
{
    private readonly IIbkrPortfolioApi _api;
    private readonly IbkrClientOptions _options;
    private readonly ILogger<PortfolioOperations> _logger;
    private readonly TenantContext _tenant;
    private readonly Dictionary<string, object> _logScope;

    /// <summary>
    /// Creates a new <see cref="PortfolioOperations"/> instance.
    /// </summary>
    /// <param name="api">The Refit portfolio API client.</param>
    /// <param name="options">Client options.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="tenant">Per-provider tenant identity used to tag telemetry.</param>
    public PortfolioOperations(IIbkrPortfolioApi api, IbkrClientOptions options, ILogger<PortfolioOperations> logger, TenantContext tenant)
    {
        _api = api;
        _options = options;
        _logger = logger;
        _tenant = tenant;
        _logScope = new Dictionary<string, object> { [LogFields.TenantId] = _tenant.TenantId };
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "{Operation} completed with status {StatusCode}")]
    private static partial void LogOperationCompleted(ILogger logger, string operation, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Operation} failed: {ErrorType} (status {StatusCode})")]
    private static partial void LogOperationFailed(ILogger logger, string operation, string errorType, int? statusCode);

    /// <inheritdoc />
    public async Task<Result<List<Account>>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetAccounts");
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
        using var _ = _logger.BeginScope(_logScope);
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
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
        activity?.SetTag(LogFields.AccountId, accountId);
        activity?.SetTag("page", page);
        var response = await _api.GetPositionsAsync(accountId, page, waitForSecDef: waitForSecDef, cancellationToken: cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);

        // ADR-0009 §10.7 (RPD-06): Positions carries no wire-reported freshness signal (unlike
        // LiveOrders' snapshot:bool), so a heuristically-sparse first read of a session (a non-empty
        // list with a row missing name/ticker) gets one transparent, immediate retry. Capped at one
        // attempt — never a loop — and recorded on this span only when it actually fires.
        if (result.IsSuccess && LooksSparse(result.Value))
        {
            activity?.SetTag("ibkr.cold_read_retry", true);
            try
            {
                var retryResponse = await _api.GetPositionsAsync(accountId, page, waitForSecDef: waitForSecDef, cancellationToken: cancellationToken);
                var retryResult = ResultFactory.FromResponse(retryResponse, retryResponse.RequestMessage?.RequestUri?.AbsolutePath);

                // ADR-0009 Decision point 4: a false-positive retry must never corrupt data or change
                // the result the consumer ultimately sees. Only adopt the retry's outcome when the
                // retry itself succeeded — a transient retry failure (500/503/timeout) must not discard
                // the good first read.
                if (retryResult.IsSuccess)
                {
                    result = retryResult;
                }
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // ADR-0009 Decision point 4a: a retry transport failure (connection fault, timeout,
                // etc. — thrown rather than captured as a Result.Failure, e.g. via
                // ResultFactory.FromResponse's ThrowOnSendFailure) must not discard the good first
                // read either, same rationale as the HTTP-status failure case above. Caller-requested
                // cancellation is excluded via the filter and propagates normally.
            }
        }

        LogResult(result, "GetPositions");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <summary>
    /// ADR-0009's "looks sparse" heuristic for a positions read: a non-empty list is sparse when any
    /// row is missing <see cref="Position.Name"/> or <see cref="Position.Ticker"/> — the fields IBKR
    /// omits on the first positions read of a fresh session (2026-07-14 live probe,
    /// <c>recordings/coldread-rpd06/</c>). An empty list is never itself evidence of sparseness — a
    /// genuinely zero-position account must not trigger a retry.
    /// </summary>
    internal static bool LooksSparse(List<Position> positions) =>
        positions.Count > 0 && positions.Any(p => string.IsNullOrEmpty(p.Name) || string.IsNullOrEmpty(p.Ticker));

    /// <inheritdoc />
    public async Task<Result<Dictionary<string, AccountSummaryEntry>>> GetAccountSummaryAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetAccountSummary");
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
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
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
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
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
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
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
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
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
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
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
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
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.InvalidatePortfolioCacheAsync(accountId, cancellationToken);
        response.ThrowOnSendFailure();
        if (response.IsSuccessStatusCode)
        {
            var result = Result<bool>.Success(true);
            LogResult(result, "InvalidatePortfolioCache");
            return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
        }

        var rawBody = (response.Error as ApiException)?.Content ?? "";
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
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
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
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
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
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
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
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
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
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
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
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
        var response = await _api.GetSubAccountsAsync(cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetSubAccounts");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<SubAccountsPage>> GetSubAccountsPagedAsync(int page = 0,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Portfolio.GetSubAccountsPaged");
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
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
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
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
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
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
