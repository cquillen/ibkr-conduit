using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Streaming;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class StreamingOperationsTests
{
    [Fact]
    public async Task MarketDataAsync_BuildsCorrectTopicAndCancelMessage()
    {
        var (ops, wsClient) = CreateOperations();

        await ops.MarketDataAsync(265598, new[] { "31", "84", "86" }, TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("smd+265598+{\"fields\":[\"31\",\"84\",\"86\"]}");
        wsClient.LastTopicPrefix.ShouldBe("smd");
        wsClient.LastCancelMessage.ShouldBe("umd+265598+{}");
    }

    [Fact]
    public async Task OrderUpdatesAsync_WithoutDays_BuildsCorrectTopicAndCancelMessage()
    {
        var (ops, wsClient) = CreateOperations();

        await ops.OrderUpdatesAsync(cancellationToken: TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("sor+{}");
        wsClient.LastTopicPrefix.ShouldBe("sor");
        wsClient.LastCancelMessage.ShouldBe("uor+{}");
    }

    [Fact]
    public async Task OrderUpdatesAsync_WithDays_BuildsCorrectTopicAndCancelMessage()
    {
        var (ops, wsClient) = CreateOperations();

        await ops.OrderUpdatesAsync(days: 3, cancellationToken: TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("sor+{\"days\":3}");
        wsClient.LastTopicPrefix.ShouldBe("sor");
        wsClient.LastCancelMessage.ShouldBe("uor+{}");
    }

    [Fact]
    public async Task TradeExecutionsAsync_NoArgs_BuildsCorrectTopicMessage()
    {
        var (ops, wsClient) = CreateOperations();

        await ops.TradeExecutionsAsync(cancellationToken: TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("str+{}");
        wsClient.LastTopicPrefix.ShouldBe("str");
        wsClient.LastCancelMessage.ShouldBe("utr");
    }

    [Fact]
    public async Task TradeExecutionsAsync_RealtimeOnly_BuildsCorrectTopicMessage()
    {
        var (ops, wsClient) = CreateOperations();

        await ops.TradeExecutionsAsync(realtimeUpdatesOnly: true, cancellationToken: TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("str+{\"realtimeUpdatesOnly\":true}");
        wsClient.LastTopicPrefix.ShouldBe("str");
        wsClient.LastCancelMessage.ShouldBe("utr");
    }

    [Fact]
    public async Task TradeExecutionsAsync_RealtimeOnlyFalse_BuildsCorrectTopicMessage()
    {
        var (ops, wsClient) = CreateOperations();

        await ops.TradeExecutionsAsync(realtimeUpdatesOnly: false, cancellationToken: TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("str+{\"realtimeUpdatesOnly\":false}");
        wsClient.LastTopicPrefix.ShouldBe("str");
        wsClient.LastCancelMessage.ShouldBe("utr");
    }

    [Fact]
    public async Task TradeExecutionsAsync_WithDays_BuildsCorrectTopicMessage()
    {
        var (ops, wsClient) = CreateOperations();

        await ops.TradeExecutionsAsync(days: 3, cancellationToken: TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("str+{\"days\":3}");
        wsClient.LastTopicPrefix.ShouldBe("str");
        wsClient.LastCancelMessage.ShouldBe("utr");
    }

    [Fact]
    public async Task TradeExecutionsAsync_RealtimeOnlyAndDays_BuildsCorrectTopicMessage()
    {
        var (ops, wsClient) = CreateOperations();

        await ops.TradeExecutionsAsync(realtimeUpdatesOnly: true, days: 3, cancellationToken: TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("str+{\"realtimeUpdatesOnly\":true,\"days\":3}");
        wsClient.LastTopicPrefix.ShouldBe("str");
        wsClient.LastCancelMessage.ShouldBe("utr");
    }

    [Fact]
    public async Task TradeExecutionsAsync_FrameWithMultipleExecutions_EmitsOnePerExecution()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var sub = await ops.TradeExecutionsAsync(cancellationToken: ct);
        var received = new System.Collections.Generic.List<TradeExecution>();
        var done = new TaskCompletionSource();
        using var s = sub.Stream.Subscribe(new TestObserver<TradeExecution>(
            onNext: e =>
            {
                received.Add(e);
                if (received.Count == 2)
                {
                    done.TrySetResult();
                }
            }));

        var json = JsonDocument.Parse("""
            {"topic":"str","args":[
              {"execution_id":"e1","symbol":"AAPL","price":"150.25","size":100,"conid":265598},
              {"execution_id":"e2","symbol":"MSFT","price":"420.10","size":50,"conid":272093}
            ]}
            """).RootElement;
        await wsClient.Channel.Writer.WriteAsync(json, ct);

        await done.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        received.Count.ShouldBe(2);
        received[0].ExecutionId.ShouldBe("e1");
        received[0].Price.ShouldBe(150.25m);
        received[1].Symbol.ShouldBe("MSFT");
    }

    [Fact]
    public async Task TradeExecutionsAsync_FrameWithNoArgs_EmitsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var sub = await ops.TradeExecutionsAsync(cancellationToken: ct);
        var got = new TaskCompletionSource<TradeExecution>();
        using var s = sub.Stream.Subscribe(new TestObserver<TradeExecution>(
            onNext: e => got.TrySetResult(e)));

        // A no-args str frame, then a valid one — only the valid execution should arrive.
        await wsClient.Channel.Writer.WriteAsync(
            JsonDocument.Parse("""{"topic":"str"}""").RootElement, ct);
        await wsClient.Channel.Writer.WriteAsync(
            JsonDocument.Parse("""{"topic":"str","args":[{"execution_id":"only"}]}""").RootElement, ct);

        var execution = await got.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        execution.ExecutionId.ShouldBe("only");
    }

    [Fact]
    public async Task ProfitAndLossAsync_BuildsCorrectTopicAndCancelMessage()
    {
        var (ops, wsClient) = CreateOperations();

        await ops.ProfitAndLossAsync(TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("spl+{}");
        wsClient.LastTopicPrefix.ShouldBe("spl");
        wsClient.LastCancelMessage.ShouldBe("upl+{}");
    }

    [Fact]
    public async Task AccountSummaryAsync_BuildsCorrectTopicAndCancelMessage()
    {
        var (ops, wsClient) = CreateOperations();

        await ops.AccountSummaryAsync("DU1234567", cancellationToken: TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("ssd+DU1234567+{}");
        wsClient.LastTopicPrefix.ShouldBe("ssd");
        wsClient.LastCancelMessage.ShouldBe("usd+DU1234567+{}");
    }

    [Fact]
    public async Task AccountSummaryAsync_WithKeysAndFields_BuildsFilteredArgs()
    {
        var (ops, wsClient) = CreateOperations();

        await ops.AccountSummaryAsync("DU1234567",
            keys: new[] { "AccruedCash-S", "ExcessLiquidity-S" },
            fields: new[] { "currency", "monetaryValue" },
            cancellationToken: TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe(
            "ssd+DU1234567+{\"keys\":[\"AccruedCash-S\",\"ExcessLiquidity-S\"],\"fields\":[\"currency\",\"monetaryValue\"]}");
    }

    [Fact]
    public async Task AccountSummaryAsync_WithFieldsOnly_BuildsFilteredArgs()
    {
        var (ops, wsClient) = CreateOperations();

        await ops.AccountSummaryAsync("DU1234567",
            fields: new[] { "currency", "monetaryValue" },
            cancellationToken: TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("ssd+DU1234567+{\"fields\":[\"currency\",\"monetaryValue\"]}");
    }

    [Fact]
    public async Task AccountLedgerAsync_BuildsCorrectTopicAndCancelMessage()
    {
        var (ops, wsClient) = CreateOperations();

        await ops.AccountLedgerAsync("DU1234567", cancellationToken: TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("sld+DU1234567+{}");
        wsClient.LastTopicPrefix.ShouldBe("sld");
        wsClient.LastCancelMessage.ShouldBe("uld+DU1234567+{}");
    }

    [Fact]
    public async Task AccountLedgerAsync_WithKeys_BuildsFilteredArgs()
    {
        var (ops, wsClient) = CreateOperations();

        await ops.AccountLedgerAsync("DU1234567",
            keys: new[] { "LedgerListUSD" },
            cancellationToken: TestContext.Current.CancellationToken);

        wsClient.LastSubscribeMessage.ShouldBe("sld+DU1234567+{\"keys\":[\"LedgerListUSD\"]}");
    }

    [Fact]
    public async Task MarketDataAsync_MapperExtractsFieldsFromJson()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var sub = await ops.MarketDataAsync(265598, new[] { "31" }, ct);
        var received = new TaskCompletionSource<MarketDataTick>();
        using var s = sub.Stream.Subscribe(new TestObserver<MarketDataTick>(
            onNext: t => received.TrySetResult(t)));

        var json = JsonDocument.Parse("""{"topic":"smd+265598","conid":265598,"_updated":1234567890,"31":"456.78"}""").RootElement;
        await wsClient.Channel.Writer.WriteAsync(json, ct);

        var tick = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        tick.Conid.ShouldBe(265598);
        tick.Updated.ShouldBe(1234567890);
        tick.Fields.ShouldNotBeNull();
        tick.Fields!["31"].ShouldBe("456.78");
    }

    [Fact]
    public async Task OrderUpdatesAsync_MapperDeserializesJson()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var sub = await ops.OrderUpdatesAsync(cancellationToken: ct);
        var received = new TaskCompletionSource<OrderUpdate>();
        using var s = sub.Stream.Subscribe(new TestObserver<OrderUpdate>(
            onNext: o => received.TrySetResult(o)));

        // Real sor frames wrap order(s) in an args array.
        var json = JsonDocument.Parse("""{"topic":"sor","args":[{"orderId":"123","conid":265598,"symbol":"AAPL","side":"BUY","size":100,"orderType":"LMT","price":150.0,"status":"Filled","filledQuantity":100,"remainingQuantity":0}]}""").RootElement;
        await wsClient.Channel.Writer.WriteAsync(json, ct);

        var order = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        order.OrderId.ShouldBe("123");
        order.Symbol.ShouldBe("AAPL");
        order.Status.ShouldBe("Filled");
    }

    [Fact]
    public async Task OrderUpdatesAsync_NumericOrderId_CoercedToString()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var sub = await ops.OrderUpdatesAsync(cancellationToken: ct);
        var received = new TaskCompletionSource<OrderUpdate>();
        using var s = sub.Stream.Subscribe(new TestObserver<OrderUpdate>(
            onNext: o => received.TrySetResult(o)));

        var json = JsonDocument.Parse(
            """{"topic":"sor","args":[{"acct":"DUO873728","conidex":"756733","conid":756733,"orderId":656804954,"isEventTrading":"0"}]}""").RootElement;
        await wsClient.Channel.Writer.WriteAsync(json, ct);

        var order = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        order.OrderId.ShouldBe("656804954");
        order.Conid.ShouldBe(756733);
    }

    [Fact]
    public async Task ProfitAndLossAsync_MapperDeserializesJson()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var sub = await ops.ProfitAndLossAsync(ct);
        var received = new TaskCompletionSource<PnlUpdate>();
        using var s = sub.Stream.Subscribe(new TestObserver<PnlUpdate>(
            onNext: p => received.TrySetResult(p)));

        // Real spl frames key args by "{account}.Core" — the account lives in the key.
        var json = JsonDocument.Parse("""{"topic":"spl","args":{"DU123.Core":{"dpl":100.50,"upl":200.25,"rpl":50.75,"nl":50000.0}}}""").RootElement;
        await wsClient.Channel.Writer.WriteAsync(json, ct);

        var pnl = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        pnl.AccountId.ShouldBe("DU123");
        pnl.DailyPnl.ShouldBe(100.50m);
        pnl.NetLiquidation.ShouldBe(50000.0m);
    }

    [Fact]
    public async Task AccountSummaryAsync_MapperReadsResultArrayAndTopicAccountId()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var sub = await ops.AccountSummaryAsync("DUO873728", cancellationToken: ct);
        var received = new TaskCompletionSource<AccountSummaryUpdate>();
        using var s = sub.Stream.Subscribe(new TestObserver<AccountSummaryUpdate>(
            onNext: u => received.TrySetResult(u)));

        var json = JsonDocument.Parse(
            """{"result":[{"key":"ExcessLiquidity-S","currency":"USD","monetaryValue":1005353.88,"severity":0,"timestamp":1783031080}],"topic":"ssd+DUO873728"}""").RootElement;
        await wsClient.Channel.Writer.WriteAsync(json, ct);

        var update = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        update.AccountId.ShouldBe("DUO873728");
        update.Result.Count.ShouldBe(1);
        update.Result[0].Key.ShouldBe("ExcessLiquidity-S");
        update.Result[0].MonetaryValue.ShouldBe(1005353.88m);
    }

    [Fact]
    public async Task AccountLedgerAsync_MapperReadsResultArrayAndTopicAccountId()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var sub = await ops.AccountLedgerAsync("DUO873728", cancellationToken: ct);
        var received = new TaskCompletionSource<AccountLedgerUpdate>();
        using var s = sub.Stream.Subscribe(new TestObserver<AccountLedgerUpdate>(
            onNext: u => received.TrySetResult(u)));

        var json = JsonDocument.Parse(
            """{"result":[{"acctCode":"DUO873728","cashbalance":976920.88,"key":"LedgerListBASE","netLiquidationValue":1017353.16,"unrealizedPnl":4598.91,"secondKey":"BASE","settledCash":976920.88,"timestamp":1783031080}],"topic":"sld+DUO873728"}""").RootElement;
        await wsClient.Channel.Writer.WriteAsync(json, ct);

        var update = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        update.AccountId.ShouldBe("DUO873728");
        update.Result.Count.ShouldBe(1);
        update.Result[0].Key.ShouldBe("LedgerListBASE");
        update.Result[0].CashBalance.ShouldBe(976920.88m);
        update.Result[0].UnrealizedPnl.ShouldBe(4598.91m);
    }

    [Fact]
    public async Task ConnectAsync_DelegatesToWebSocketClient()
    {
        var (ops, wsClient) = CreateOperations();

        await ((IStreamingOperations)ops).ConnectAsync(TestContext.Current.CancellationToken);

        wsClient.ConnectCallCount.ShouldBe(1);
    }

    [Fact]
    public void IsConnected_DelegatesToUnderlyingWebSocketClient()
    {
        var (ops, wsClient) = CreateOperations();

        wsClient.IsConnected = true;
        ((IStreamingOperations)ops).IsConnected.ShouldBeTrue();

        wsClient.IsConnected = false;
        ((IStreamingOperations)ops).IsConnected.ShouldBeFalse();
    }

    [Fact]
    public void LastMessageReceivedAt_DelegatesToUnderlyingWebSocketClient()
    {
        var (ops, wsClient) = CreateOperations();

        ((IStreamingOperations)ops).LastMessageReceivedAt.ShouldBeNull();

        var stamp = new DateTimeOffset(2026, 4, 30, 12, 0, 0, TimeSpan.Zero);
        wsClient.LastMessageReceivedAt = stamp;
        ((IStreamingOperations)ops).LastMessageReceivedAt.ShouldBe(stamp);
    }

    [Fact]
    public async Task SessionStatus_DeliversTypedEventOnTopicMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var sub = ((IStreamingOperations)ops).SubscribeSessionStatus();
        var received = new TaskCompletionSource<SessionStatusEvent>();
        using var s = sub.Stream.Subscribe(new TestObserver<SessionStatusEvent>(
            onNext: e => received.TrySetResult(e)));

        var json = JsonDocument.Parse("""{"topic":"sts","args":{"authenticated":true}}""").RootElement;
        await wsClient.UnsolicitedChannels["sts"].Writer.WriteAsync(json, ct);

        var evt = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        evt.Authenticated.ShouldBeTrue();
    }

    [Fact]
    public async Task SessionStatus_DeliversAuthenticatedFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var sub = ((IStreamingOperations)ops).SubscribeSessionStatus();
        var received = new TaskCompletionSource<SessionStatusEvent>();
        using var s = sub.Stream.Subscribe(new TestObserver<SessionStatusEvent>(
            onNext: e => received.TrySetResult(e)));

        var json = JsonDocument.Parse("""{"topic":"sts","args":{"authenticated":false}}""").RootElement;
        await wsClient.UnsolicitedChannels["sts"].Writer.WriteAsync(json, ct);

        var evt = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        evt.Authenticated.ShouldBeFalse();
    }

    [Fact]
    public async Task Bulletins_DeliversTypedEventOnTopicMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var sub = ((IStreamingOperations)ops).SubscribeBulletins();
        var received = new TaskCompletionSource<BulletinEvent>();
        using var s = sub.Stream.Subscribe(new TestObserver<BulletinEvent>(
            onNext: e => received.TrySetResult(e)));

        var json = JsonDocument.Parse("""{"topic":"blt","args":{"id":"B-42","message":"Exchange XYZ delayed"}}""").RootElement;
        await wsClient.UnsolicitedChannels["blt"].Writer.WriteAsync(json, ct);

        var evt = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        evt.Id.ShouldBe("B-42");
        evt.Message.ShouldBe("Exchange XYZ delayed");
    }

    [Fact]
    public async Task TradingNotifications_DeliversTypedEvent_AllFieldsPresent()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var sub = ((IStreamingOperations)ops).SubscribeTradingNotifications();
        var received = new TaskCompletionSource<NotificationEvent>();
        using var s = sub.Stream.Subscribe(new TestObserver<NotificationEvent>(
            onNext: e => received.TrySetResult(e)));

        var json = JsonDocument.Parse("""
            {"topic":"ntf","args":{"id":"N-7","title":"Order filled","text":"Your AAPL order was filled","url":"https://example.com/n7"}}
            """).RootElement;
        await wsClient.UnsolicitedChannels["ntf"].Writer.WriteAsync(json, ct);

        var evt = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        evt.Id.ShouldBe("N-7");
        evt.Title.ShouldBe("Order filled");
        evt.Text.ShouldBe("Your AAPL order was filled");
        evt.Url.ShouldBe("https://example.com/n7");
    }

    [Fact]
    public async Task TradingNotifications_DeliversTypedEvent_UrlMissing_StaysNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var sub = ((IStreamingOperations)ops).SubscribeTradingNotifications();
        var received = new TaskCompletionSource<NotificationEvent>();
        using var s = sub.Stream.Subscribe(new TestObserver<NotificationEvent>(
            onNext: e => received.TrySetResult(e)));

        var json = JsonDocument.Parse("""
            {"topic":"ntf","args":{"id":"N-8","title":"T","text":"X"}}
            """).RootElement;
        await wsClient.UnsolicitedChannels["ntf"].Writer.WriteAsync(json, ct);

        var evt = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        evt.Url.ShouldBeNull();
    }

    [Fact]
    public async Task SystemEvents_DeliversConnectionVariant_WithUsername()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var sub = ((IStreamingOperations)ops).SubscribeSystemEvents();
        var received = new TaskCompletionSource<SystemEvent>();
        using var s = sub.Stream.Subscribe(new TestObserver<SystemEvent>(
            onNext: e => received.TrySetResult(e)));

        var json = JsonDocument.Parse("""{"topic":"system","success":"alice","isFT":false,"isPaper":true}""").RootElement;
        await wsClient.UnsolicitedChannels["system"].Writer.WriteAsync(json, ct);

        var evt = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        evt.Username.ShouldBe("alice");
        evt.HeartbeatMs.ShouldBeNull();
        evt.IsFT.ShouldBe(false);
        evt.IsPaper.ShouldBe(true);
    }

    [Fact]
    public async Task AccountStatus_DeliversTypedEvent_AllFieldsPresent()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var sub = ((IStreamingOperations)ops).SubscribeAccountStatus();
        var received = new TaskCompletionSource<AccountStatusEvent>();
        using var s = sub.Stream.Subscribe(new TestObserver<AccountStatusEvent>(
            onNext: e => received.TrySetResult(e)));

        var json = JsonDocument.Parse("""
        {
          "topic":"act",
          "args":{
            "accounts":["DU123","DU124"],
            "acctProps":{
              "All":{
                "hasChildAccounts":false,
                "supportsCashQty":true,
                "liteUnderPro":true,
                "noFXConv":false,
                "isProp":false,
                "supportsFractions":true,
                "allowCustomerTime":true,
                "autoFx":true
              }
            },
            "aliases":{"DU123":"Main"},
            "allowFeatures":{
              "showGFIS":true,
              "showEUCostReport":false,
              "allowEventContract":false,
              "allowFXConv":true,
              "allowFinancialLens":false,
              "allowMTA":true,
              "allowTypeAhead":true,
              "allowEventTrading":false,
              "snapshotRefreshTimeout":300,
              "liteUser":false,
              "showWebNews":true,
              "research":true,
              "debugPnl":false,
              "showTaxOpt":false,
              "showImpactDashboard":true,
              "allowDynAccount":false,
              "allowCrypto":true,
              "allowFA":true,
              "allowLiteUnderPro":false,
              "allowedAssetTypes":"STK,OPT,FUT,CASH",
              "restrictTradeSubscription":false,
              "showUkUserLabels":true,
              "sideBySide":true
            },
            "chartPeriods":{"STK":["1d","5d"],"OPT":["1d"]},
            "groups":["G1"],
            "profiles":["P1"],
            "selectedAccount":"DU123",
            "serverInfo":{"serverName":"server-east-1","serverVersion":"10.42.0"},
            "sessionId":"SESS-XYZ",
            "isFT":true,
            "isPaper":true
          }
        }
        """).RootElement;
        await wsClient.UnsolicitedChannels["act"].Writer.WriteAsync(json, ct);

        var evt = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

        evt.Accounts.ShouldBe(new[] { "DU123", "DU124" });
        evt.AcctProps.ShouldContainKey("All");
        evt.AcctProps["All"].SupportsCashQty.ShouldBeTrue();
        evt.AcctProps["All"].SupportsFractions.ShouldBeTrue();
        evt.AcctProps["All"].LiteUnderPro.ShouldBeTrue();
        evt.AcctProps["All"].AutoFx.ShouldBeTrue();
        evt.Aliases["DU123"].ShouldBe("Main");
        evt.AllowFeatures.ShouldNotBeNull();
        evt.AllowFeatures!.AllowFXConv.ShouldBeTrue();
        evt.AllowFeatures.SnapshotRefreshTimeout.ShouldBe(300);
        evt.AllowFeatures.AllowedAssetTypes.ShouldBe("STK,OPT,FUT,CASH");
        evt.AllowFeatures.AllowFA.ShouldBeTrue();
        evt.AllowFeatures.AllowLiteUnderPro.ShouldBeFalse();
        evt.AllowFeatures.RestrictTradeSubscription.ShouldBeFalse();
        evt.AllowFeatures.ShowUkUserLabels.ShouldBeTrue();
        evt.AllowFeatures.SideBySide.ShouldBeTrue();
        evt.ChartPeriods["STK"].ShouldBe(new[] { "1d", "5d" });
        evt.Groups.ShouldBe(new[] { "G1" });
        evt.Profiles.ShouldBe(new[] { "P1" });
        evt.SelectedAccount.ShouldBe("DU123");
        evt.ServerInfo.ShouldNotBeNull();
        evt.ServerInfo!.ServerName.ShouldBe("server-east-1");
        evt.ServerInfo.ServerVersion.ShouldBe("10.42.0");
        evt.SessionId.ShouldBe("SESS-XYZ");
        evt.IsFT.ShouldBeTrue();
        evt.IsPaper.ShouldBeTrue();
    }

    [Fact]
    public async Task SystemEvents_DeliversHeartbeatVariant_WithHbMillis()
    {
        var ct = TestContext.Current.CancellationToken;
        var (ops, wsClient) = CreateOperations();

        var sub = ((IStreamingOperations)ops).SubscribeSystemEvents();
        var received = new TaskCompletionSource<SystemEvent>();
        using var s = sub.Stream.Subscribe(new TestObserver<SystemEvent>(
            onNext: e => received.TrySetResult(e)));

        var json = JsonDocument.Parse("""{"topic":"system","hb":1730000000000}""").RootElement;
        await wsClient.UnsolicitedChannels["system"].Writer.WriteAsync(json, ct);

        var evt = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        evt.Username.ShouldBeNull();
        evt.HeartbeatMs.ShouldBe(1730000000000);
        evt.IsFT.ShouldBeNull();
        evt.IsPaper.ShouldBeNull();
    }

    private static (StreamingOperations Operations, FakeWebSocketClient Client) CreateOperations()
    {
        var wsClient = new FakeWebSocketClient();
        var ops = new StreamingOperations(wsClient);
        return (ops, wsClient);
    }

    internal sealed class FakeWebSocketClient : IIbkrWebSocketClient
    {
        public string? LastSubscribeMessage { get; private set; }
        public string? LastTopicPrefix { get; private set; }
        public string? LastCancelMessage { get; private set; }
        public Channel<JsonElement> Channel { get; } = System.Threading.Channels.Channel.CreateUnbounded<JsonElement>();

        public bool IsConnected { get; set; } = true;
        public int ActiveSubscriptionCount => 0;
        public DateTimeOffset? LastMessageReceivedAt { get; set; }

        public int ConnectCallCount { get; private set; }

        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            ConnectCallCount++;
            return Task.CompletedTask;
        }

        public Task<(ChannelReader<JsonElement> Reader, Func<CancellationToken, ValueTask> Unsubscribe)> SubscribeTopicAsync(
            string subscribeMessage,
            string topicPrefix,
            string? cancelMessage,
            CancellationToken cancellationToken)
        {
            LastSubscribeMessage = subscribeMessage;
            LastTopicPrefix = topicPrefix;
            LastCancelMessage = cancelMessage;
            return Task.FromResult<(ChannelReader<JsonElement>, Func<CancellationToken, ValueTask>)>(
                (Channel.Reader, _ => ValueTask.CompletedTask));
        }

        public ConcurrentDictionary<string, Channel<JsonElement>> UnsolicitedChannels { get; } = new();

        public (ChannelReader<JsonElement> Reader, Func<CancellationToken, ValueTask> Unsubscribe) RegisterUnsolicitedTopic(string topicPrefix)
        {
            var channel = UnsolicitedChannels.GetOrAdd(
                topicPrefix,
                _ => System.Threading.Channels.Channel.CreateUnbounded<JsonElement>());
            return (channel.Reader, _ => ValueTask.CompletedTask);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TestObserver<T> : IObserver<T>
    {
        private readonly Action<T>? _onNext;
        private readonly Action<Exception>? _onError;
        private readonly Action? _onCompleted;

        public TestObserver(
            Action<T>? onNext = null,
            Action<Exception>? onError = null,
            Action? onCompleted = null)
        {
            _onNext = onNext;
            _onError = onError;
            _onCompleted = onCompleted;
        }

        public void OnNext(T value) => _onNext?.Invoke(value);
        public void OnError(Exception error) => _onError?.Invoke(error);
        public void OnCompleted() => _onCompleted?.Invoke();
    }
}
