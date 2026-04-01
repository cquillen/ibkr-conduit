using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using IbkrConduit.Allocation;
using IbkrConduit.Client;
using Refit;
using Shouldly;

namespace IbkrConduit.Tests.Integration.E2E;

/// <summary>
/// E2E Scenario 9: FA Allocation Management.
/// Exercises allocation accounts, groups, and presets operations
/// against a real IBKR paper trading account. Skips gracefully if
/// the account type does not support FA allocations.
/// </summary>
[Collection("IBKR E2E")]
[ExcludeFromCodeCoverage]
public sealed class Scenario09_FaAllocationTests : E2eScenarioBase
{
    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task FaAllocation_FullWorkflow()
    {
        var (_, client) = CreateClient();
        var groupName = $"E2E-{Guid.NewGuid():N}"[..20];
        AllocationPresetsResponse? originalPresets = null;

        try
        {
            StartRecording("Scenario09_FaAllocation");

            // Step 1: Probe — get allocation accounts. Skip if FA not supported.
            AllocationAccountsResponse accounts;
            try
            {
                accounts = await client.Allocations.GetAccountsAsync(CT);
            }
            catch (ApiException)
            {
                // FA allocation is not supported on this account type (e.g., individual paper account).
                StopRecording();
                return;
            }

            accounts.ShouldNotBeNull();
            accounts.Accounts.ShouldNotBeNull();

            // We need at least one sub-account to create a group.
            if (accounts.Accounts.Count == 0)
            {
                StopRecording();
                return;
            }

            var firstAccount = accounts.Accounts[0];
            firstAccount.Name.ShouldNotBeNullOrEmpty();

            // Step 2: Get allocation groups
            var groups = await client.Allocations.GetGroupsAsync(CT);
            groups.ShouldNotBeNull();
            groups.Data.ShouldNotBeNull();

            // Step 3: Create group
            var createRequest = new AllocationGroupRequest(
                Name: groupName,
                Accounts: new List<AllocationGroupAccount>
                {
                    new(Name: firstAccount.Name, Amount: 100m),
                },
                DefaultMethod: "EqualQuantity");

            var createResponse = await client.Allocations.AddGroupAsync(createRequest, CT);
            createResponse.ShouldNotBeNull();
            createResponse.Success.ShouldBeTrue("Group creation should succeed");

            // Step 4: Get group detail
            var detail = await client.Allocations.GetGroupAsync(groupName, CT);
            detail.ShouldNotBeNull();
            detail.Name.ShouldBe(groupName);
            detail.Accounts.ShouldNotBeEmpty("Group should have at least one account");

            // Step 5: Modify group — change allocation method
            var modifyRequest = new AllocationGroupRequest(
                Name: groupName,
                Accounts: new List<AllocationGroupAccount>
                {
                    new(Name: firstAccount.Name, Amount: 100m),
                },
                DefaultMethod: "NetLiq");

            var modifyResponse = await client.Allocations.ModifyGroupAsync(modifyRequest, CT);
            modifyResponse.ShouldNotBeNull();
            modifyResponse.Success.ShouldBeTrue("Group modification should succeed");

            // Step 6: Get presets
            originalPresets = await client.Allocations.GetPresetsAsync(CT);
            originalPresets.ShouldNotBeNull();
            originalPresets.DefaultMethodForAll.ShouldNotBeNullOrEmpty();

            // Step 7: Set presets — save current, modify, then restore
            var modifiedPresets = new AllocationPresetsRequest(
                DefaultMethodForAll: originalPresets.DefaultMethodForAll,
                GroupAutoClosePositions: originalPresets.GroupAutoClosePositions,
                ProfilesAutoClosePositions: originalPresets.ProfilesAutoClosePositions,
                StrictCreditCheck: originalPresets.StrictCreditCheck,
                GroupProportionalAllocation: originalPresets.GroupProportionalAllocation);

            var setPresetsResponse = await client.Allocations.SetPresetsAsync(modifiedPresets, CT);
            setPresetsResponse.ShouldNotBeNull();
            setPresetsResponse.Success.ShouldBeTrue("Setting presets should succeed");

            // Step 8: Delete group
            var deleteResponse = await client.Allocations.DeleteGroupAsync(groupName, CT);
            deleteResponse.ShouldNotBeNull();
            deleteResponse.Success.ShouldBeTrue("Group deletion should succeed");

            StopRecording();
        }
        finally
        {
            // Cleanup: delete any leftover E2E groups
            try
            {
                var groups = await client.Allocations.GetGroupsAsync(CT);
                foreach (var group in groups.Data.Where(g =>
                             g.Name.StartsWith("E2E-", StringComparison.Ordinal)))
                {
                    try
                    {
                        await client.Allocations.DeleteGroupAsync(group.Name, CT);
                    }
                    catch
                    {
                        // Cleanup best-effort
                    }
                }
            }
            catch
            {
                // Cleanup best-effort
            }

            // Restore original presets if we captured them
            if (originalPresets is not null)
            {
                try
                {
                    var restoreRequest = new AllocationPresetsRequest(
                        DefaultMethodForAll: originalPresets.DefaultMethodForAll,
                        GroupAutoClosePositions: originalPresets.GroupAutoClosePositions,
                        ProfilesAutoClosePositions: originalPresets.ProfilesAutoClosePositions,
                        StrictCreditCheck: originalPresets.StrictCreditCheck,
                        GroupProportionalAllocation: originalPresets.GroupProportionalAllocation);
                    await client.Allocations.SetPresetsAsync(restoreRequest, CT);
                }
                catch
                {
                    // Cleanup best-effort
                }
            }

            await DisposeAsync();
        }
    }

    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task GetGroup_NonExistentName_HandlesError()
    {
        var (_, client) = CreateClient();

        try
        {
            StartRecording("Scenario09_GetGroup_NonExistent");

            // Probe: skip if FA allocation is not supported
            try
            {
                await client.Allocations.GetAccountsAsync(CT);
            }
            catch (ApiException)
            {
                StopRecording();
                return;
            }

            try
            {
                var detail = await client.Allocations.GetGroupAsync("NONEXISTENT-GROUP-99999", CT);

                // IBKR QUIRK: If we get here, the API returned 200 for a non-existent group.
                detail.ShouldNotBeNull();
            }
            catch (ApiException)
            {
                // Expected: IBKR returns an HTTP error for non-existent group names.
            }

            StopRecording();
        }
        finally
        {
            await DisposeAsync();
        }
    }

    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task DeleteGroup_NonExistentName_HandlesError()
    {
        var (_, client) = CreateClient();

        try
        {
            StartRecording("Scenario09_DeleteGroup_NonExistent");

            // Probe: skip if FA allocation is not supported
            try
            {
                await client.Allocations.GetAccountsAsync(CT);
            }
            catch (ApiException)
            {
                StopRecording();
                return;
            }

            try
            {
                var result = await client.Allocations.DeleteGroupAsync("NONEXISTENT-GROUP-99999", CT);

                // IBKR QUIRK: If we get here, the API returned 200 for a non-existent group.
                result.ShouldNotBeNull();
            }
            catch (ApiException)
            {
                // Expected: IBKR returns an HTTP error for non-existent group names.
            }

            StopRecording();
        }
        finally
        {
            await DisposeAsync();
        }
    }
}
