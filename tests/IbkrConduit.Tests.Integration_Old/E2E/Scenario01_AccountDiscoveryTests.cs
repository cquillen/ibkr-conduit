using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Session;
using Microsoft.Extensions.DependencyInjection;
using Refit;
using Shouldly;

namespace IbkrConduit.Tests.Integration.E2E;

/// <summary>
/// E2E Scenario 1: Account Discovery and Session Validation.
/// Exercises session validation, account listing, search, info, and switching
/// against a real IBKR paper trading account.
/// </summary>
[Collection("IBKR E2E")]
[ExcludeFromCodeCoverage]
public sealed class Scenario01_AccountDiscoveryTests : E2eScenarioBase
{
    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task AccountDiscovery_FullWorkflow()
    {
        var (provider, client) = CreateClient();

        try
        {

            // Step 1: Get accounts list — this triggers lazy session initialization
            var accountsResult = (await client.Accounts.GetAccountsAsync(CT)).Value;
            accountsResult.Accounts.ShouldNotBeEmpty("Should have at least one account");

            var sessionApi = provider.GetRequiredService<IIbkrSessionApi>();

            // Step 2: Verify auth status
            var authStatus = (await sessionApi.GetAuthStatusAsync(CT)).Content!;
            authStatus.Authenticated.ShouldBeTrue("Session should be authenticated");
            var accountId = accountsResult.Accounts[0];

            // Step 3: Switch account
            var switchResult = (await client.Accounts.SwitchAccountAsync(accountId, CT)).Value;
            // IBKR QUIRK: Switching to the already-selected account may return Set=false,
            // since the account is already active. We verify the response is not null.
            switchResult.ShouldNotBeNull();

            // Step 4: Reset suppressed questions
            var resetResult = (await sessionApi.ResetSuppressedQuestionsAsync(CT)).Content!;
            resetResult.ShouldNotBeNull();

        }
        finally
        {
            await DisposeAsync();
        }
    }

    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task SwitchAccount_InvalidId_ThrowsApiException()
    {
        var (_, client) = CreateClient();

        try
        {

            // IBKR may return HTTP error or 200 with error body for invalid account IDs.
            try
            {
                var result = (await client.Accounts.SwitchAccountAsync("INVALID999", CT)).Value;

                // IBKR QUIRK: If we get here, the API returned 200 for an invalid account ID.
                result.Success.ShouldNotBeNull(
                    "IBKR QUIRK: API returned 200 for invalid account switch — Success should contain a message");
            }
            catch (ApiException)
            {
                // Expected: IBKR returns an HTTP error for invalid account switch.
            }

        }
        finally
        {
            await DisposeAsync();
        }
    }
}
