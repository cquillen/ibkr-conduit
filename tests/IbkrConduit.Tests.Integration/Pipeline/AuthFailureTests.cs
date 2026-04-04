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
    public async Task Request_401AfterReauth_ReturnsFailureResult()
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

        // Under Result pattern, persistent 401 after re-auth returns a failed Result
        var result = await _harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBeOfType<IbkrApiError>();
        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task Request_ReauthLstFails_ThrowsIbkrApiException()
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

        var ex = await Should.ThrowAsync<IbkrApiException>(
            _harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken));

        ex.Error.ShouldBeOfType<IbkrSessionError>();
        ex.InnerException.ShouldNotBeNull();
    }

    [Fact]
    public async Task Request_ReauthSsodhInitFails_ThrowsIbkrApiException()
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

        var ex = await Should.ThrowAsync<IbkrApiException>(
            _harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken));

        ex.Error.ShouldBeOfType<IbkrSessionError>();
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
