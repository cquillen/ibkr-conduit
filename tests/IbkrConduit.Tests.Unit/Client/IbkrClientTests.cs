using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Contracts;
using IbkrConduit.Flex;
using IbkrConduit.MarketData;
using IbkrConduit.Orders;
using IbkrConduit.Portfolio;
using IbkrConduit.Session;
using IbkrConduit.Streaming;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Client;

public class IbkrClientTests
{
    [Fact]
    public void Portfolio_ReturnsSameInstance()
    {
        var portfolio = new FakePortfolioOperations();
        var client = CreateClient(portfolio: portfolio);

        client.Portfolio.ShouldBeSameAs(portfolio);
    }

    [Fact]
    public void Contracts_ReturnsSameInstance()
    {
        var contracts = new FakeContractOperations();
        var client = CreateClient(contracts: contracts);

        client.Contracts.ShouldBeSameAs(contracts);
    }

    [Fact]
    public void Orders_ReturnsSameInstance()
    {
        var orders = new FakeOrderOperations();
        var client = CreateClient(orders: orders);

        client.Orders.ShouldBeSameAs(orders);
    }

    [Fact]
    public void MarketData_ReturnsSameInstance()
    {
        var marketData = new FakeMarketDataOperations();
        var client = CreateClient(marketData: marketData);

        client.MarketData.ShouldBeSameAs(marketData);
    }

    [Fact]
    public void Streaming_ReturnsSameInstance()
    {
        var streaming = new FakeStreamingOperations();
        var client = CreateClient(streaming: streaming);

        client.Streaming.ShouldBeSameAs(streaming);
    }

    [Fact]
    public void Flex_ReturnsSameInstance()
    {
        var flex = new FakeFlexOperations();
        var client = CreateClient(flex: flex);

        client.Flex.ShouldBeSameAs(flex);
    }

    [Fact]
    public async Task DisposeAsync_DisposesSessionManager()
    {
        var sessionManager = new FakeSessionManager();
        var client = CreateClient(sessionManager: sessionManager);

        await client.DisposeAsync();

        sessionManager.Disposed.ShouldBeTrue();
    }

    private static IbkrClient CreateClient(
        IPortfolioOperations? portfolio = null,
        IContractOperations? contracts = null,
        IOrderOperations? orders = null,
        IMarketDataOperations? marketData = null,
        IStreamingOperations? streaming = null,
        IFlexOperations? flex = null,
        ISessionManager? sessionManager = null) =>
        new(
            portfolio ?? new FakePortfolioOperations(),
            contracts ?? new FakeContractOperations(),
            orders ?? new FakeOrderOperations(),
            marketData ?? new FakeMarketDataOperations(),
            streaming ?? new FakeStreamingOperations(),
            flex ?? new FakeFlexOperations(),
            sessionManager ?? new FakeSessionManager());

    private class FakePortfolioOperations : IPortfolioOperations
    {
        public Task<List<Account>> GetAccountsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<Account>());

        public Task<List<Position>> GetPositionsAsync(string accountId, int page = 0,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Dictionary<string, AccountSummaryEntry>> GetAccountSummaryAsync(string accountId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Dictionary<string, LedgerEntry>> GetLedgerAsync(string accountId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AccountInfo> GetAccountInfoAsync(string accountId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AccountAllocation> GetAccountAllocationAsync(string accountId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<Position>> GetPositionByConidAsync(string accountId, string conid,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<PositionContractInfo> GetPositionAndContractInfoAsync(string conid,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task InvalidatePortfolioCacheAsync(string accountId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AccountPerformance> GetAccountPerformanceAsync(List<string> accountIds, string period,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<TransactionHistory> GetTransactionHistoryAsync(List<string> accountIds,
            List<string> conids, string currency, int? days = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private class FakeContractOperations : IContractOperations
    {
        public Task<List<ContractSearchResult>> SearchBySymbolAsync(
            string symbol, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<ContractSearchResult>());

        public Task<ContractDetails> GetContractDetailsAsync(
            string conid, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<SecurityDefinitionInfo>> GetSecurityDefinitionInfoAsync(
            string conid, string sectype, string month,
            string? exchange = null, string? strike = null, string? right = null, string? issuerId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<OptionStrikes> GetOptionStrikesAsync(
            string conid, string sectype, string month,
            string? exchange = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<TradingRules> GetTradingRulesAsync(
            TradingRulesRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<SecurityDefinitionResponse> GetSecurityDefinitionsByConidAsync(
            string conids,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<ExchangeConid>> GetAllConidsByExchangeAsync(
            string exchange,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Dictionary<string, List<FutureContract>>> GetFuturesBySymbolAsync(
            string symbols,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Dictionary<string, List<StockContract>>> GetStocksBySymbolAsync(
            string symbols,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<TradingSchedule>> GetTradingScheduleAsync(
            string assetClass, string symbol, string conid,
            string? exchange = null, string? exchangeFilter = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Dictionary<string, List<CurrencyPair>>> GetCurrencyPairsAsync(
            string currency,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ExchangeRateResponse> GetExchangeRateAsync(
            string source, string target,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private class FakeOrderOperations : IOrderOperations
    {
        public Task<OrderResult> PlaceOrderAsync(
            string accountId, OrderRequest order, CancellationToken cancellationToken = default) =>
            Task.FromResult(new OrderResult("1", "Submitted"));

        public Task<CancelOrderResponse> CancelOrderAsync(
            string accountId, string orderId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<LiveOrder>> GetLiveOrdersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<LiveOrder>());

        public Task<List<Trade>> GetTradesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<Trade>());

        public Task<OrderResult> ModifyOrderAsync(
            string accountId, string orderId, OrderRequest order,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<WhatIfResponse> WhatIfOrderAsync(
            string accountId, OrderRequest order,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<OrderStatus> GetOrderStatusAsync(
            string orderId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private class FakeMarketDataOperations : IMarketDataOperations
    {
        public Task<List<MarketDataSnapshot>> GetSnapshotAsync(int[] conids, string[] fields,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<MarketDataSnapshot>());

        public Task<HistoricalDataResponse> GetHistoryAsync(int conid, string period, string bar,
            bool? outsideRth = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private class FakeStreamingOperations : IStreamingOperations
    {
        public IObservable<MarketDataTick> MarketData(int conid, string[] fields, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IObservable<OrderUpdate> OrderUpdates(int? days = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IObservable<PnlUpdate> ProfitAndLoss(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IObservable<AccountSummaryUpdate> AccountSummary(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IObservable<AccountLedgerUpdate> AccountLedger(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private class FakeFlexOperations : IFlexOperations
    {
        public Task<FlexQueryResult> ExecuteQueryAsync(
            string queryId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<FlexQueryResult> ExecuteQueryAsync(
            string queryId, string fromDate, string toDate,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private class FakeSessionManager : ISessionManager
    {
        public bool Disposed { get; private set; }

        public Task EnsureInitializedAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ReauthenticateAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
