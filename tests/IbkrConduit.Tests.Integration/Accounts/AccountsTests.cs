using System;
using System.Net;
using System.Threading.Tasks;
using IbkrConduit.Errors;
using IbkrConduit.Tests.Integration.Fixtures;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Accounts;

public class AccountsTests : IAsyncLifetime, IDisposable
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
            "/v1/api/iserver/accounts",
            FixtureLoader.LoadBody("Accounts", "GET-iserver-accounts"));

        var result = (await _harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken)).Value;

        result.Accounts.ShouldNotBeEmpty();
        result.Accounts[0].ShouldBe("U1234567");
        result.SelectedAccount.ShouldBe("U1234567");
        result.SessionId.ShouldBe("test-session-001");
        result.IsFt.ShouldNotBeNull();
        result.IsFt!.Value.ShouldBeFalse();
        result.IsPaper.ShouldNotBeNull();
        result.IsPaper!.Value.ShouldBeTrue();
        result.ServerInfo.ShouldNotBeNull();
        result.AcctProps.ShouldNotBeNull();
        result.Aliases.ShouldNotBeNull();
        result.AllowFeatures.ShouldNotBeNull();
        result.ChartPeriods.ShouldNotBeNull();

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetAccounts_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .InScenario("accounts-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .InScenario("accounts-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Accounts", "GET-iserver-accounts")));

        var result = (await _harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken)).Value;

        result.Accounts.ShouldNotBeEmpty();
        result.Accounts[0].ShouldBe("U1234567");
        result.SelectedAccount.ShouldBe("U1234567");

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task SwitchAccount_ReturnsAllFields()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/iserver/account",
            FixtureLoader.LoadBody("Accounts", "POST-switch-account"));

        var result = (await _harness.Client.Accounts.SwitchAccountAsync(
            "U1234567", TestContext.Current.CancellationToken)).Value;

        result.Success.ShouldBe("Account already set");

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task SwitchAccount_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account")
                .UsingPost())
            .InScenario("switch-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account")
                .UsingPost())
            .InScenario("switch-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Accounts", "POST-switch-account")));

        var result = (await _harness.Client.Accounts.SwitchAccountAsync(
            "U1234567", TestContext.Current.CancellationToken)).Value;

        result.Success.ShouldBe("Account already set");

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetSignaturesAndOwners_ReturnsAllFields()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/acesws/U1234567/signatures-and-owners",
            FixtureLoader.LoadBody("Accounts", "GET-signatures-and-owners"));

        var result = (await _harness.Client.Accounts.GetSignaturesAndOwnersAsync(
            "U1234567", TestContext.Current.CancellationToken)).Value;

        result.AccountId.ShouldBe("U1234567");
        result.Users.ShouldNotBeEmpty();
        result.Users[0].RoleId.ShouldBe("OWNER");
        result.Users[0].HasRightCodeInd.ShouldBeTrue();
        result.Users[0].UserName.ShouldBe("testuser01");
        result.Users[0].Entity.ShouldNotBeNull();
        result.Users[0].Entity!.FirstName.ShouldBe("Jane");
        result.Users[0].Entity!.LastName.ShouldBe("Doe");
        result.Users[0].Entity!.EntityType.ShouldBe("INDIVIDUAL");
        result.Users[0].Entity!.EntityName.ShouldBe("Ms. Jane A Doe");
        result.Users[0].Entity!.DateOfBirth.ShouldBe("1990-01-15");
        result.Applicant.ShouldNotBeNull();
        result.Applicant!.Signatures.ShouldNotBeEmpty();
        result.Applicant!.Signatures[0].ShouldBe("Jane A Doe");

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetSignaturesAndOwners_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/acesws/U1234567/signatures-and-owners")
                .UsingGet())
            .InScenario("signatures-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/acesws/U1234567/signatures-and-owners")
                .UsingGet())
            .InScenario("signatures-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Accounts", "GET-signatures-and-owners")));

        var result = (await _harness.Client.Accounts.GetSignaturesAndOwnersAsync(
            "U1234567", TestContext.Current.CancellationToken)).Value;

        result.AccountId.ShouldBe("U1234567");
        result.Users.ShouldNotBeEmpty();

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task SearchDynamicAccount_ReturnsMatchedAccounts()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/account/search/U123",
            FixtureLoader.LoadBody("Accounts", "GET-search-dynamic-account"));

        var result = (await _harness.Client.Accounts.SearchDynamicAccountAsync(
            "U123", TestContext.Current.CancellationToken)).Value;

        result.Pattern.ShouldBe("U123");
        result.MatchedAccounts.ShouldNotBeEmpty();
        result.MatchedAccounts[0].AccountId.ShouldBe("U1234567");
        result.MatchedAccounts[0].Alias.ShouldBe("U1234567");
        result.MatchedAccounts[0].AllocationId.ShouldBe("U1234567");

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task SearchDynamicAccount_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/search/U123")
                .UsingGet())
            .InScenario("search-dynacct-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/search/U123")
                .UsingGet())
            .InScenario("search-dynacct-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Accounts", "GET-search-dynamic-account")));

        var result = (await _harness.Client.Accounts.SearchDynamicAccountAsync(
            "U123", TestContext.Current.CancellationToken)).Value;

        result.MatchedAccounts.ShouldNotBeEmpty();

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task SetDynamicAccount_ReturnsConfirmation()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/iserver/dynaccount",
            FixtureLoader.LoadBody("Accounts", "POST-set-dynamic-account"));

        var result = (await _harness.Client.Accounts.SetDynamicAccountAsync(
            "U1234567", TestContext.Current.CancellationToken)).Value;

        result.Set.ShouldNotBeNull();
        result.Set!.Value.ShouldBeTrue();
        result.AcctId.ShouldBe("U1234567");

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task SetDynamicAccount_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/dynaccount")
                .UsingPost())
            .InScenario("set-dynacct-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/dynaccount")
                .UsingPost())
            .InScenario("set-dynacct-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Accounts", "POST-set-dynamic-account")));

        var result = (await _harness.Client.Accounts.SetDynamicAccountAsync(
            "U1234567", TestContext.Current.CancellationToken)).Value;

        result.Set.ShouldNotBeNull();
        result.Set!.Value.ShouldBeTrue();

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetAccountSummary_ReturnsAllFields()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/account/U1234567/summary",
            FixtureLoader.LoadBody("Accounts", "GET-account-summary"));

        var result = (await _harness.Client.Accounts.GetAccountSummaryAsync(
            "U1234567", TestContext.Current.CancellationToken)).Value;

        result.Balance.ShouldBe(994831.0);
        result.BuyingPower.ShouldBe(3979324.0);
        result.NetLiquidationValue.ShouldBe(1005254.0);
        result.EquityWithLoanValue.ShouldBe(1002485.0);
        result.TotalCashValue.ShouldBe(971870.0);
        result.InitialMargin.ShouldBe(7654.0);
        result.MaintenanceMargin.ShouldBe(7654.0);
        result.SecuritiesGvp.ShouldBe(30615.0);
        result.Sma.ShouldBe(989946.0);
        result.CashBalances.ShouldNotBeNull();
        result.CashBalances!.Count.ShouldBe(1);
        result.CashBalances[0].Currency.ShouldBe("USD");
        result.CashBalances[0].Balance.ShouldBe(971870.0);
        result.CashBalances[0].SettledCash.ShouldBe(971870.0);

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetAccountSummary_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/U1234567/summary")
                .UsingGet())
            .InScenario("summary-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/U1234567/summary")
                .UsingGet())
            .InScenario("summary-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Accounts", "GET-account-summary")));

        var result = (await _harness.Client.Accounts.GetAccountSummaryAsync(
            "U1234567", TestContext.Current.CancellationToken)).Value;

        result.NetLiquidationValue.ShouldBe(1005254.0);

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetAccountSummaryAvailableFunds_ReturnsAllSegments()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/account/U1234567/summary/available_funds",
            FixtureLoader.LoadBody("Accounts", "GET-account-summary-available-funds"));

        var result = (await _harness.Client.Accounts.GetAccountSummaryAvailableFundsAsync(
            "U1234567", TestContext.Current.CancellationToken)).Value;

        result.ShouldContainKey("total");
        result.ShouldContainKey("securities");
        result.ShouldContainKey("commodities");
        result.ShouldContainKey("Crypto at Paxos");
        result["total"]["current_available"].ShouldBe("994,831 USD");
        result["total"]["buying_power"].ShouldBe("3,979,324 USD");
        result["securities"]["leverage"].ShouldBe("1.01");

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetAccountSummaryAvailableFunds_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/U1234567/summary/available_funds")
                .UsingGet())
            .InScenario("avail-funds-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/U1234567/summary/available_funds")
                .UsingGet())
            .InScenario("avail-funds-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Accounts", "GET-account-summary-available-funds")));

        var result = (await _harness.Client.Accounts.GetAccountSummaryAvailableFundsAsync(
            "U1234567", TestContext.Current.CancellationToken)).Value;

        result.ShouldContainKey("total");

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetAccountSummaryBalances_ReturnsAllSegments()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/account/U1234567/summary/balances",
            FixtureLoader.LoadBody("Accounts", "GET-account-summary-balances"));

        var result = (await _harness.Client.Accounts.GetAccountSummaryBalancesAsync(
            "U1234567", TestContext.Current.CancellationToken)).Value;

        result.ShouldContainKey("total");
        result.ShouldContainKey("securities");
        result.ShouldContainKey("commodities");
        result.ShouldContainKey("Crypto at Paxos");
        result["total"]["net_liquidation"].ShouldBe("1,005,254 USD");
        result["total"]["equity_with_loan"].ShouldBe("1,002,485 USD");
        result["total"]["cash"].ShouldBe("971,870 USD");

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetAccountSummaryBalances_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/U1234567/summary/balances")
                .UsingGet())
            .InScenario("balances-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/U1234567/summary/balances")
                .UsingGet())
            .InScenario("balances-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Accounts", "GET-account-summary-balances")));

        var result = (await _harness.Client.Accounts.GetAccountSummaryBalancesAsync(
            "U1234567", TestContext.Current.CancellationToken)).Value;

        result.ShouldContainKey("total");

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetAccountSummaryMargins_ReturnsAllSegments()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/account/U1234567/summary/margins",
            FixtureLoader.LoadBody("Accounts", "GET-account-summary-margins"));

        var result = (await _harness.Client.Accounts.GetAccountSummaryMarginsAsync(
            "U1234567", TestContext.Current.CancellationToken)).Value;

        result.ShouldContainKey("total");
        result.ShouldContainKey("securities");
        result.ShouldContainKey("commodities");
        result.ShouldContainKey("Crypto at Paxos");
        result["total"]["current_initial"].ShouldBe("7,654 USD");
        result["total"]["current_maint"].ShouldBe("7,654 USD");
        result["securities"]["RegT Margin"].ShouldBe("0 USD");

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetAccountSummaryMargins_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/U1234567/summary/margins")
                .UsingGet())
            .InScenario("margins-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/U1234567/summary/margins")
                .UsingGet())
            .InScenario("margins-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Accounts", "GET-account-summary-margins")));

        var result = (await _harness.Client.Accounts.GetAccountSummaryMarginsAsync(
            "U1234567", TestContext.Current.CancellationToken)).Value;

        result.ShouldContainKey("total");

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetAccountSummaryMarketValue_ReturnsAllCurrencies()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/account/U1234567/summary/market_value",
            FixtureLoader.LoadBody("Accounts", "GET-account-summary-market-value"));

        var result = (await _harness.Client.Accounts.GetAccountSummaryMarketValueAsync(
            "U1234567", TestContext.Current.CancellationToken)).Value;

        result.ShouldContainKey("USD");
        result["USD"]["total_cash"].ShouldBe("971,870");
        result["USD"]["stock"].ShouldBe("30,615");
        result["USD"]["net_liquidation"].ShouldBe("1,005,254");
        result["USD"]["unrealized_pnl"].ShouldBe("2,769");

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetAccountSummaryMarketValue_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/U1234567/summary/market_value")
                .UsingGet())
            .InScenario("market-value-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/U1234567/summary/market_value")
                .UsingGet())
            .InScenario("market-value-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Accounts", "GET-account-summary-market-value")));

        var result = (await _harness.Client.Accounts.GetAccountSummaryMarketValueAsync(
            "U1234567", TestContext.Current.CancellationToken)).Value;

        result.ShouldContainKey("USD");

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetAccounts_ServerError_ReturnsFailureResult()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(500)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"error":"Internal Server Error"}"""));

        var result = await _harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<IbkrApiError>();
        error.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
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
