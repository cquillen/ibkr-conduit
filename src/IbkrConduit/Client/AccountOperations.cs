using IbkrConduit.Accounts;
using IbkrConduit.Diagnostics;

namespace IbkrConduit.Client;

/// <summary>
/// Account operations that delegate to the underlying Refit API.
/// </summary>
public class AccountOperations : IAccountOperations
{
    private readonly IIbkrAccountApi _api;

    /// <summary>
    /// Creates a new <see cref="AccountOperations"/> instance.
    /// </summary>
    /// <param name="api">The Refit account API client.</param>
    public AccountOperations(IIbkrAccountApi api) => _api = api;

    /// <inheritdoc />
    public async Task<IserverAccountsResponse> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.GetAccounts");
        return await _api.GetAccountsAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SwitchAccountResponse> SwitchAccountAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.SwitchAccount");
        activity?.SetTag(LogFields.AccountId, accountId);
        return await _api.SwitchAccountAsync(new SwitchAccountRequest(accountId), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SignaturesAndOwnersResponse> GetSignaturesAndOwnersAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.GetSignaturesAndOwners");
        activity?.SetTag(LogFields.AccountId, accountId);
        return await _api.GetSignaturesAndOwnersAsync(accountId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DynamicAccountSearchResponse> SearchDynamicAccountAsync(string pattern,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.SearchDynamicAccount");
        return await _api.SearchDynamicAccountAsync(pattern, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SetDynamicAccountResponse> SetDynamicAccountAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.SetDynamicAccount");
        activity?.SetTag(LogFields.AccountId, accountId);
        return await _api.SetDynamicAccountAsync(new SetDynamicAccountRequest(accountId), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AccountSummaryOverview> GetAccountSummaryAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.GetAccountSummary");
        activity?.SetTag(LogFields.AccountId, accountId);
        return await _api.GetAccountSummaryAsync(accountId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, Dictionary<string, string>>> GetAccountSummaryAvailableFundsAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.GetAccountSummaryAvailableFunds");
        activity?.SetTag(LogFields.AccountId, accountId);
        return await _api.GetAccountSummaryAvailableFundsAsync(accountId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, Dictionary<string, string>>> GetAccountSummaryBalancesAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.GetAccountSummaryBalances");
        activity?.SetTag(LogFields.AccountId, accountId);
        return await _api.GetAccountSummaryBalancesAsync(accountId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, Dictionary<string, string>>> GetAccountSummaryMarginsAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.GetAccountSummaryMargins");
        activity?.SetTag(LogFields.AccountId, accountId);
        return await _api.GetAccountSummaryMarginsAsync(accountId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, Dictionary<string, string>>> GetAccountSummaryMarketValueAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.GetAccountSummaryMarketValue");
        activity?.SetTag(LogFields.AccountId, accountId);
        return await _api.GetAccountSummaryMarketValueAsync(accountId, cancellationToken);
    }

}
