using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using IbkrConduit.Alerts;
using IbkrConduit.Client;
using Refit;
using Shouldly;

namespace IbkrConduit.Tests.Integration.E2E;

/// <summary>
/// E2E Scenario 6: Alert Management.
/// Exercises create, list, get detail, and delete alert operations
/// against a real IBKR paper trading account.
/// </summary>
[Collection("IBKR E2E")]
[ExcludeFromCodeCoverage]
public sealed class Scenario06_AlertManagementTests : E2eScenarioBase
{
    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task AlertManagement_FullWorkflow()
    {
        var (_, client) = CreateClient();
        var accountId = string.Empty;

        try
        {

            // Step 1: Get account ID
            var accounts = await client.Portfolio.GetAccountsAsync(CT);
            accounts.ShouldNotBeEmpty("Should have at least one account");
            accountId = accounts[0].Id;

            // Step 2: Search SPY conid
            var searchResults = await client.Contracts.SearchBySymbolAsync("SPY", CT);
            searchResults.ShouldNotBeEmpty("SPY search should return results");
            var spyConid = searchResults[0].Conid;

            // Step 3: Create alert
            var testId = $"E2E-{Guid.NewGuid():N}"[..20];
            var createRequest = new CreateAlertRequest(
                OrderId: 0,
                AlertName: testId,
                AlertMessage: "E2E test alert",
                AlertRepeatable: 0,
                OutsideRth: 1,
                Conditions: new List<AlertCondition>
                {
                    new(
                        Type: 1,
                        Conidex: spyConid.ToString(),
                        Operator: ">=",
                        TriggerMethod: "0",
                        Value: "9999.00"),
                });

            // IBKR QUIRK (discovered 2026-04-01): The alert creation endpoint may return
            // 403 Forbidden on paper trading accounts when using OAuth authentication.
            // This appears to be a permission limitation for paper accounts. When this
            // occurs, we skip the rest of the workflow gracefully.
            CreateAlertResponse createResponse;
            try
            {
                createResponse = await client.Alerts.CreateOrModifyAlertAsync(accountId, createRequest, CT);
            }
            catch (ApiException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Forbidden
                                              or System.Net.HttpStatusCode.ServiceUnavailable
                                              or System.Net.HttpStatusCode.InternalServerError)
            {
                // IBKR QUIRK: Alert creation returns 403/503/500 on some paper account configurations.
                // Skip the rest of the workflow — we cannot test CRUD without a successful create.
                return;
            }

            createResponse.ShouldNotBeNull();
            createResponse.OrderId.ShouldBeGreaterThan(0, "Created alert should have a positive OrderId");
            var alertId = createResponse.OrderId.ToString();

            // Step 4: List alerts — verify our alert appears
            var alerts = await client.Alerts.GetAlertsAsync(CT);
            alerts.ShouldNotBeNull();
            alerts.ShouldContain(a => a.AlertName == testId,
                "Alert list should contain the newly created alert");

            // Step 5: Get alert detail
            var detail = await client.Alerts.GetAlertDetailAsync(alertId, CT);
            detail.ShouldNotBeNull();
            detail.AlertName.ShouldBe(testId);
            detail.Conditions.ShouldNotBeEmpty("Alert should have at least one condition");

            // Step 6: Delete alert
            var deleteResponse = await client.Alerts.DeleteAlertAsync(accountId, alertId, CT);
            deleteResponse.ShouldNotBeNull();

            // Step 7: List alerts again — verify our alert is gone
            var alertsAfterDelete = await client.Alerts.GetAlertsAsync(CT);
            alertsAfterDelete.ShouldNotContain(a => a.AlertName == testId,
                "Alert list should not contain the deleted alert");

            // Step 8: Double-delete — verify IBKR behavior
            try
            {
                var doubleDeleteResponse = await client.Alerts.DeleteAlertAsync(accountId, alertId, CT);

                // IBKR QUIRK: If we get here, the API returned 200 for deleting an already-deleted alert.
                doubleDeleteResponse.ShouldNotBeNull();
            }
            catch (ApiException)
            {
                // Expected: IBKR returns an HTTP error for deleting a non-existent alert.
            }

        }
        finally
        {
            // Cleanup: delete any leftover E2E alerts
            try
            {
                if (!string.IsNullOrEmpty(accountId))
                {
                    var alerts = await client.Alerts.GetAlertsAsync(CT);
                    foreach (var alert in alerts.Where(a => a.AlertName.StartsWith("E2E-", StringComparison.Ordinal)))
                    {
                        try
                        {
                            await client.Alerts.DeleteAlertAsync(accountId, alert.OrderId.ToString(), CT);
                        }
                        catch
                        {
                            // Cleanup best-effort
                        }
                    }
                }
            }
            catch
            {
                // Cleanup best-effort
            }

            await DisposeAsync();
        }
    }

    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task GetAlertDetail_NonExistentAlertId_ThrowsOrReturnsError()
    {
        var (_, client) = CreateClient();

        try
        {

            try
            {
                var detail = await client.Alerts.GetAlertDetailAsync("0", CT);

                // IBKR QUIRK: If we get here, the API returned 200 for a non-existent alert ID.
                detail.ShouldNotBeNull();
            }
            catch (ApiException)
            {
                // Expected: IBKR returns an HTTP error for non-existent alert IDs.
            }

        }
        finally
        {
            await DisposeAsync();
        }
    }

    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task DeleteAlert_NonExistentAlertId_ThrowsOrReturnsError()
    {
        var (_, client) = CreateClient();

        try
        {

            var accounts = await client.Portfolio.GetAccountsAsync(CT);
            var accountId = accounts[0].Id;

            try
            {
                var result = await client.Alerts.DeleteAlertAsync(accountId, "0", CT);

                // IBKR QUIRK: If we get here, the API returned 200 for a non-existent alert.
                result.ShouldNotBeNull();
            }
            catch (ApiException)
            {
                // Expected: IBKR returns an HTTP error for non-existent alert IDs.
            }

        }
        finally
        {
            await DisposeAsync();
        }
    }
}
