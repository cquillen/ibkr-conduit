using System;
using System.Threading.Tasks;
using IbkrConduit.Tests.Integration.Fixtures;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Portfolio;

public class PortfolioTests : IAsyncLifetime, IDisposable
{
    private TestHarness _harness = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = await TestHarness.CreateAsync();
    }

    [Fact]
    public async Task GetAccounts_ReturnsAllFields()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/portfolio/accounts",
            FixtureLoader.LoadBody("Portfolio", "GET-portfolio-accounts"));

        var accounts = await _harness.Client.Portfolio.GetAccountsAsync(TestContext.Current.CancellationToken);

        accounts.ShouldNotBeEmpty();
        var account = accounts[0];
        account.Id.ShouldBe("U1234567");
        account.AccountId.ShouldBe("U1234567");
        account.AccountTitle.ShouldBe("Test User");
        account.DisplayName.ShouldBe("Test User");
        account.Currency.ShouldBe("USD");
        account.Type.ShouldBe("DEMO");
        account.TradingType.ShouldBe("STKNOPT");
        account.BusinessType.ShouldBe("INDEPENDENT");
        account.IbEntity.ShouldBe("IBLLC-US");
        account.BrokerageAccess.ShouldBeTrue();
        account.Faclient.ShouldBeFalse();
        account.ClearingStatus.ShouldBe("O");
        account.Covestor.ShouldBeFalse();
        account.NoClientTrading.ShouldBeFalse();
        account.TrackVirtualFXPortfolio.ShouldBeFalse();
        account.Parent.ShouldNotBeNull();
        account.Parent!.IsMParent.ShouldBeFalse();
        account.Parent!.IsMultiplex.ShouldBeFalse();

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetAccounts_401Recovery_ReauthenticatesAndRetries()
    {
        // First call returns 401
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .InScenario("401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        // After re-auth, second call succeeds
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .InScenario("401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Portfolio", "GET-portfolio-accounts")));

        var accounts = await _harness.Client.Portfolio.GetAccountsAsync(TestContext.Current.CancellationToken);

        accounts.ShouldNotBeEmpty();
        accounts[0].Id.ShouldBe("U1234567");

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetPositions_ReturnsAllFields()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/portfolio/U1234567/positions/0",
            FixtureLoader.LoadBody("Portfolio", "GET-portfolio-positions-page0"));

        var positions = await _harness.Client.Portfolio.GetPositionsAsync(
            "U1234567", 0, TestContext.Current.CancellationToken);

        positions.ShouldNotBeEmpty();
        var pos = positions[0];
        pos.AccountId.ShouldBe("U1234567");
        pos.Conid.ShouldBe(320227571L);
        pos.ContractDescription.ShouldBe("QQQ");
        pos.Quantity.ShouldBe(3.0m);
        pos.MarketPrice.ShouldBe(584.6799927m);
        pos.MarketValue.ShouldBe(1754.04m);
        pos.Currency.ShouldBe("USD");
        pos.AverageCost.ShouldBe(584.31333335m);
        pos.AveragePrice.ShouldBe(584.31333335m);
        pos.RealizedPnl.ShouldBe(0.0m);
        pos.UnrealizedPnl.ShouldBe(1.1m);
        pos.Multiplier.ShouldBeNull();
        pos.ExerciseStyle.ShouldBeNull();
        pos.ConExchMap.ShouldNotBeNull();
        pos.ConExchMap.ShouldBeEmpty();
        pos.AssetClass.ShouldBe("STK");
        pos.UndConid.ShouldBe(0L);
        pos.Model.ShouldBe("");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetPositions_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/U1234567/positions/0")
                .UsingGet())
            .InScenario("positions-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/U1234567/positions/0")
                .UsingGet())
            .InScenario("positions-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Portfolio", "GET-portfolio-positions-page0")));

        var positions = await _harness.Client.Portfolio.GetPositionsAsync(
            "U1234567", 0, TestContext.Current.CancellationToken);

        positions.ShouldNotBeEmpty();
        positions[0].Conid.ShouldBe(320227571L);

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetPositions_EmptyPage_ReturnsEmptyList()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/portfolio/U1234567/positions/999",
            FixtureLoader.LoadBody("Portfolio", "GET-portfolio-positions-empty"));

        var positions = await _harness.Client.Portfolio.GetPositionsAsync(
            "U1234567", 999, TestContext.Current.CancellationToken);

        positions.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAccountSummary_ReturnsAllEntryFields()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/portfolio/U1234567/summary",
            FixtureLoader.LoadBody("Portfolio", "GET-portfolio-summary"));

        var summary = await _harness.Client.Portfolio.GetAccountSummaryAsync(
            "U1234567", TestContext.Current.CancellationToken);

        summary.ShouldNotBeEmpty();
        summary.Count.ShouldBe(5);

        summary.ShouldContainKey("accountcode");
        var accountCode = summary["accountcode"];
        accountCode.Amount.ShouldBe(0.0m);
        accountCode.Currency.ShouldBeNull();
        accountCode.IsNull.ShouldBeFalse();
        accountCode.Timestamp.ShouldBe(1775169606L);
        accountCode.Value.ShouldBe("U1234567");
        accountCode.Severity.ShouldBe(0);

        summary.ShouldContainKey("netliquidation");
        var netLiq = summary["netliquidation"];
        netLiq.Amount.ShouldBe(1005165.5m);
        netLiq.Currency.ShouldBe("USD");
        netLiq.IsNull.ShouldBeFalse();
        netLiq.Value.ShouldBe("1005165.5");

        summary.ShouldContainKey("buyingpower");
        summary["buyingpower"].Amount.ShouldBe(3984328.97m);

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetAccountSummary_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/U1234567/summary")
                .UsingGet())
            .InScenario("summary-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/U1234567/summary")
                .UsingGet())
            .InScenario("summary-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Portfolio", "GET-portfolio-summary")));

        var summary = await _harness.Client.Portfolio.GetAccountSummaryAsync(
            "U1234567", TestContext.Current.CancellationToken);

        summary.ShouldNotBeEmpty();
        summary.ShouldContainKey("netliquidation");

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetLedger_ReturnsAllFields()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/portfolio/U1234567/ledger",
            FixtureLoader.LoadBody("Portfolio", "GET-portfolio-ledger"));

        var ledger = await _harness.Client.Portfolio.GetLedgerAsync(
            "U1234567", TestContext.Current.CancellationToken);

        ledger.ShouldNotBeEmpty();
        ledger.Count.ShouldBe(2);
        ledger.ShouldContainKey("USD");
        ledger.ShouldContainKey("BASE");

        var usd = ledger["USD"];
        usd.CashBalance.ShouldBe(971869.6m);
        usd.NetLiquidationValue.ShouldBe(1005165.5m);
        usd.SettledCash.ShouldBe(971869.6m);
        usd.ExchangeRate.ShouldBe(1m);
        usd.Interest.ShouldBe(2682.45m);
        usd.UnrealizedPnl.ShouldBe(151.57m);
        usd.StockMarketValue.ShouldBe(30613.4m);
        usd.Currency.ShouldBe("USD");
        usd.RealizedPnl.ShouldBe(0.0m);
        usd.AcctCode.ShouldBe("U1234567");
        usd.Key.ShouldBe("LedgerList");
        usd.Timestamp.ShouldBe(1775169606L);
        usd.Severity.ShouldBe(0);
        usd.SecondKey.ShouldBe("USD");
        usd.EndOfBundle.ShouldBe(1);
        usd.Dividends.ShouldBe(0.0m);
        usd.CryptocurrencyValue.ShouldBe(0.0m);

        var baseEntry = ledger["BASE"];
        baseEntry.Currency.ShouldBe("BASE");
        baseEntry.SecondKey.ShouldBe("BASE");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetLedger_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/U1234567/ledger")
                .UsingGet())
            .InScenario("ledger-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/U1234567/ledger")
                .UsingGet())
            .InScenario("ledger-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Portfolio", "GET-portfolio-ledger")));

        var ledger = await _harness.Client.Portfolio.GetLedgerAsync(
            "U1234567", TestContext.Current.CancellationToken);

        ledger.ShouldNotBeEmpty();
        ledger.ShouldContainKey("USD");

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetPartitionedPnl_ReturnsEmptyUpnl()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/account/pnl/partitioned",
            FixtureLoader.LoadBody("Portfolio", "GET-iserver-account-pnl-partitioned"));

        var pnl = await _harness.Client.Portfolio.GetPartitionedPnlAsync(
            TestContext.Current.CancellationToken);

        pnl.ShouldNotBeNull();
        pnl.Upnl.ShouldNotBeNull();
        pnl.Upnl.ShouldBeEmpty();

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetPartitionedPnl_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/pnl/partitioned")
                .UsingGet())
            .InScenario("pnl-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/pnl/partitioned")
                .UsingGet())
            .InScenario("pnl-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Portfolio", "GET-iserver-account-pnl-partitioned")));

        var pnl = await _harness.Client.Portfolio.GetPartitionedPnlAsync(
            TestContext.Current.CancellationToken);

        pnl.ShouldNotBeNull();
        pnl.Upnl.ShouldNotBeNull();

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetAccountInfo_ReturnsAllFields()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/portfolio/U1234567/meta",
            FixtureLoader.LoadBody("Portfolio", "GET-portfolio-meta"));

        var info = await _harness.Client.Portfolio.GetAccountInfoAsync(
            "U1234567", TestContext.Current.CancellationToken);

        info.ShouldNotBeNull();
        info.Id.ShouldBe("U1234567");
        info.AccountId.ShouldBe("U1234567");
        info.AccountTitle.ShouldBe("Test User");
        info.Type.ShouldBe("DEMO");
        info.Currency.ShouldBe("USD");
        info.AdditionalData.ShouldNotBeNull();
        info.AdditionalData!.ShouldContainKey("tradingType");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetAccountInfo_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/U1234567/meta")
                .UsingGet())
            .InScenario("meta-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/U1234567/meta")
                .UsingGet())
            .InScenario("meta-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Portfolio", "GET-portfolio-meta")));

        var info = await _harness.Client.Portfolio.GetAccountInfoAsync(
            "U1234567", TestContext.Current.CancellationToken);

        info.Id.ShouldBe("U1234567");
        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetAccountAllocation_ReturnsAllFields()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/portfolio/U1234567/allocation",
            FixtureLoader.LoadBody("Portfolio", "GET-portfolio-allocation"));

        var allocation = await _harness.Client.Portfolio.GetAccountAllocationAsync(
            "U1234567", TestContext.Current.CancellationToken);

        allocation.ShouldNotBeNull();
        allocation.AssetClass.ShouldNotBeNull();
        allocation.AssetClass!["long"].ShouldContainKey("STK");
        allocation.AssetClass!["long"]["STK"].ShouldBe(30613.4m);
        allocation.AssetClass!["long"]["CASH"].ShouldBe(971869.6m);
        allocation.AssetClass!["short"].ShouldBeEmpty();
        allocation.Sector.ShouldNotBeNull();
        allocation.Sector!["long"]["Others"].ShouldBe(30613.4m);
        allocation.Group.ShouldNotBeNull();
        allocation.Group!["long"]["Others"].ShouldBe(30613.4m);

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetAccountAllocation_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/U1234567/allocation")
                .UsingGet())
            .InScenario("allocation-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/U1234567/allocation")
                .UsingGet())
            .InScenario("allocation-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Portfolio", "GET-portfolio-allocation")));

        var allocation = await _harness.Client.Portfolio.GetAccountAllocationAsync(
            "U1234567", TestContext.Current.CancellationToken);

        allocation.AssetClass.ShouldNotBeNull();
        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetPositionByConid_ReturnsAllFields()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/portfolio/U1234567/position/756733",
            FixtureLoader.LoadBody("Portfolio", "GET-portfolio-position-by-conid"));

        var positions = await _harness.Client.Portfolio.GetPositionByConidAsync(
            "U1234567", "756733", TestContext.Current.CancellationToken);

        positions.ShouldNotBeEmpty();
        var pos = positions[0];
        pos.AccountId.ShouldBe("U1234567");
        pos.Conid.ShouldBe(756733L);
        pos.ContractDescription.ShouldBe("SPY");
        pos.Quantity.ShouldBe(44.0m);
        pos.MarketPrice.ShouldBe(655.8945923m);
        pos.MarketValue.ShouldBe(28859.36m);
        pos.Currency.ShouldBe("USD");
        pos.AverageCost.ShouldBe(652.47477275m);
        pos.RealizedPnl.ShouldBe(0.0m);
        pos.UnrealizedPnl.ShouldBe(150.47m);
        pos.AssetClass.ShouldBe("STK");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetPositionByConid_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/U1234567/position/756733")
                .UsingGet())
            .InScenario("position-conid-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/U1234567/position/756733")
                .UsingGet())
            .InScenario("position-conid-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Portfolio", "GET-portfolio-position-by-conid")));

        var positions = await _harness.Client.Portfolio.GetPositionByConidAsync(
            "U1234567", "756733", TestContext.Current.CancellationToken);

        positions.ShouldNotBeEmpty();
        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetPositionsByConid_ReturnsResult()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/portfolio/positions/756733",
            FixtureLoader.LoadBody("Portfolio", "GET-portfolio-positions-by-conid"));

        var result = await _harness.Client.Portfolio.GetPositionAndContractInfoAsync(
            "756733", TestContext.Current.CancellationToken);

        // The endpoint returns a dictionary { "accountId": [...] } but the DTO is PositionContractInfo.
        // The account-keyed data goes into AdditionalData.
        result.ShouldNotBeNull();
        result.AdditionalData.ShouldNotBeNull();
        result.AdditionalData!.ShouldContainKey("U1234567");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetComboPositions_EmptyResponse_ReturnsEmptyList()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/portfolio/U1234567/combo/positions",
            FixtureLoader.LoadBody("Portfolio", "GET-portfolio-combo-positions-empty"));

        var combos = await _harness.Client.Portfolio.GetComboPositionsAsync(
            "U1234567", cancellationToken: TestContext.Current.CancellationToken);

        combos.ShouldBeEmpty();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetRealTimePositions_ReturnsPositions()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/portfolio2/U1234567/positions",
            FixtureLoader.LoadBody("Portfolio", "GET-portfolio2-positions"));

        var positions = await _harness.Client.Portfolio.GetRealTimePositionsAsync(
            "U1234567", cancellationToken: TestContext.Current.CancellationToken);

        positions.ShouldNotBeEmpty();
        positions.Count.ShouldBe(2);
        // The portfolio2 endpoint uses different field names than standard Position.
        // Fields matching JsonPropertyName annotations will deserialize; others go to AdditionalData.
        var pos = positions[0];
        pos.Conid.ShouldBe(320227571L);
        pos.Currency.ShouldBe("USD");
        pos.AverageCost.ShouldBe(584.3133333333334m);
        pos.AveragePrice.ShouldBe(584.3133333333334m);
        pos.RealizedPnl.ShouldBe(0.0m);
        pos.AssetClass.ShouldBe("STK");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetRealTimePositions_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio2/U1234567/positions")
                .UsingGet())
            .InScenario("realtime-positions-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio2/U1234567/positions")
                .UsingGet())
            .InScenario("realtime-positions-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Portfolio", "GET-portfolio2-positions")));

        var positions = await _harness.Client.Portfolio.GetRealTimePositionsAsync(
            "U1234567", cancellationToken: TestContext.Current.CancellationToken);

        positions.ShouldNotBeEmpty();
        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetSubAccounts_ReturnsAllFields()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/portfolio/subaccounts",
            FixtureLoader.LoadBody("Portfolio", "GET-portfolio-subaccounts"));

        var subAccounts = await _harness.Client.Portfolio.GetSubAccountsAsync(
            TestContext.Current.CancellationToken);

        subAccounts.ShouldNotBeEmpty();
        var sub = subAccounts[0];
        sub.Id.ShouldBe("U1234567");
        sub.AccountId.ShouldBe("U1234567");
        sub.AccountTitle.ShouldBe("Test User");
        sub.AccountType.ShouldBe("DEMO");
        sub.Description.ShouldBe("U1234567");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task InvalidatePortfolioCache_Succeeds()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/portfolio/U1234567/positions/invalidate",
            FixtureLoader.LoadBody("Portfolio", "POST-portfolio-invalidate"));

        // InvalidatePortfolioCacheAsync returns Task (void) — just verify it doesn't throw
        await _harness.Client.Portfolio.InvalidatePortfolioCacheAsync(
            "U1234567", TestContext.Current.CancellationToken);

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetPerformance_ReturnsAllFields()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/pa/performance",
            FixtureLoader.LoadBody("Portfolio", "POST-pa-performance"));

        var perf = await _harness.Client.Portfolio.GetAccountPerformanceAsync(
            ["U1234567"], "1M", TestContext.Current.CancellationToken);

        perf.ShouldNotBeNull();
        perf.CurrencyType.ShouldBe("base");
        // Complex nested data (cps, tpps, nav) is in AdditionalData
        perf.AdditionalData.ShouldNotBeNull();
        perf.AdditionalData!.ShouldContainKey("cps");
        perf.AdditionalData!.ShouldContainKey("nav");
        perf.AdditionalData!.ShouldContainKey("pm");
        perf.AdditionalData!.ShouldContainKey("included");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetTransactionHistory_ReturnsAllFields()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/pa/transactions",
            FixtureLoader.LoadBody("Portfolio", "POST-pa-transactions"));

        var txns = await _harness.Client.Portfolio.GetTransactionHistoryAsync(
            ["U1234567"], ["756733"], "USD", 30, TestContext.Current.CancellationToken);

        txns.ShouldNotBeNull();
        txns.Id.ShouldBe("getTransactions");
        // Complex nested data (transactions array, rpnl) is in AdditionalData
        txns.AdditionalData.ShouldNotBeNull();
        txns.AdditionalData!.ShouldContainKey("transactions");
        txns.AdditionalData!.ShouldContainKey("currency");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetConsolidatedAllocation_ReturnsAllFields()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/portfolio/allocation",
            FixtureLoader.LoadBody("Portfolio", "POST-portfolio-consolidated-allocation"));

        var allocation = await _harness.Client.Portfolio.GetConsolidatedAllocationAsync(
            ["U1234567"], TestContext.Current.CancellationToken);

        allocation.ShouldNotBeNull();
        allocation.AssetClass.ShouldNotBeNull();
        allocation.AssetClass!["long"]["STK"].ShouldBe(30613.4m);
        allocation.Sector.ShouldNotBeNull();
        allocation.Group.ShouldNotBeNull();

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetAllPeriodsPerformance_ReturnsAllFields()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/pa/allperiods",
            FixtureLoader.LoadBody("Portfolio", "POST-pa-allperiods"));

        var perf = await _harness.Client.Portfolio.GetAllPeriodsPerformanceAsync(
            ["U1234567"], TestContext.Current.CancellationToken);

        perf.ShouldNotBeNull();
        perf.CurrencyType.ShouldBe("base");
        perf.AdditionalData.ShouldNotBeNull();
        perf.AdditionalData!.ShouldContainKey("pm");
        perf.AdditionalData!.ShouldContainKey("included");
        perf.AdditionalData!.ShouldContainKey("U1234567");

        _harness.VerifyHandshakeOccurred();
    }

    public async ValueTask DisposeAsync()
    {
        await _harness.DisposeAsync();
    }

    public void Dispose()
    {
        _harness.Dispose();
        GC.SuppressFinalize(this);
    }
}
