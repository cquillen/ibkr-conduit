using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Accounts;
using IbkrConduit.Alerts;
using IbkrConduit.Allocation;
using IbkrConduit.Client;
using IbkrConduit.Contracts;
using IbkrConduit.Flex;
using IbkrConduit.Fyi;
using IbkrConduit.MarketData;
using IbkrConduit.Orders;
using IbkrConduit.Portfolio;
using IbkrConduit.Session;
using IbkrConduit.Streaming;
using IbkrConduit.Watchlists;
using OneOf;
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
        IAccountOperations? accounts = null,
        IAlertOperations? alerts = null,
        IWatchlistOperations? watchlists = null,
        IFyiOperations? notifications = null,
        IAllocationOperations? allocations = null,
        ISessionManager? sessionManager = null) =>
        new(
            portfolio ?? new FakePortfolioOperations(),
            contracts ?? new FakeContractOperations(),
            orders ?? new FakeOrderOperations(),
            marketData ?? new FakeMarketDataOperations(),
            streaming ?? new FakeStreamingOperations(),
            flex ?? new FakeFlexOperations(),
            accounts ?? new FakeAccountOperations(),
            alerts ?? new FakeAlertOperations(),
            watchlists ?? new FakeWatchlistOperations(),
            notifications ?? new FakeFyiOperations(),
            allocations ?? new FakeAllocationOperations(),
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

        public Task<AccountAllocation> GetConsolidatedAllocationAsync(List<string> accountIds,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<ComboPosition>> GetComboPositionsAsync(string accountId, bool? nocache = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<Position>> GetRealTimePositionsAsync(string accountId,
            string? model = null, string? sort = null, string? direction = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<SubAccount>> GetSubAccountsAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<SubAccount>> GetSubAccountsPagedAsync(int page = 0,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AllPeriodsPerformance> GetAllPeriodsPerformanceAsync(List<string> accountIds,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<PartitionedPnl> GetPartitionedPnlAsync(CancellationToken cancellationToken = default) =>
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
        public Task<OneOf<OrderSubmitted, OrderConfirmationRequired>> PlaceOrderAsync(
            string accountId, OrderRequest order, CancellationToken cancellationToken = default) =>
            Task.FromResult<OneOf<OrderSubmitted, OrderConfirmationRequired>>(new OrderSubmitted("1", "Submitted"));

        public Task<CancelOrderResponse> CancelOrderAsync(
            string accountId, string orderId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<LiveOrder>> GetLiveOrdersAsync(string? filters = null, bool? force = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<LiveOrder>());

        public Task<List<Trade>> GetTradesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<Trade>());

        public Task<OneOf<OrderSubmitted, OrderConfirmationRequired>> ModifyOrderAsync(
            string accountId, string orderId, OrderRequest order,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<OneOf<OrderSubmitted, OrderConfirmationRequired>> ReplyAsync(
            string replyId, bool confirmed, CancellationToken cancellationToken = default) =>
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

        public Task<MarketDataSnapshot> GetRegulatorySnapshotAsync(int conid,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<UnsubscribeResponse> UnsubscribeAsync(int conid,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<UnsubscribeAllResponse> UnsubscribeAllAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ScannerResponse> RunScannerAsync(ScannerRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ScannerParameters> GetScannerParametersAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<HmdsScannerResponse> RunHmdsScannerAsync(HmdsScannerRequest request,
            CancellationToken cancellationToken = default) =>
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

    private class FakeAccountOperations : IAccountOperations
    {
        public Task<IserverAccountsResponse> GetAccountsAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<SwitchAccountResponse> SwitchAccountAsync(string accountId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<DynAccountResponse> SetDynAccountAsync(string accountId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<AccountSearchResult>> SearchAccountsAsync(string pattern, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IserverAccountInfo> GetAccountInfoAsync(string accountId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private class FakeAlertOperations : IAlertOperations
    {
        public Task<CreateAlertResponse> CreateOrModifyAlertAsync(string accountId, CreateAlertRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<AlertSummary>> GetAlertsAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AlertDetail> GetAlertDetailAsync(string alertId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<DeleteAlertResponse> DeleteAlertAsync(string accountId, string alertId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private class FakeWatchlistOperations : IWatchlistOperations
    {
        public Task<CreateWatchlistResponse> CreateWatchlistAsync(CreateWatchlistRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<WatchlistSummary>> GetWatchlistsAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<WatchlistDetail> GetWatchlistAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<DeleteWatchlistResponse> DeleteWatchlistAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private class FakeFyiOperations : IFyiOperations
    {
        public Task<UnreadBulletinCountResponse> GetUnreadCountAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<FyiSettingItem>> GetSettingsAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<FyiAcknowledgementResponse> UpdateSettingAsync(string typecode, bool enabled, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<FyiDisclaimerResponse> GetDisclaimerAsync(string typecode, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<FyiAcknowledgementResponse> MarkDisclaimerReadAsync(string typecode, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<FyiDeliveryOptionsResponse> GetDeliveryOptionsAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<FyiAcknowledgementResponse> SetEmailDeliveryAsync(bool enabled, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<FyiAcknowledgementResponse> RegisterDeviceAsync(FyiDeviceRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task DeleteDeviceAsync(string deviceId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<FyiNotification>> GetNotificationsAsync(string? max = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<FyiNotification>> GetMoreNotificationsAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<FyiNotificationReadResponse> MarkNotificationReadAsync(string notificationId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private class FakeAllocationOperations : IAllocationOperations
    {
        public Task<AllocationAccountsResponse> GetAccountsAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AllocationGroupListResponse> GetGroupsAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AllocationSuccessResponse> AddGroupAsync(AllocationGroupRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AllocationGroupDetail> GetGroupAsync(string name, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AllocationSuccessResponse> DeleteGroupAsync(string name, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AllocationSuccessResponse> ModifyGroupAsync(AllocationGroupRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AllocationPresetsResponse> GetPresetsAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AllocationSuccessResponse> SetPresetsAsync(AllocationPresetsRequest request, CancellationToken cancellationToken = default) =>
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
