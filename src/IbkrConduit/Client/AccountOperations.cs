using IbkrConduit.Accounts;
using IbkrConduit.Diagnostics;
using IbkrConduit.Errors;
using IbkrConduit.Session;

namespace IbkrConduit.Client;

/// <summary>
/// Account operations that delegate to the underlying Refit API.
/// </summary>
public class AccountOperations : IAccountOperations
{
    private readonly IIbkrAccountApi _api;
    private readonly IbkrClientOptions _options;

    /// <summary>
    /// Creates a new <see cref="AccountOperations"/> instance.
    /// </summary>
    /// <param name="api">The Refit account API client.</param>
    /// <param name="options">Client options.</param>
    public AccountOperations(IIbkrAccountApi api, IbkrClientOptions options)
    {
        _api = api;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<Result<IserverAccountsResponse>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.GetAccounts");
        var response = await _api.GetAccountsAsync(cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<SwitchAccountResponse>> SwitchAccountAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.SwitchAccount");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.SwitchAccountAsync(new SwitchAccountRequest(accountId), cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<SignaturesAndOwnersResponse>> GetSignaturesAndOwnersAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.GetSignaturesAndOwners");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.GetSignaturesAndOwnersAsync(accountId, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<DynamicAccountSearchResponse>> SearchDynamicAccountAsync(string pattern,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.SearchDynamicAccount");
        var response = await _api.SearchDynamicAccountAsync(pattern, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<SetDynamicAccountResponse>> SetDynamicAccountAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.SetDynamicAccount");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.SetDynamicAccountAsync(new SetDynamicAccountRequest(accountId), cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<AccountSummaryOverview>> GetAccountSummaryAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.GetAccountSummary");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.GetAccountSummaryAsync(accountId, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<Dictionary<string, Dictionary<string, string>>>> GetAccountSummaryAvailableFundsAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.GetAccountSummaryAvailableFunds");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.GetAccountSummaryAvailableFundsAsync(accountId, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<Dictionary<string, Dictionary<string, string>>>> GetAccountSummaryBalancesAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.GetAccountSummaryBalances");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.GetAccountSummaryBalancesAsync(accountId, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<Dictionary<string, Dictionary<string, string>>>> GetAccountSummaryMarginsAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.GetAccountSummaryMargins");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.GetAccountSummaryMarginsAsync(accountId, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<Dictionary<string, Dictionary<string, string>>>> GetAccountSummaryMarketValueAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.GetAccountSummaryMarketValue");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.GetAccountSummaryMarketValueAsync(accountId, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

}
