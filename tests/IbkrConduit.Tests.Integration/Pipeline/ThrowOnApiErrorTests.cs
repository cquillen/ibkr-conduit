using System;
using System.Net;
using System.Threading.Tasks;
using IbkrConduit.Errors;
using IbkrConduit.Tests.Integration.Fixtures;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Pipeline;

/// <summary>
/// Integration tests verifying that <c>ThrowOnApiError = true</c> causes API failures
/// to throw <see cref="IbkrApiException"/> instead of returning <c>Result.Failure</c>,
/// while successful responses still return normally.
/// </summary>
public class ThrowOnApiErrorTests : IAsyncLifetime, IDisposable
{
    private TestHarness _harness = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = await TestHarness.CreateAsync(configureOptions: opts => opts.ThrowOnApiError = true);
    }

    [Fact]
    public async Task ApiError_WithThrowOnApiError_ThrowsIbkrApiException()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(500)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"error":"Invalid account ID","statusCode":500}"""));

        var ex = await Should.ThrowAsync<IbkrApiException>(
            () => _harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken));

        var error = ex.Error.ShouldBeOfType<IbkrApiError>();
        error.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        error.Message.ShouldBe("Invalid account ID");
    }

    [Fact]
    public async Task Success_WithThrowOnApiError_ReturnsResultNormally()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/accounts",
            FixtureLoader.LoadBody("Accounts", "GET-iserver-accounts"));

        var result = await _harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Accounts.ShouldNotBeEmpty();
        result.Value.Accounts.ShouldContain("U1234567");
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
