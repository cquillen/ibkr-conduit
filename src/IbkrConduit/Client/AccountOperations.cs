using IbkrConduit.Accounts;
using IbkrConduit.Diagnostics;
using IbkrConduit.Errors;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Client;

/// <summary>
/// Account operations that delegate to the underlying Refit API.
/// </summary>
internal partial class AccountOperations : IAccountOperations
{
    private readonly IIbkrAccountApi _api;
    private readonly IbkrClientOptions _options;
    private readonly ILogger<AccountOperations> _logger;
    private readonly ResultFactory _resultFactory;

    /// <summary>
    /// Creates a new <see cref="AccountOperations"/> instance.
    /// </summary>
    /// <param name="api">The Refit account API client.</param>
    /// <param name="options">Client options.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="resultFactory">Factory for converting API responses to results.</param>
    public AccountOperations(IIbkrAccountApi api, IbkrClientOptions options, ILogger<AccountOperations> logger, ResultFactory resultFactory)
    {
        _api = api;
        _options = options;
        _logger = logger;
        _resultFactory = resultFactory;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "{Operation} completed with status {StatusCode}")]
    private static partial void LogOperationCompleted(ILogger logger, string operation, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Operation} failed: {ErrorType} (status {StatusCode})")]
    private static partial void LogOperationFailed(ILogger logger, string operation, string errorType, int? statusCode);

    /// <inheritdoc />
    public async Task<Result<IserverAccountsResponse>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.GetAccounts");
        var response = await _api.GetAccountsAsync(cancellationToken);
        var result = _resultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetAccounts");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<SwitchAccountResponse>> SwitchAccountAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.SwitchAccount");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.SwitchAccountAsync(new SwitchAccountRequest(accountId), cancellationToken);
        var result = _resultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "SwitchAccount");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<SignaturesAndOwnersResponse>> GetSignaturesAndOwnersAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.GetSignaturesAndOwners");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.GetSignaturesAndOwnersAsync(accountId, cancellationToken);
        var result = _resultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetSignaturesAndOwners");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<DynamicAccountSearchResponse>> SearchDynamicAccountAsync(string pattern,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.SearchDynamicAccount");
        var response = await _api.SearchDynamicAccountAsync(pattern, cancellationToken);
        var result = _resultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "SearchDynamicAccount");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<SetDynamicAccountResponse>> SetDynamicAccountAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.SetDynamicAccount");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.SetDynamicAccountAsync(new SetDynamicAccountRequest(accountId), cancellationToken);
        var result = _resultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "SetDynamicAccount");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<AccountSummaryOverview>> GetAccountSummaryAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.GetAccountSummary");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.GetAccountSummaryAsync(accountId, cancellationToken);
        var result = _resultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetAccountSummary");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<Dictionary<string, Dictionary<string, string>>>> GetAccountSummaryAvailableFundsAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.GetAccountSummaryAvailableFunds");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.GetAccountSummaryAvailableFundsAsync(accountId, cancellationToken);
        var result = _resultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetAccountSummaryAvailableFunds");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<Dictionary<string, Dictionary<string, string>>>> GetAccountSummaryBalancesAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.GetAccountSummaryBalances");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.GetAccountSummaryBalancesAsync(accountId, cancellationToken);
        var result = _resultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetAccountSummaryBalances");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<Dictionary<string, Dictionary<string, string>>>> GetAccountSummaryMarginsAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.GetAccountSummaryMargins");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.GetAccountSummaryMarginsAsync(accountId, cancellationToken);
        var result = _resultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetAccountSummaryMargins");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<Dictionary<string, Dictionary<string, string>>>> GetAccountSummaryMarketValueAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.GetAccountSummaryMarketValue");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.GetAccountSummaryMarketValueAsync(accountId, cancellationToken);
        var result = _resultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetAccountSummaryMarketValue");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
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
