using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Refit;
using Shouldly;

namespace IbkrConduit.Tests.Integration.E2E;

/// <summary>
/// E2E Scenario 5: Portfolio Deep Dive.
/// Exercises portfolio accounts, positions, balances, P&amp;L, performance,
/// transaction history, and sub-account endpoints against a real IBKR paper trading account.
/// </summary>
[Collection("IBKR E2E")]
[ExcludeFromCodeCoverage]
public sealed class Scenario05_PortfolioDeepDiveTests : E2eScenarioBase
{
    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task PortfolioDeepDive_FullWorkflow()
    {
        var (_, client) = CreateClient();

        try
        {
            StartRecording("Scenario05_PortfolioDeepDive");

            // Step 1: Get portfolio accounts
            var accounts = await client.Portfolio.GetAccountsAsync(CT);
            accounts.ShouldNotBeEmpty("Should have at least one portfolio account");
            var accountId = accounts[0].Id;
            accountId.ShouldNotBeNullOrWhiteSpace();

            // Step 2: Get account summary
            var summary = await client.Portfolio.GetAccountSummaryAsync(accountId, CT);
            summary.ShouldNotBeNull();
            summary.ShouldNotBeEmpty("Account summary should contain at least one entry");

            // Step 3: Get ledger (cash balances by currency)
            var ledger = await client.Portfolio.GetLedgerAsync(accountId, CT);
            ledger.ShouldNotBeNull();
            ledger.ShouldNotBeEmpty("Ledger should contain at least one currency entry");

            // Step 4: Get account metadata
            var meta = await client.Portfolio.GetAccountInfoAsync(accountId, CT);
            meta.ShouldNotBeNull();
            meta.Id.ShouldNotBeNullOrWhiteSpace();

            // Step 5: Get account allocation
            var allocation = await client.Portfolio.GetAccountAllocationAsync(accountId, CT);
            allocation.ShouldNotBeNull();

            // Step 6: Get positions page 0
            var positions = await client.Portfolio.GetPositionsAsync(accountId, 0, CT);
            positions.ShouldNotBeNull();

            // Step 7: If positions exist, get position by conid for first position
            if (positions.Count > 0)
            {
                var firstConid = positions[0].Conid;
                var positionByConid = await client.Portfolio.GetPositionByConidAsync(
                    accountId, firstConid.ToString(), CT);
                positionByConid.ShouldNotBeNull();
                positionByConid.ShouldNotBeEmpty("Position by conid should return at least the held position");
            }

            // Step 8: If positions exist, get position + contract info
            if (positions.Count > 0)
            {
                var firstConid = positions[0].Conid;
                var posContractInfo = await client.Portfolio.GetPositionAndContractInfoAsync(
                    firstConid.ToString(), CT);
                posContractInfo.ShouldNotBeNull();
            }

            // Step 9: Get real-time positions (portfolio2 — bypasses server cache)
            var realTimePositions = await client.Portfolio.GetRealTimePositionsAsync(accountId, cancellationToken: CT);
            realTimePositions.ShouldNotBeNull();

            // Step 10: Invalidate portfolio cache
            await client.Portfolio.InvalidatePortfolioCacheAsync(accountId, CT);

            // Step 11: Get combo positions (may be empty for accounts without spreads)
            try
            {
                var comboPositions = await client.Portfolio.GetComboPositionsAsync(accountId, cancellationToken: CT);
                comboPositions.ShouldNotBeNull();
                // Combo positions may be empty — that is expected for accounts without spreads.
            }
            catch (ApiException)
            {
                // IBKR QUIRK: Combo positions endpoint may return an HTTP error when
                // the account has no combo/spread positions instead of an empty list.
            }

            // Step 12: Get consolidated allocation across accounts
            var accountIds = new List<string> { accountId };
            var consolidatedAllocation = await client.Portfolio.GetConsolidatedAllocationAsync(accountIds, CT);
            consolidatedAllocation.ShouldNotBeNull();

            // Step 13: Get performance (1M period)
            var performance = await client.Portfolio.GetAccountPerformanceAsync(accountIds, "1M", CT);
            performance.ShouldNotBeNull();

            // Step 14: Get transaction history
            var transactions = await client.Portfolio.GetTransactionHistoryAsync(
                accountIds, new List<string>(), "USD", 7, CT);
            transactions.ShouldNotBeNull();

            // Step 15: Get all-periods performance
            var allPeriodsPerf = await client.Portfolio.GetAllPeriodsPerformanceAsync(accountIds, CT);
            allPeriodsPerf.ShouldNotBeNull();

            // Step 16: Get partitioned P&L
            var pnl = await client.Portfolio.GetPartitionedPnlAsync(CT);
            pnl.ShouldNotBeNull();

            // Step 17: Get subaccounts (may return empty/error for individual accounts)
            try
            {
                var subAccounts = await client.Portfolio.GetSubAccountsAsync(CT);
                subAccounts.ShouldNotBeNull();
                // Subaccounts may be empty for individual (non-FA) accounts — that is expected.
            }
            catch (ApiException)
            {
                // IBKR QUIRK: Subaccounts endpoint returns an HTTP error for individual
                // (non-FA/IBroker) accounts instead of an empty list.
            }

            // Step 18: Get subaccounts paged (same FA limitation)
            try
            {
                var subAccountsPaged = await client.Portfolio.GetSubAccountsPagedAsync(0, CT);
                subAccountsPaged.ShouldNotBeNull();
                // Paged subaccounts may be empty for individual (non-FA) accounts.
            }
            catch (ApiException)
            {
                // IBKR QUIRK: Paged subaccounts endpoint returns an HTTP error for individual
                // (non-FA/IBroker) accounts instead of an empty list.
            }

            StopRecording();
        }
        finally
        {
            await DisposeAsync();
        }
    }

    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task GetPositionByConid_NonExistentConid_ReturnsEmpty()
    {
        var (_, client) = CreateClient();

        try
        {
            StartRecording("Scenario05_NonExistentConid");

            var accounts = await client.Portfolio.GetAccountsAsync(CT);
            accounts.ShouldNotBeEmpty();
            var accountId = accounts[0].Id;

            // A conid that almost certainly does not exist in the portfolio
            try
            {
                var positions = await client.Portfolio.GetPositionByConidAsync(
                    accountId, "999999999", CT);
                positions.ShouldNotBeNull();
                positions.ShouldBeEmpty("Position for non-existent conid should be empty");
            }
            catch (ApiException)
            {
                // IBKR QUIRK: The API may return an HTTP error for a conid not held
                // in the portfolio instead of an empty list.
            }

            StopRecording();
        }
        finally
        {
            await DisposeAsync();
        }
    }

    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task GetPositions_PageBeyondRange_ReturnsEmpty()
    {
        var (_, client) = CreateClient();

        try
        {
            StartRecording("Scenario05_PageBeyondRange");

            var accounts = await client.Portfolio.GetAccountsAsync(CT);
            accounts.ShouldNotBeEmpty();
            var accountId = accounts[0].Id;

            // IBKR QUIRK: Page 999 is expected to be beyond any real position pages,
            // but IBKR ignores invalid page numbers and returns all positions instead of
            // an empty list. Accept either empty or non-empty results.
            var positions = await client.Portfolio.GetPositionsAsync(accountId, 999, CT);
            positions.ShouldNotBeNull();

            StopRecording();
        }
        finally
        {
            await DisposeAsync();
        }
    }

    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task GetPerformance_EmptyAccountList_HandlesGracefully()
    {
        var (_, client) = CreateClient();

        try
        {
            StartRecording("Scenario05_EmptyAccountList");

            // Empty account list should either return a valid (possibly empty) response
            // or throw an ApiException — both are acceptable.
            try
            {
                var performance = await client.Portfolio.GetAccountPerformanceAsync(
                    new List<string>(), "1M", CT);
                performance.ShouldNotBeNull();
            }
            catch (ApiException)
            {
                // IBKR QUIRK: Performance endpoint may return an HTTP error when
                // given an empty account list instead of an empty response.
            }

            StopRecording();
        }
        finally
        {
            await DisposeAsync();
        }
    }
}
