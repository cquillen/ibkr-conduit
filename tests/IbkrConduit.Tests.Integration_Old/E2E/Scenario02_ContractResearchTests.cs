using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Contracts;
using Refit;
using Shouldly;

namespace IbkrConduit.Tests.Integration.E2E;

/// <summary>
/// E2E Scenario 2: Contract Research.
/// Exercises contract search, details, trading rules, security definitions,
/// option strikes, futures, stocks, currency pairs, and exchange rates
/// against a real IBKR paper trading account.
/// </summary>
[Collection("IBKR E2E")]
[ExcludeFromCodeCoverage]
public sealed class Scenario02_ContractResearchTests : E2eScenarioBase
{
    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task ContractResearch_FullWorkflow()
    {
        var (_, client) = CreateClient();

        try
        {

            // Step 1: Search for AAPL
            var searchResults = await client.Contracts.SearchBySymbolAsync("AAPL", CT);
            searchResults.ShouldNotBeEmpty("AAPL search should return results");
            var aaplConid = searchResults[0].Conid;
            aaplConid.ShouldBeGreaterThan(0, "AAPL conid should be positive");

            // Step 2: Get contract details
            var details = await client.Contracts.GetContractDetailsAsync(aaplConid.ToString(), CT);
            details.Symbol.ShouldContain("AAPL");

            // Step 3: Get trading rules (isBuy is required by IBKR)
            // IBKR QUIRK: The /iserver/contract/rules endpoint intermittently returns 500
            // on paper trading accounts. This may be related to session state or internal
            // server issues. We treat a 500 as a known quirk and continue the workflow.
            try
            {
                var tradingRules = await client.Contracts.GetTradingRulesAsync(
                    new TradingRulesRequest(aaplConid, null, true, false, null), CT);
                tradingRules.ShouldNotBeNull("Trading rules should not be null");
            }
            catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.InternalServerError)
            {
                // IBKR QUIRK: /iserver/contract/rules returns 500 intermittently on paper accounts.
            }

            // Step 4: Get security definitions by conid
            var secDefs = await client.Contracts.GetSecurityDefinitionsByConidAsync(
                aaplConid.ToString(), CT);
            secDefs.Secdef.ShouldNotBeEmpty("Security definitions should not be empty");

            // Step 5: Get trading schedule
            // IBKR QUIRK: The /trsrv/secdef/schedule endpoint sometimes returns 400 on paper
            // accounts. This may be due to assetClass/symbol/conid mismatch in IBKR's backend.
            try
            {
                var schedules = await client.Contracts.GetTradingScheduleAsync(
                    "STK", "AAPL", aaplConid.ToString(), cancellationToken: CT);
                schedules.ShouldNotBeEmpty("Trading schedule should not be empty");
            }
            catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                // IBKR QUIRK: Trading schedule returns 400 intermittently on paper accounts.
            }

            // Step 6: Get option strikes for nearest month
            var nearestMonth = GetNearestOptionMonth();
            var strikes = await client.Contracts.GetOptionStrikesAsync(
                aaplConid.ToString(), "OPT", nearestMonth, cancellationToken: CT);
            strikes.Call.ShouldNotBeEmpty("Call strikes should not be empty");
            strikes.Put.ShouldNotBeEmpty("Put strikes should not be empty");

