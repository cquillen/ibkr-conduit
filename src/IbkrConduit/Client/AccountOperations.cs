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

}
