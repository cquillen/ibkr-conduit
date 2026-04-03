using System;
using System.Threading.Tasks;
using IbkrConduit.Errors;
using IbkrConduit.Tests.Integration.Fixtures;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Pipeline;

public class AuthFailureTests : IAsyncDisposable
{
    private TestHarness? _harness;

    private async Task<TestHarness> CreateInitializedHarnessAsync()
    {
        var harness = await TestHarness.CreateAsync();

        harness.StubAuthenticatedGet(
            "/v1/api/iserver/accounts",
            FixtureLoader.LoadBody("Accounts", "GET-iserver-accounts"));

        await harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken);

        return harness;
    }

    [Fact]
    public async Task Request_401AfterReauth_ThrowsSessionException()
    {
        _harness = await CreateInitializedHarnessAsync();

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .AtPriority(-1)
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        var ex = await Should.ThrowAsync<IbkrSessionException>(
            _harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Re-authentication succeeded but request still unauthorized");
        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task Request_ReauthLstFails_ThrowsSessionException()
    {
        _harness = await CreateInitializedHarnessAsync();

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/oauth/live_session_token")
                .UsingPost())
            .AtPriority(-1)
            .RespondWith(
                Response.Create()
                    .WithStatusCode(500)
                    .WithBody("DH exchange validation failed"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .AtPriority(-1)
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        var ex = await Should.ThrowAsync<IbkrSessionException>(
            _harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken));

        ex.InnerException.ShouldNotBeNull();
    }

    [Fact]
    public async Task Request_ReauthSsodhInitFails_ThrowsSessionException()
    {
        _harness = await CreateInitializedHarnessAsync();

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/auth/ssodh/init")
                .UsingPost())
            .AtPriority(-1)
            .RespondWith(
                Response.Create()
                    .WithStatusCode(500)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"error":"Session rejected"}"""));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .AtPriority(-1)
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        var ex = await Should.ThrowAsync<IbkrSessionException>(
            _harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken));

        ex.InnerException.ShouldNotBeNull();
    }

    public async ValueTask DisposeAsync()
    {
        if (_harness is not null)
        {
            await _harness.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
