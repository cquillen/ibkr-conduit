using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Watchlists;
using Refit;
using Shouldly;

namespace IbkrConduit.Tests.Integration.E2E;

/// <summary>
/// E2E Scenario 7: Watchlist Management.
/// Exercises create, list, get, and delete watchlist operations
/// against a real IBKR paper trading account.
/// </summary>
[Collection("IBKR E2E")]
[ExcludeFromCodeCoverage]
public sealed class Scenario07_WatchlistManagementTests : E2eScenarioBase
{
    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task WatchlistManagement_FullWorkflow()
    {
        var (_, client) = CreateClient();
        var watchlistId = string.Empty;

        try
        {

            // Step 1: Search SPY and AAPL conids
            var spyResults = await client.Contracts.SearchBySymbolAsync("SPY", CT);
            spyResults.ShouldNotBeEmpty("SPY search should return results");
            var spyConid = spyResults[0].Conid;

            var aaplResults = await client.Contracts.SearchBySymbolAsync("AAPL", CT);
            aaplResults.ShouldNotBeEmpty("AAPL search should return results");
            var aaplConid = aaplResults[0].Conid;

            // Step 2: Create watchlist
            var testId = $"E2E-{Guid.NewGuid():N}"[..20];
            var createRequest = new CreateWatchlistRequest(
                Id: testId,
                Rows: new List<WatchlistRow>
                {
                    new(C: spyConid, H: "SPY"),
                    new(C: aaplConid, H: "AAPL"),
                });

            // IBKR QUIRK (discovered 2026-04-01): The watchlist creation endpoint may return
            // 503 Service Unavailable on paper trading accounts. This appears to be an
            // intermittent or configuration-dependent limitation.
            CreateWatchlistResponse createResponse;
            try
            {
                createResponse = await client.Watchlists.CreateWatchlistAsync(createRequest, CT);
            }
            catch (ApiException ex) when (ex.StatusCode is System.Net.HttpStatusCode.ServiceUnavailable
                                              or System.Net.HttpStatusCode.Forbidden
                                              or System.Net.HttpStatusCode.InternalServerError)
            {
                // IBKR QUIRK: Watchlist creation returns 503/403/500 on some paper account configurations.
                // Skip the rest of the workflow — we cannot test CRUD without a successful create.
                return;
            }

            createResponse.ShouldNotBeNull();
            createResponse.Id.ShouldNotBeNullOrEmpty("Created watchlist should have an Id");
            watchlistId = createResponse.Id;

            // Step 3: List watchlists — verify our watchlist appears
            var watchlists = await client.Watchlists.GetWatchlistsAsync(CT);
            watchlists.ShouldNotBeNull();
            watchlists.ShouldContain(
                w => w.Id == watchlistId || w.Name == testId,
                "Watchlist list should contain the newly created watchlist");

            // Step 4: Get watchlist detail
            var detail = await client.Watchlists.GetWatchlistAsync(watchlistId, CT);
            detail.ShouldNotBeNull();
            detail.Rows.ShouldNotBeEmpty("Watchlist should have rows");
            detail.Rows.Count.ShouldBe(2, "Watchlist should have 2 rows (SPY and AAPL)");

            // Step 5: Delete watchlist
            var deleteResponse = await client.Watchlists.DeleteWatchlistAsync(watchlistId, CT);
            deleteResponse.ShouldNotBeNull();
            deleteResponse.Deleted.ShouldBeTrue("Delete should confirm success");

            // Step 6: List watchlists again — verify ours is gone
            var watchlistsAfterDelete = await client.Watchlists.GetWatchlistsAsync(CT);
            watchlistsAfterDelete.ShouldNotContain(
                w => w.Id == watchlistId || w.Name == testId,
                "Watchlist list should not contain the deleted watchlist");

            // Step 7: Double-delete — verify IBKR behavior
            try
            {
                var doubleDeleteResponse = await client.Watchlists.DeleteWatchlistAsync(watchlistId, CT);

                // IBKR QUIRK: If we get here, the API returned 200 for deleting an already-deleted watchlist.
                doubleDeleteResponse.ShouldNotBeNull();
            }
            catch (ApiException)
            {
                // Expected: IBKR returns an HTTP error for deleting a non-existent watchlist.
            }

        }
        finally
        {
            // Cleanup: delete any leftover E2E watchlists
            try
            {
                var watchlists = await client.Watchlists.GetWatchlistsAsync(CT);
                foreach (var wl in watchlists.Where(w =>
                             w.Name.StartsWith("E2E-", StringComparison.Ordinal)))
                {
                    try
                    {
                        await client.Watchlists.DeleteWatchlistAsync(wl.Id, CT);
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

            await DisposeAsync();
        }
    }

    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task GetWatchlist_NonExistentId_ThrowsOrReturnsError()
    {
        var (_, client) = CreateClient();

        try
        {

            try
            {
                var detail = await client.Watchlists.GetWatchlistAsync("FAKE-ID-99999", CT);

                // IBKR QUIRK: If we get here, the API returned 200 for a non-existent watchlist ID.
                detail.ShouldNotBeNull();
            }
            catch (ApiException)
            {
                // Expected: IBKR returns an HTTP error for non-existent watchlist IDs.
            }

        }
        finally
        {
            await DisposeAsync();
        }
    }

    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task DeleteWatchlist_NonExistentId_ThrowsOrReturnsError()
    {
        var (_, client) = CreateClient();

        try
        {

            try
            {
                var result = await client.Watchlists.DeleteWatchlistAsync("FAKE-ID-99999", CT);

                // IBKR QUIRK: If we get here, the API returned 200 for a non-existent watchlist.
                result.ShouldNotBeNull();
            }
            catch (ApiException)
            {
                // Expected: IBKR returns an HTTP error for non-existent watchlist IDs.
            }

        }
        finally
        {
            await DisposeAsync();
        }
    }
}
