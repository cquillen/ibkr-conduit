using System;
using System.Threading.Tasks;
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

        var result = await _harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken);

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

        var result = await _harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken);

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

        var result = await _harness.Client.Accounts.SwitchAccountAsync(
            "U1234567", TestContext.Current.CancellationToken);

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

        var result = await _harness.Client.Accounts.SwitchAccountAsync(
            "U1234567", TestContext.Current.CancellationToken);

        result.Success.ShouldBe("Account already set");

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetSignaturesAndOwners_ReturnsAllFields()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/acesws/U1234567/signatures-and-owners",
            FixtureLoader.LoadBody("Accounts", "GET-signatures-and-owners"));

        var result = await _harness.Client.Accounts.GetSignaturesAndOwnersAsync(
            "U1234567", TestContext.Current.CancellationToken);

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

        var result = await _harness.Client.Accounts.GetSignaturesAndOwnersAsync(
            "U1234567", TestContext.Current.CancellationToken);

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

        var result = await _harness.Client.Accounts.SearchDynamicAccountAsync(
            "U123", TestContext.Current.CancellationToken);

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

        var result = await _harness.Client.Accounts.SearchDynamicAccountAsync(
            "U123", TestContext.Current.CancellationToken);

        result.MatchedAccounts.ShouldNotBeEmpty();

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task SetDynamicAccount_ReturnsConfirmation()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/iserver/dynaccount",
            FixtureLoader.LoadBody("Accounts", "POST-set-dynamic-account"));

        var result = await _harness.Client.Accounts.SetDynamicAccountAsync(
            "U1234567", TestContext.Current.CancellationToken);

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

        var result = await _harness.Client.Accounts.SetDynamicAccountAsync(
            "U1234567", TestContext.Current.CancellationToken);

        result.Set.ShouldNotBeNull();
        result.Set!.Value.ShouldBeTrue();

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
