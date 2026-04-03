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
