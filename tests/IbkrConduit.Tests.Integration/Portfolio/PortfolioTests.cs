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
