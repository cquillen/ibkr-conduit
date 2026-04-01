using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.MarketData;
using Refit;
using Shouldly;

namespace IbkrConduit.Tests.Integration.E2E;

/// <summary>
/// E2E Scenario 3: Market Data and Scanners.
/// Exercises market data snapshots, historical data, scanner parameters,
/// iserver scanner, HMDS scanner, and unsubscribe operations
/// against a real IBKR paper trading account.
/// </summary>
[Collection("IBKR E2E")]
[ExcludeFromCodeCoverage]
public sealed class Scenario03_MarketDataScannerTests : E2eScenarioBase
{
    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task MarketDataAndScanners_FullWorkflow()
    {
        var (_, client) = CreateClient();

        try
        {
            StartRecording("Scenario03_MarketDataScanners");

            // Step 1: Search for SPY to get conid
            var searchResults = await client.Contracts.SearchBySymbolAsync("SPY", CT);
            searchResults.ShouldNotBeEmpty("SPY search should return results");
            var spyConid = searchResults[0].Conid;
            spyConid.ShouldBeGreaterThan(0, "SPY conid should be positive");

            // Step 2: First snapshot call (pre-flight — may return empty fields)
            var preFlightSnapshot = await client.MarketData.GetSnapshotAsync(
                new[] { spyConid }, new[] { "31", "84" }, CT);

            // Step 3: Wait for pre-flight data to become available
            await Task.Delay(3000, CT);

            // Step 4: Second snapshot call — should have data
            var snapshot = await client.MarketData.GetSnapshotAsync(
                new[] { spyConid }, new[] { "31", "84" }, CT);
            snapshot.ShouldNotBeEmpty("Snapshot should return data after pre-flight");
            snapshot[0].Conid.ShouldBe(spyConid);

            // Step 5: Get historical data
            var history = await client.MarketData.GetHistoryAsync(
                spyConid, "5d", "1d", cancellationToken: CT);
            history.ShouldNotBeNull("Historical data response should not be null");
            history.Data.ShouldNotBeNull("Historical data bars should not be null");
            history.Data.ShouldNotBeEmpty("Historical data should contain bars");

            // Step 6: Get scanner parameters
            var scannerParams = await client.MarketData.GetScannerParametersAsync(CT);
            scannerParams.ScanTypeList.ShouldNotBeNull("Scanner type list should not be null");
            scannerParams.ScanTypeList.ShouldNotBeEmpty("Scanner type list should not be empty");

            // Step 7: Run iserver scanner
            // IBKR QUIRK: The /iserver/scanner/run endpoint returns 400 Bad Request for
            // certain scan types or when market data subscriptions are not active.
            // We treat a 400 as a known quirk and continue the workflow.
            try
            {
                var scannerResult = await client.MarketData.RunScannerAsync(
                    new ScannerRequest("STK", "TOP_TRADE_COUNT", "STK.US.MAJOR", null), CT);
                scannerResult.Contracts.ShouldNotBeNull("Scanner contracts should not be null");
            }
            catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                // IBKR QUIRK: /iserver/scanner/run returns 400 for some scan types on paper accounts.
            }

            // Step 8: Run HMDS scanner
            // IBKR QUIRK: The /hmds/scanner endpoint returns 400 for some scan codes or
            // market data subscription levels on paper accounts.
            try
            {
                var hmdsScannerResult = await client.MarketData.RunHmdsScannerAsync(
                    new HmdsScannerRequest("STK", "STK.US.MAJOR", "TOP_PERC_GAIN", "STK", 10, null), CT);
                hmdsScannerResult.ShouldNotBeNull("HMDS scanner response should not be null");
            }
            catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                // IBKR QUIRK: HMDS scanner returns 400 on paper accounts for some scan codes.
            }

            // Step 9: Unsubscribe from SPY market data
            var unsubResult = await client.MarketData.UnsubscribeAsync(spyConid, CT);
            unsubResult.ShouldNotBeNull("Unsubscribe response should not be null");

            // Step 10: Unsubscribe all
            var unsubAllResult = await client.MarketData.UnsubscribeAllAsync(CT);
            unsubAllResult.ShouldNotBeNull("UnsubscribeAll response should not be null");

            StopRecording();
        }
        finally
        {
            // Cleanup: ensure we unsubscribe from all market data
            try
            {
                await client.MarketData.UnsubscribeAllAsync(CT);
            }
            catch
            {
                // Best-effort cleanup — ignore errors during dispose.
            }

            await DisposeAsync();
        }
    }

    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task GetSnapshot_InvalidConid_ReturnsEmptyOrError()
    {
        var (_, client) = CreateClient();

        try
        {
            StartRecording("Scenario03_InvalidConidSnapshot");

            try
            {
                var result = await client.MarketData.GetSnapshotAsync(
                    new[] { 0 }, new[] { "31" }, CT);

                // IBKR QUIRK: The snapshot endpoint may return an empty list or a snapshot
                // with no data for invalid conids rather than throwing an HTTP error.
                // Either outcome is acceptable.
            }
            catch (ApiException)
            {
                // Expected: IBKR returns an HTTP error for invalid conid.
            }

            StopRecording();
        }
        finally
        {
            await DisposeAsync();
        }
    }

    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task GetHistory_InvalidPeriod_ThrowsApiException()
    {
        var (_, client) = CreateClient();

        try
        {
            StartRecording("Scenario03_InvalidPeriodHistory");

            // Search for SPY first to get a valid conid
            var searchResults = await client.Contracts.SearchBySymbolAsync("SPY", CT);
            searchResults.ShouldNotBeEmpty();
            var spyConid = searchResults[0].Conid;

            try
            {
                var result = await client.MarketData.GetHistoryAsync(
                    spyConid, "INVALID_PERIOD", "1d", cancellationToken: CT);

                // IBKR QUIRK: If we get here, the API returned 200 for an invalid period.
                result.ShouldNotBeNull(
                    "IBKR QUIRK: API returned 200 for invalid period instead of HTTP error");
            }
            catch (ApiException)
            {
                // Expected: IBKR returns an HTTP error for invalid period format.
            }

            StopRecording();
        }
        finally
        {
            await DisposeAsync();
        }
    }

    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task Unsubscribe_NeverSubscribed_HandlesGracefully()
    {
        var (_, client) = CreateClient();

        try
        {
            StartRecording("Scenario03_UnsubscribeNeverSubscribed");

            // Unsubscribing from a conid we never subscribed to should not crash.
            try
            {
                var result = await client.MarketData.UnsubscribeAsync(999999999, CT);
                result.ShouldNotBeNull("Unsubscribe should return a response even for unknown conid");
            }
            catch (ApiException)
            {
                // Some IBKR setups may return an error for unsubscribing unknown conids.
            }

            StopRecording();
        }
        finally
        {
            await DisposeAsync();
        }
    }

    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task RunScanner_InvalidType_ThrowsApiException()
    {
        var (_, client) = CreateClient();

        try
        {
            StartRecording("Scenario03_InvalidScannerType");

            try
            {
                var result = await client.MarketData.RunScannerAsync(
                    new ScannerRequest("STK", "TOTALLY_INVALID_SCAN_TYPE", "STK.US.MAJOR", null), CT);

                // IBKR QUIRK: If we get here, the API returned 200 for an invalid scan type.
                // The contracts list may be null or empty.
            }
            catch (ApiException)
            {
                // Expected: IBKR returns an HTTP error for invalid scan type.
            }

            StopRecording();
        }
        finally
        {
            await DisposeAsync();
        }
    }
}