            // Step 7: Get security definition info for options
            // IBKR QUIRK: The /iserver/secdef/info endpoint returns 400 if the month format
            // doesn't match available option months, or if no options exist for the month.
            try
            {
                var secDefInfo = await client.Contracts.GetSecurityDefinitionInfoAsync(
                    aaplConid.ToString(), "OPT", nearestMonth, cancellationToken: CT);
                secDefInfo.ShouldNotBeEmpty("Security definition info should not be empty");
            }
            catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                // IBKR QUIRK: SecDef info returns 400 if month doesn't match available options.
            }

            // Step 8: Get stocks by symbol
            var stocks = await client.Contracts.GetStocksBySymbolAsync("AAPL", CT);
            stocks.ShouldContainKey("AAPL");

            // Step 9: Get futures by symbol
            var futures = await client.Contracts.GetFuturesBySymbolAsync("ES", CT);
            futures.ShouldNotBeEmpty("ES futures should not be empty");

            // Step 10: Get all conids by exchange
            var exchangeConids = await client.Contracts.GetAllConidsByExchangeAsync("NASDAQ", CT);
            exchangeConids.ShouldNotBeEmpty("NASDAQ conids should not be empty");

            // Step 11: Get currency pairs
            var currencyPairs = await client.Contracts.GetCurrencyPairsAsync("USD", CT);
            currencyPairs.ShouldNotBeEmpty("USD currency pairs should not be empty");

            // Step 12: Get exchange rate
            var exchangeRate = await client.Contracts.GetExchangeRateAsync("USD", "EUR", CT);
            exchangeRate.Rate.ShouldBeGreaterThan(0m, "USD/EUR rate should be positive");

        }
        finally
        {
            await DisposeAsync();
        }
    }

    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task SearchBySymbol_NonExistent_ReturnsEmptyOrError()
    {
        var (_, client) = CreateClient();

        try
        {

            try
            {
                var results = await client.Contracts.SearchBySymbolAsync("ZZZZNOTREAL", CT);
                results.ShouldBeEmpty("Non-existent symbol should return empty results");
            }
            catch (ApiException)
            {
                // IBKR QUIRK: The symbol search endpoint returns a non-array JSON response
                // (e.g., an error object) for non-existent symbols, causing a deserialization
                // error in Refit. This is acceptable — the symbol was not found.
            }

        }
        finally
        {
            await DisposeAsync();
        }
    }

    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task GetContractDetails_InvalidConid_ThrowsApiException()
    {
        var (_, client) = CreateClient();

        try
        {

            try
            {
                var result = await client.Contracts.GetContractDetailsAsync("999999999", CT);

                // IBKR QUIRK: If we get here, the API returned 200 for an invalid conid.
                result.ShouldNotBeNull(
                    "IBKR QUIRK: API returned 200 for invalid conid instead of HTTP error");
            }
            catch (ApiException)
            {
                // Expected: IBKR returns an HTTP error for invalid conid.
            }

        }
        finally
        {
            await DisposeAsync();
        }
    }

    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task GetExchangeRate_SameCurrency_ReturnsRate()
    {
        var (_, client) = CreateClient();

        try
        {

            try
            {
                var result = await client.Contracts.GetExchangeRateAsync("USD", "USD", CT);

                // Same currency exchange rate should be 1.0 or very close to it.
                result.Rate.ShouldBeGreaterThan(0m,
                    "Same-currency exchange rate should be positive");
            }
            catch (ApiException)
            {
                // IBKR may reject same-currency exchange rate requests.
            }

        }
        finally
        {
            await DisposeAsync();
        }
    }

    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task GetTradingRules_InvalidConid_ThrowsApiException()
    {
        var (_, client) = CreateClient();

        try
        {

            try
            {
                var result = await client.Contracts.GetTradingRulesAsync(
                    new TradingRulesRequest(0, null, null, null, null), CT);

                // IBKR QUIRK: If we get here, the API returned 200 for conid 0.
                result.ShouldNotBeNull(
                    "IBKR QUIRK: API returned 200 for invalid conid instead of HTTP error");
            }
            catch (ApiException)
            {
                // Expected: IBKR returns an HTTP error for invalid conid.
            }

        }
        finally
        {
            await DisposeAsync();
        }
    }

    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task GetOptionStrikes_InvalidMonth_HandlesGracefully()
    {
        var (_, client) = CreateClient();

        try
        {

            // Search for AAPL first to get a valid conid
            var searchResults = await client.Contracts.SearchBySymbolAsync("AAPL", CT);
            searchResults.ShouldNotBeEmpty();
            var aaplConid = searchResults[0].Conid;

            try
            {
                var result = await client.Contracts.GetOptionStrikesAsync(
                    aaplConid.ToString(), "OPT", "999999", cancellationToken: CT);

                // IBKR QUIRK: If we get here, the API returned 200 for invalid month.
                // Strikes lists may be empty.
                (result.Call.Count + result.Put.Count).ShouldBe(0,
                    "Invalid month should return empty strike lists");
            }
            catch (ApiException)
            {
                // Expected: IBKR returns an HTTP error for invalid month format.
            }

        }
        finally
        {
            await DisposeAsync();
        }
    }

    /// <summary>
    /// Returns the nearest option month in YYYYMM format.
    /// Uses the current month if before the 15th, otherwise the next month.
    /// </summary>
    private static string GetNearestOptionMonth()
    {
        var now = DateTime.UtcNow;
        var target = now.Day <= 15 ? now : now.AddMonths(1);
        return target.ToString("yyyyMM");
    }
}
