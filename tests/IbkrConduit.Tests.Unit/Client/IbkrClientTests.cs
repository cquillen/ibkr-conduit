using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Xml.Linq;
using IbkrConduit.Accounts;
using IbkrConduit.Alerts;
using IbkrConduit.Client;
using IbkrConduit.Contracts;
using IbkrConduit.Errors;
using IbkrConduit.EventContracts;
using IbkrConduit.Flex;
using IbkrConduit.Fyi;
using IbkrConduit.Health;
using IbkrConduit.MarketData;
using IbkrConduit.Orders;
using IbkrConduit.Portfolio;
using IbkrConduit.Session;
using IbkrConduit.Streaming;
using IbkrConduit.Watchlists;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    public async Task GetHealthStatusAsync_DelegatesToCollector()
    {
        var collector = new FakeHealthStatusCollector();
        var client = CreateClient(healthCollector: collector);

        var result = await client.GetHealthStatusAsync(
            activeProbe: true, cancellationToken: TestContext.Current.CancellationToken);

        collector.CallCount.ShouldBe(1);
        collector.LastActiveProbe.ShouldBe(true);
        result.OverallStatus.ShouldBe(HealthState.Healthy);
    }

    [Fact]
    public async Task ValidateConnectionAsync_DelegatesToSessionManager()
    {
        var sessionManager = new FakeSessionManager();
        var client = CreateClient(sessionManager: sessionManager);
        await client.ValidateConnectionAsync(cancellationToken: TestContext.Current.CancellationToken);
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

    [Fact]
    public async Task DisposeAsync_DisposesWebSocketClient()
    {
        // PVR-21 / §5.4: the facade now owns the full-client teardown and disposes the WebSocket
        // client, which the pre-PVR-21 facade left untouched (finding PRB-4.3).
        var webSocket = new FakeWebSocketClient();
        var client = CreateClient(webSocketClient: webSocket);

        await client.DisposeAsync();

        webSocket.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task DisposeAsync_DisposesWebSocketBeforeSessionManager()
    {
        // §5.4 ManagedTenant order: WebSocket disconnect/dispose first, then session teardown.
        var order = new List<string>();
        var webSocket = new FakeWebSocketClient(order);
        var sessionManager = new FakeSessionManager(order);
        var client = CreateClient(webSocketClient: webSocket, sessionManager: sessionManager);

        await client.DisposeAsync();

        order.ShouldBe(new[] { "websocket", "session" });
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_TearsDownEachComponentExactlyOnce()
    {
        // The atomic guard makes the teardown run exactly once even when invoked twice
        // (e.g. `await using client` plus the provider disposing the same singleton).
        var webSocket = new FakeWebSocketClient();
        var sessionManager = new FakeSessionManager();
        var client = CreateClient(webSocketClient: webSocket, sessionManager: sessionManager);

        await client.DisposeAsync();
        await client.DisposeAsync();

        webSocket.DisposeCount.ShouldBe(1);
        sessionManager.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task ValidateConnectionAsync_FlexTokenAndQueryIdConfigured_ValidatesSuccessfully()
    {
        var flex = new FakeFlexOperations
        {
            ExecuteQueryResult = Result<FlexGenericResult>.Success(
                new FlexGenericResult("TestQuery", "AF", DateTimeOffset.UtcNow, [], new XDocument()))
        };
        var options = new IbkrClientOptions
        {
            FlexToken = "fake-token",
            FlexQueries = new FlexQueryOptions { CashTransactionsQueryId = "12345" }
        };
        var client = CreateClient(flex: flex, options: options);

        await client.ValidateConnectionAsync(cancellationToken: TestContext.Current.CancellationToken);

        flex.ExecuteQueryCallCount.ShouldBe(1);
        flex.LastQueryId.ShouldBe("12345");
    }

    [Fact]
    public async Task ValidateConnectionAsync_FlexTokenInvalid_ThrowsConfigurationException()
    {
        var flex = new FakeFlexOperations
        {
            ExecuteQueryResult = Result<FlexGenericResult>.Failure(
                new IbkrFlexError(1015, "Token is invalid", false, "Token is invalid", null, null))
        };
        var options = new IbkrClientOptions
        {
            FlexToken = "bad-token",
            FlexQueries = new FlexQueryOptions { CashTransactionsQueryId = "12345" }
        };
        var client = CreateClient(flex: flex, options: options);

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => client.ValidateConnectionAsync(cancellationToken: TestContext.Current.CancellationToken));

        ex.CredentialHint.ShouldBe("FlexToken");
        ex.Message.ShouldContain("invalid");
    }

    [Fact]
    public async Task ValidateConnectionAsync_FlexTokenExpired_ThrowsConfigurationException()
    {
        var flex = new FakeFlexOperations
        {
            ExecuteQueryResult = Result<FlexGenericResult>.Failure(
                new IbkrFlexError(1012, "Token has expired", false, "Token has expired", null, null))
        };
        var options = new IbkrClientOptions
        {
            FlexToken = "expired-token",
            FlexQueries = new FlexQueryOptions { CashTransactionsQueryId = "12345" }
        };
        var client = CreateClient(flex: flex, options: options);

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => client.ValidateConnectionAsync(cancellationToken: TestContext.Current.CancellationToken));

        ex.CredentialHint.ShouldBe("FlexToken");
        ex.Message.ShouldContain("expired");
    }

    [Fact]
    public async Task ValidateConnectionAsync_FlexTokenIpRestriction_ThrowsConfigurationException()
    {
        var flex = new FakeFlexOperations
        {
            ExecuteQueryResult = Result<FlexGenericResult>.Failure(
                new IbkrFlexError(1013, "IP restriction", false, "IP restriction", null, null))
        };
        var options = new IbkrClientOptions
        {
            FlexToken = "restricted-token",
            FlexQueries = new FlexQueryOptions { CashTransactionsQueryId = "12345" }
        };
        var client = CreateClient(flex: flex, options: options);

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => client.ValidateConnectionAsync(cancellationToken: TestContext.Current.CancellationToken));

        ex.CredentialHint.ShouldBe("FlexToken");
        ex.Message.ShouldContain("IP restriction");
    }

    [Fact]
    public async Task ValidateConnectionAsync_FlexTokenOkButQueryFails_DoesNotThrow()
    {
        var flex = new FakeFlexOperations
        {
            ExecuteQueryResult = Result<FlexGenericResult>.Failure(
                new IbkrFlexError(1014, "Query is invalid", false, "Query is invalid", null, null))
        };
        var options = new IbkrClientOptions
        {
            FlexToken = "good-token",
            FlexQueries = new FlexQueryOptions { CashTransactionsQueryId = "12345" }
        };
        var client = CreateClient(flex: flex, options: options);

        await client.ValidateConnectionAsync(cancellationToken: TestContext.Current.CancellationToken);

        flex.ExecuteQueryCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task ValidateConnectionAsync_FlexNotConfigured_SkipsValidation()
    {
        var flex = new FakeFlexOperations();
        var options = new IbkrClientOptions(); // No FlexToken
        var client = CreateClient(flex: flex, options: options);

        await client.ValidateConnectionAsync(cancellationToken: TestContext.Current.CancellationToken);

        flex.ExecuteQueryCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateConnectionAsync_FlexTokenButNoQueryIds_SkipsValidation()
    {
        var flex = new FakeFlexOperations();
        var options = new IbkrClientOptions
        {
            FlexToken = "good-token",
            FlexQueries = new FlexQueryOptions() // No query IDs
        };
        var client = CreateClient(flex: flex, options: options);

        await client.ValidateConnectionAsync(cancellationToken: TestContext.Current.CancellationToken);

        flex.ExecuteQueryCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateConnectionAsync_ValidateFlexFalse_SkipsValidation()
    {
        var flex = new FakeFlexOperations();
        var options = new IbkrClientOptions
        {
            FlexToken = "good-token",
            FlexQueries = new FlexQueryOptions { CashTransactionsQueryId = "12345" }
        };
        var client = CreateClient(flex: flex, options: options);

        await client.ValidateConnectionAsync(validateFlex: false, cancellationToken: TestContext.Current.CancellationToken);

        flex.ExecuteQueryCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateConnectionAsync_TransportError_ThrowsTransientException()
    {
        // ERR-2 (breaking-behavioral): a transient TRANSPORT failure reaching the Flex Web Service is no
        // longer misclassified as a FlexToken configuration problem — it classifies truthfully as
        // transient (wait-and-retry), distinct from IbkrConfigurationException.
        var flex = new FakeFlexOperations
        {
            ExecuteQueryResult = Result<FlexGenericResult>.Failure(
                new IbkrApiError(null, "Connection refused", null, null))
        };
        var options = new IbkrClientOptions
        {
            FlexToken = "good-token",
            FlexQueries = new FlexQueryOptions { CashTransactionsQueryId = "12345" }
        };
        var client = CreateClient(flex: flex, options: options);

        var ex = await Should.ThrowAsync<IbkrTransientException>(
            () => client.ValidateConnectionAsync(cancellationToken: TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("transient");
    }

    [Fact]
    public async Task ValidateConnectionAsync_TransportErrorUnderThrowOnApiError_ThrowsTransientException()
    {
        // ERR-2: under ThrowOnApiError=true the transport failure arrives as a thrown IbkrApiException
        // (EnsureSuccess) rather than a returned failure — it must STILL classify as transient, not leak
        // the raw IbkrApiException nor be recast as a token problem.
        var flex = new FakeFlexOperations
        {
            ExecuteQueryException = new IbkrApiException(
                new IbkrApiError(null, "Connection refused", null, null))
        };
        var options = new IbkrClientOptions
        {
            FlexToken = "good-token",
            FlexQueries = new FlexQueryOptions { CashTransactionsQueryId = "12345" }
        };
        var client = CreateClient(flex: flex, options: options);

        var ex = await Should.ThrowAsync<IbkrTransientException>(
            () => client.ValidateConnectionAsync(cancellationToken: TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("transient");
    }

    [Theory]
    [InlineData(1015, "invalid")]
    [InlineData(1012, "expired")]
    [InlineData(1013, "IP restriction")]
    public async Task ValidateConnectionAsync_FlexTokenErrorUnderThrowOnApiError_MapsToConfigurationException(
        int errorCode, string expectedMessageFragment)
    {
        // ERR-2 (both throw settings): under ThrowOnApiError=true ExecuteQueryAsync THROWS
        // IbkrApiException(IbkrFlexError) instead of returning a failed Result. The 1012/1013/1015 token
        // mapping must still apply — the friendly IbkrConfigurationException is produced, not the raw
        // IbkrApiException. The ThrowOnApiError=false path is covered by the sibling *_Throws* tests.
        var flex = new FakeFlexOperations
        {
            ExecuteQueryException = new IbkrApiException(
                new IbkrFlexError(errorCode, "Token error", false, "Token error", null, null))
        };
        var options = new IbkrClientOptions
        {
            FlexToken = "bad-token",
            FlexQueries = new FlexQueryOptions { CashTransactionsQueryId = "12345" }
        };
        var client = CreateClient(flex: flex, options: options);

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => client.ValidateConnectionAsync(cancellationToken: TestContext.Current.CancellationToken));

        ex.CredentialHint.ShouldBe("FlexToken");
        ex.Message.ShouldContain(expectedMessageFragment);
    }

    [Fact]
    public async Task ValidateConnectionAsync_FlexQueryErrorUnderThrowOnApiError_DoesNotThrow()
    {
        // ERR-2: a non-token Flex query error (token appears valid) is logged and swallowed even when it
        // arrives as a thrown IbkrApiException under ThrowOnApiError=true — parity with the returned-Result
        // path (ValidateConnectionAsync_FlexTokenOkButQueryFails_DoesNotThrow).
        var flex = new FakeFlexOperations
        {
            ExecuteQueryException = new IbkrApiException(
                new IbkrFlexError(1014, "Query is invalid", false, "Query is invalid", null, null))
        };
        var options = new IbkrClientOptions
        {
            FlexToken = "good-token",
            FlexQueries = new FlexQueryOptions { CashTransactionsQueryId = "12345" }
        };
        var client = CreateClient(flex: flex, options: options);

        await client.ValidateConnectionAsync(cancellationToken: TestContext.Current.CancellationToken);

        flex.ExecuteQueryCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task ValidateConnectionAsync_FallsBackToTradeConfirmationsQueryId()
    {
        var flex = new FakeFlexOperations
        {
            ExecuteQueryResult = Result<FlexGenericResult>.Success(
                new FlexGenericResult("TestQuery", "TCF", DateTimeOffset.UtcNow, [], new XDocument()))
        };
        var options = new IbkrClientOptions
        {
            FlexToken = "good-token",
            FlexQueries = new FlexQueryOptions { TradeConfirmationsQueryId = "67890" }
        };
        var client = CreateClient(flex: flex, options: options);

        await client.ValidateConnectionAsync(cancellationToken: TestContext.Current.CancellationToken);

        flex.ExecuteQueryCallCount.ShouldBe(1);
        flex.LastQueryId.ShouldBe("67890");
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
        IEventContractOperations? eventContracts = null,
        IHealthStatusCollector? healthCollector = null,
        ISessionManager? sessionManager = null,
        IIbkrWebSocketClient? webSocketClient = null,
        IbkrClientOptions? options = null) =>
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
            eventContracts ?? new FakeEventContractOperations(),
            healthCollector ?? new FakeHealthStatusCollector(),
            sessionManager ?? new FakeSessionManager(),
            webSocketClient ?? new FakeWebSocketClient(),
            options ?? new IbkrClientOptions(),
            NullLogger<IbkrClient>.Instance);

    private class FakePortfolioOperations : IPortfolioOperations
    {
        public Task<Result<List<Account>>> GetAccountsAsync(CancellationToken ct = default) => Task.FromResult(Result<List<Account>>.Success([]));
        public Task<Result<List<Position>>> GetPositionsAsync(string accountId, int page = 0, bool? waitForSecDef = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Dictionary<string, AccountSummaryEntry>>> GetAccountSummaryAsync(string accountId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Dictionary<string, LedgerEntry>>> GetLedgerAsync(string accountId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<AccountInfo>> GetAccountInfoAsync(string accountId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<AccountAllocation>> GetAccountAllocationAsync(string accountId, string? model = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<Position>>> GetPositionByConidAsync(string accountId, string conid, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<PositionContractInfo>> GetPositionAndContractInfoAsync(string conid, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<bool>> InvalidatePortfolioCacheAsync(string accountId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<AccountPerformance>> GetAccountPerformanceAsync(List<string> accountIds, PerformancePeriod period, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<TransactionHistory>> GetTransactionHistoryAsync(List<string> accountIds, List<string> conids, string currency, int? days = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<AccountAllocation>> GetConsolidatedAllocationAsync(List<string> accountIds, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<ComboPosition>>> GetComboPositionsAsync(string accountId, bool? nocache = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<Position>>> GetRealTimePositionsAsync(string accountId, string? model = null, string? sort = null, IbkrConduit.Portfolio.SortDirection? direction = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<SubAccount>>> GetSubAccountsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<SubAccountsPage>> GetSubAccountsPagedAsync(int page = 0, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<AllPeriodsPerformance>> GetAllPeriodsPerformanceAsync(List<string> accountIds, string? param = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<PartitionedPnl>> GetPartitionedPnlAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private class FakeContractOperations : IContractOperations
    {
        public Task<Result<List<ContractSearchResult>>> SearchBySymbolAsync(string symbol, SecurityType? secType = null, bool? name = null, bool? more = null, bool? fund = null, string? fundFamilyConidEx = null, bool? pattern = null, string? referrer = null, CancellationToken ct = default) => Task.FromResult(Result<List<ContractSearchResult>>.Success([]));
        public Task<Result<ContractDetails>> GetContractDetailsAsync(string conid, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<SecurityDefinitionInfo>>> GetSecurityDefinitionInfoAsync(string conid, SecurityType sectype, ExpiryMonth month, string? exchange = null, decimal? strike = null, OptionRight? right = null, string? issuerId = null, string? filters = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<OptionStrikes>> GetOptionStrikesAsync(string conid, SecurityType sectype, ExpiryMonth month, string? exchange = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<TradingRules>> GetTradingRulesAsync(TradingRulesRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<SecurityDefinitionResponse>> GetSecurityDefinitionsByConidAsync(string conids, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<ExchangeConid>>> GetAllConidsByExchangeAsync(string exchange, string? assetClass = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Dictionary<string, List<FutureContract>>>> GetFuturesBySymbolAsync(string symbols, string? exchange = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Dictionary<string, List<StockContract>>>> GetStocksBySymbolAsync(string symbols, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<TradingSchedule>>> GetTradingScheduleAsync(string assetClass, string symbol, string conid, string? exchange = null, string? exchangeFilter = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<Dictionary<string, List<CurrencyPair>>>> GetCurrencyPairsAsync(string currency, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<ExchangeRateResponse>> GetExchangeRateAsync(string source, string target, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<ContractInfoAndRules>> GetContractInfoAndRulesAsync(string conid, bool? isBuy = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<AlgoListResponse>> GetAlgosAsync(string conid, string? algos = null, int? addDescription = null, int? addParams = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<BondFilterResponse>> GetBondFiltersAsync(string symbol, string issuerId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<ContractSearchResult>>> SearchBySymbolPostAsync(ContractSearchRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<TradingScheduleResponse>> GetTradingScheduleNewAsync(string conid, string? exchange = null, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private class FakeOrderOperations : IOrderOperations
    {
        public Task<Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>> PlaceOrderAsync(string accountId, OrderRequest order, CancellationToken ct = default) => Task.FromResult(Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>.Success(new OrderSubmitted("1", "Submitted")));
        public Task<Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>> PlaceOrdersAsync(string accountId, IReadOnlyList<OrderRequest> orders, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<CancelOrderResponse>> CancelOrderAsync(string accountId, string orderId, string? extOperator = null, bool? manualIndicator = null, DateTimeOffset? manualCancelTime = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<LiveOrdersSnapshot>> GetLiveOrdersAsync(OrderStatusFilter[]? filters = null, bool? force = null, CancellationToken ct = default) => Task.FromResult(Result<LiveOrdersSnapshot>.Success(new LiveOrdersSnapshot([], true)));
        public Task<Result<List<Trade>>> GetTradesAsync(int? days = null, CancellationToken ct = default) => Task.FromResult(Result<List<Trade>>.Success([]));
        public Task<Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>> ModifyOrderAsync(string accountId, string orderId, OrderRequest order, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>> ReplyAsync(string replyId, bool confirmed, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<WhatIfResponse>> WhatIfOrderAsync(string accountId, OrderRequest order, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<OrderStatus>> GetOrderStatusAsync(string orderId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<string>> DismissNotificationAsync(int orderId, string reqId, string text, CancellationToken ct = default) =>
            Task.FromResult(Result<string>.Success("ok"));
    }

    private class FakeMarketDataOperations : IMarketDataOperations
    {
        public Task<Result<List<MarketDataSnapshot>>> GetSnapshotAsync(int[] conids, string[] fields, CancellationToken ct = default) => Task.FromResult(Result<List<MarketDataSnapshot>>.Success([]));
        public Task<Result<HistoricalDataResponse>> GetHistoryAsync(int conid, HistoryPeriod period, BarSize bar, bool? outsideRth = null, string? exchange = null, DateTimeOffset? startTime = null, HistoryDirection? direction = null, HistoryBarSource? source = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<MarketDataSnapshot>> GetRegulatorySnapshotAsync(int conid, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<UnsubscribeResponse>> UnsubscribeAsync(int conid, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<UnsubscribeAllResponse>> UnsubscribeAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<ScannerResponse>> RunScannerAsync(ScannerRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<ScannerParameters>> GetScannerParametersAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<HmdsScannerResponse>> RunHmdsScannerAsync(HmdsScannerRequest request, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private class FakeStreamingOperations : IStreamingOperations
    {
        public Task ConnectAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public bool IsConnected => false;
        public DateTimeOffset? LastMessageReceivedAt => null;
        public IIbkrSubscription<ConnectionEvent> SubscribeConnectionEvents() => EmptySubscription<ConnectionEvent>();
        public IIbkrSubscription<SessionStatusEvent> SubscribeSessionStatus() => EmptySubscription<SessionStatusEvent>();
        public IIbkrSubscription<BulletinEvent> SubscribeBulletins() => EmptySubscription<BulletinEvent>();
        public IIbkrSubscription<NotificationEvent> SubscribeTradingNotifications() => EmptySubscription<NotificationEvent>();
        public IIbkrSubscription<SystemEvent> SubscribeSystemEvents() => EmptySubscription<SystemEvent>();
        public IIbkrSubscription<AccountStatusEvent> SubscribeAccountStatus() => EmptySubscription<AccountStatusEvent>();
        public Task<IIbkrSubscription<MarketDataTick>> MarketDataAsync(int conid, string[] fields, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IIbkrSubscription<OrderUpdate>> OrderUpdatesAsync(int? days = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IIbkrSubscription<TradeExecution>> TradeExecutionsAsync(bool? realtimeUpdatesOnly = null, int? days = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IIbkrSubscription<PnlUpdate>> ProfitAndLossAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IIbkrSubscription<AccountSummaryUpdate>> AccountSummaryAsync(string accountId, string[]? keys = null, string[]? fields = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IIbkrSubscription<AccountLedgerUpdate>> AccountLedgerAsync(string accountId, string[]? keys = null, string[]? fields = null, CancellationToken ct = default) => throw new NotImplementedException();

        private static IIbkrSubscription<T> EmptySubscription<T>() =>
            new IbkrSubscription<T>(new EmptyObservable<T>(), _ => ValueTask.CompletedTask);
    }

    private sealed class EmptyObservable<T> : IObservable<T>
    {
        public IDisposable Subscribe(IObserver<T> observer) =>
            new EmptyDisposable();

        private sealed class EmptyDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }

    private class FakeFlexOperations : IFlexOperations
    {
        public Result<FlexGenericResult>? ExecuteQueryResult { get; set; }

        /// <summary>
        /// When set, <see cref="ExecuteQueryAsync(string, CancellationToken)"/> throws this exception —
        /// faithfully simulating <c>FlexOperations</c> under <c>ThrowOnApiError=true</c>, where
        /// <c>EnsureSuccess()</c> throws an <see cref="IbkrApiException"/> instead of returning a failed
        /// Result. Lets ERR-2 assert the classification holds under BOTH throw settings.
        /// </summary>
        public Exception? ExecuteQueryException { get; set; }

        public int ExecuteQueryCallCount { get; private set; }
        public string? LastQueryId { get; private set; }

        public Task<Result<CashTransactionsFlexResult>> GetCashTransactionsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<TradeConfirmationsFlexResult>> GetTradeConfirmationsAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default) => throw new NotImplementedException();

        public Task<Result<FlexGenericResult>> ExecuteQueryAsync(string queryId, CancellationToken ct = default)
        {
            ExecuteQueryCallCount++;
            LastQueryId = queryId;
            if (ExecuteQueryException is not null)
            {
                throw ExecuteQueryException;
            }

            return Task.FromResult(ExecuteQueryResult ?? throw new NotImplementedException("ExecuteQueryResult not configured"));
        }

        public Task<Result<FlexGenericResult>> ExecuteQueryAsync(string queryId, string fromDate, string toDate, CancellationToken ct = default) => throw new NotImplementedException();
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

    private class FakeEventContractOperations : IEventContractOperations
    {
        public Task<Result<EventContractCategoryTreeResponse>> GetCategoryTreeAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<EventContractMarketResponse>> GetMarketAsync(int underlyingConid, string? exchange = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<EventContractRulesResponse>> GetContractRulesAsync(int conid, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<EventContractDetailsResponse>> GetContractDetailsAsync(int conid, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<EventContractSchedulesResponse>> GetContractSchedulesAsync(int conid, CancellationToken ct = default) => throw new NotImplementedException();
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
        public Task<Result<List<FyiNotification>>> GetNotificationsAsync(int? max = null, string? include = null, string? exclude = null, string? id = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<FyiNotification>>> GetMoreNotificationsAsync(string id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<FyiNotificationReadResponse>> MarkNotificationReadAsync(string notificationId, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private class FakeHealthStatusCollector : IHealthStatusCollector
    {
        public IbkrHealthStatus? ResultToReturn { get; set; }
        public int CallCount { get; private set; }
        public bool? LastActiveProbe { get; private set; }

        public Task<IbkrHealthStatus> GetHealthStatusAsync(
            bool activeProbe = false, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastActiveProbe = activeProbe;
            return Task.FromResult(ResultToReturn ?? new IbkrHealthStatus
            {
                OverallStatus = HealthState.Healthy,
                Session = new BrokerageSessionHealth(true, true, false, true, null),
                Streaming = null,
                Token = new OAuthTokenHealth(false, TimeSpan.FromHours(1)),
                RateLimiter = new RateLimiterHealth(10, 10, 0, 0),
                LastSuccessfulCall = DateTimeOffset.UtcNow,
                CheckedAt = DateTimeOffset.UtcNow,
            });
        }
    }

    private class FakeSessionManager : ISessionManager
    {
        private readonly List<string>? _teardownOrder;

        public FakeSessionManager(List<string>? teardownOrder = null) => _teardownOrder = teardownOrder;

        public bool Disposed { get; private set; }
        public int DisposeCount { get; private set; }
        public int EnsureInitializedCallCount { get; private set; }
        public bool SessionEstablished => true;
        public Task EnsureInitializedAsync(CancellationToken cancellationToken) { EnsureInitializedCallCount++; return Task.CompletedTask; }
        public Task ReauthenticateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            DisposeCount++;
            _teardownOrder?.Add("session");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeWebSocketClient : IIbkrWebSocketClient
    {
        private readonly List<string>? _teardownOrder;

        public FakeWebSocketClient(List<string>? teardownOrder = null) => _teardownOrder = teardownOrder;

        public int DisposeCount { get; private set; }
        public bool IsConnected => false;
        public int ActiveSubscriptionCount => 0;
        public DateTimeOffset? LastMessageReceivedAt => null;

        public Task ConnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<(ChannelReader<JsonElement> Reader, Func<CancellationToken, ValueTask> Unsubscribe)> SubscribeTopicAsync(
            string subscribeMessage, string topicPrefix, string? cancelMessage, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public (ChannelReader<JsonElement> Reader, Func<CancellationToken, ValueTask> Unsubscribe) RegisterUnsolicitedTopic(string topicPrefix) =>
            throw new NotImplementedException();

        public (ChannelReader<ConnectionEvent> Reader, Func<CancellationToken, ValueTask> Unsubscribe) RegisterConnectionEvents() =>
            throw new NotImplementedException();

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            _teardownOrder?.Add("websocket");
            return ValueTask.CompletedTask;
        }
    }
}
