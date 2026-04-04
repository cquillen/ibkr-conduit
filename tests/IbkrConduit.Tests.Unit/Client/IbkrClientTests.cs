using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Accounts;
using IbkrConduit.Alerts;
using IbkrConduit.Client;
using IbkrConduit.Contracts;
using IbkrConduit.Errors;
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
    public async Task ValidateConnectionAsync_DelegatesToSessionManager()
    {
        var sessionManager = new FakeSessionManager();
        var client = CreateClient(sessionManager: sessionManager);
        await client.ValidateConnectionAsync(TestContext.Current.CancellationToken);
        sessionManager.EnsureInitializedCallCount.ShouldBe(1);
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
            sessionManager ?? new FakeSessionManager());

    private class FakePortfolioOperations : IPortfolioOperations
    {
        public Task<Result<List<Account>>> GetAccountsAsync(CancellationToken ct = default) => Task.FromResult(Result<List<Account>>.Success([]));
        public Task<Result<List<Position>>> GetPositionsAsync(string accountId, int page = 0, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Dictionary<string, AccountSummaryEntry>>> GetAccountSummaryAsync(string accountId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Dictionary<string, LedgerEntry>>> GetLedgerAsync(string accountId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<AccountInfo>> GetAccountInfoAsync(string accountId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<AccountAllocation>> GetAccountAllocationAsync(string accountId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<Position>>> GetPositionByConidAsync(string accountId, string conid, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<PositionContractInfo>> GetPositionAndContractInfoAsync(string conid, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<bool>> InvalidatePortfolioCacheAsync(string accountId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<AccountPerformance>> GetAccountPerformanceAsync(List<string> accountIds, string period, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<TransactionHistory>> GetTransactionHistoryAsync(List<string> accountIds, List<string> conids, string currency, int? days = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<AccountAllocation>> GetConsolidatedAllocationAsync(List<string> accountIds, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<ComboPosition>>> GetComboPositionsAsync(string accountId, bool? nocache = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<Position>>> GetRealTimePositionsAsync(string accountId, string? model = null, string? sort = null, string? direction = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<SubAccount>>> GetSubAccountsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<SubAccount>>> GetSubAccountsPagedAsync(int page = 0, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<AllPeriodsPerformance>> GetAllPeriodsPerformanceAsync(List<string> accountIds, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<PartitionedPnl>> GetPartitionedPnlAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private class FakeContractOperations : IContractOperations
    {
        public Task<Result<List<ContractSearchResult>>> SearchBySymbolAsync(string symbol, CancellationToken ct = default) => Task.FromResult(Result<List<ContractSearchResult>>.Success([]));
        public Task<Result<ContractDetails>> GetContractDetailsAsync(string conid, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<SecurityDefinitionInfo>>> GetSecurityDefinitionInfoAsync(string conid, string sectype, string month, string? exchange = null, string? strike = null, string? right = null, string? issuerId = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<OptionStrikes>> GetOptionStrikesAsync(string conid, string sectype, string month, string? exchange = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<TradingRules>> GetTradingRulesAsync(TradingRulesRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<SecurityDefinitionResponse>> GetSecurityDefinitionsByConidAsync(string conids, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<ExchangeConid>>> GetAllConidsByExchangeAsync(string exchange, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Dictionary<string, List<FutureContract>>>> GetFuturesBySymbolAsync(string symbols, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Dictionary<string, List<StockContract>>>> GetStocksBySymbolAsync(string symbols, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<TradingSchedule>>> GetTradingScheduleAsync(string assetClass, string symbol, string conid, string? exchange = null, string? exchangeFilter = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Dictionary<string, List<CurrencyPair>>>> GetCurrencyPairsAsync(string currency, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<ExchangeRateResponse>> GetExchangeRateAsync(string source, string target, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private class FakeOrderOperations : IOrderOperations
    {
        public Task<Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>> PlaceOrderAsync(string accountId, OrderRequest order, CancellationToken ct = default) => Task.FromResult(Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>.Success(new OrderSubmitted("1", "Submitted")));
        public Task<Result<CancelOrderResponse>> CancelOrderAsync(string accountId, string orderId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<LiveOrder>>> GetLiveOrdersAsync(CancellationToken ct = default) => Task.FromResult(Result<List<LiveOrder>>.Success([]));
        public Task<Result<List<Trade>>> GetTradesAsync(CancellationToken ct = default) => Task.FromResult(Result<List<Trade>>.Success([]));
        public Task<Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>> ModifyOrderAsync(string accountId, string orderId, OrderRequest order, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>> ReplyAsync(string replyId, bool confirmed, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<WhatIfResponse>> WhatIfOrderAsync(string accountId, OrderRequest order, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<OrderStatus>> GetOrderStatusAsync(string orderId, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private class FakeMarketDataOperations : IMarketDataOperations
    {
        public Task<Result<List<MarketDataSnapshot>>> GetSnapshotAsync(int[] conids, string[] fields, CancellationToken ct = default) => Task.FromResult(Result<List<MarketDataSnapshot>>.Success([]));
        public Task<Result<HistoricalDataResponse>> GetHistoryAsync(int conid, string period, string bar, bool? outsideRth = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<MarketDataSnapshot>> GetRegulatorySnapshotAsync(int conid, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<UnsubscribeResponse>> UnsubscribeAsync(int conid, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<UnsubscribeAllResponse>> UnsubscribeAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<ScannerResponse>> RunScannerAsync(ScannerRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<ScannerParameters>> GetScannerParametersAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<HmdsScannerResponse>> RunHmdsScannerAsync(HmdsScannerRequest request, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private class FakeStreamingOperations : IStreamingOperations
    {
        public IObservable<MarketDataTick> MarketData(int conid, string[] fields, CancellationToken ct = default) => throw new NotImplementedException();
        public IObservable<OrderUpdate> OrderUpdates(int? days = null, CancellationToken ct = default) => throw new NotImplementedException();
        public IObservable<PnlUpdate> ProfitAndLoss(CancellationToken ct = default) => throw new NotImplementedException();
        public IObservable<AccountSummaryUpdate> AccountSummary(CancellationToken ct = default) => throw new NotImplementedException();
        public IObservable<AccountLedgerUpdate> AccountLedger(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private class FakeFlexOperations : IFlexOperations
    {
        public Task<FlexQueryResult> ExecuteQueryAsync(string queryId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<FlexQueryResult> ExecuteQueryAsync(string queryId, string fromDate, string toDate, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private class FakeAccountOperations : IAccountOperations
    {
        public Task<Result<IserverAccountsResponse>> GetAccountsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<SwitchAccountResponse>> SwitchAccountAsync(string accountId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<SignaturesAndOwnersResponse>> GetSignaturesAndOwnersAsync(string accountId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<DynamicAccountSearchResponse>> SearchDynamicAccountAsync(string pattern, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<SetDynamicAccountResponse>> SetDynamicAccountAsync(string accountId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<AccountSummaryOverview>> GetAccountSummaryAsync(string accountId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Dictionary<string, Dictionary<string, string>>>> GetAccountSummaryAvailableFundsAsync(string accountId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Dictionary<string, Dictionary<string, string>>>> GetAccountSummaryBalancesAsync(string accountId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Dictionary<string, Dictionary<string, string>>>> GetAccountSummaryMarginsAsync(string accountId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Dictionary<string, Dictionary<string, string>>>> GetAccountSummaryMarketValueAsync(string accountId, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private class FakeAlertOperations : IAlertOperations
    {
        public Task<Result<CreateAlertResponse>> CreateOrModifyAlertAsync(string accountId, CreateAlertRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<AlertSummary>>> GetAlertsAsync(string accountId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<AlertSummary>>> GetMtaAlertAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<AlertDetail>> GetAlertDetailAsync(string alertId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<AlertActivationResponse>> ActivateAlertAsync(string accountId, AlertActivationRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<DeleteAlertResponse>> DeleteAlertAsync(string accountId, string alertId, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private class FakeWatchlistOperations : IWatchlistOperations
    {
        public Task<Result<CreateWatchlistResponse>> CreateWatchlistAsync(CreateWatchlistRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<GetWatchlistsResponse>> GetWatchlistsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<WatchlistDetail>> GetWatchlistAsync(string id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<DeleteWatchlistResponse>> DeleteWatchlistAsync(string id, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private class FakeFyiOperations : IFyiOperations
    {
        public Task<Result<UnreadBulletinCountResponse>> GetUnreadCountAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<FyiSettingItem>>> GetSettingsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<FyiAcknowledgementResponse>> UpdateSettingAsync(string typecode, bool enabled, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<FyiDisclaimerResponse>> GetDisclaimerAsync(string typecode, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<FyiAcknowledgementResponse>> MarkDisclaimerReadAsync(string typecode, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<FyiDeliveryOptionsResponse>> GetDeliveryOptionsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<FyiAcknowledgementResponse>> SetEmailDeliveryAsync(bool enabled, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<FyiAcknowledgementResponse>> RegisterDeviceAsync(FyiDeviceRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<bool>> DeleteDeviceAsync(string deviceId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<FyiNotification>>> GetNotificationsAsync(string? max = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<FyiNotification>>> GetMoreNotificationsAsync(string id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<FyiNotificationReadResponse>> MarkNotificationReadAsync(string notificationId, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private class FakeSessionManager : ISessionManager
    {
        public bool Disposed { get; private set; }
        public int EnsureInitializedCallCount { get; private set; }
        public Task EnsureInitializedAsync(CancellationToken cancellationToken) { EnsureInitializedCallCount++; return Task.CompletedTask; }
        public Task ReauthenticateAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
}
