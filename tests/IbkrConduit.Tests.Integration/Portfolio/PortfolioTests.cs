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
