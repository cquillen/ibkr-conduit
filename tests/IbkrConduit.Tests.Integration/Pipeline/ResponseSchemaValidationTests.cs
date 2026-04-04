using System;
using System.Threading.Tasks;
using IbkrConduit.Errors;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Pipeline;

/// <summary>
/// Integration tests for <see cref="IbkrConduit.Http.ResponseSchemaValidationHandler"/>
/// exercised through the full DI pipeline.
/// </summary>
public class ResponseSchemaValidationTests : IAsyncDisposable
{
    private TestHarness? _harness;

    [Fact]
    public async Task StrictMode_MissingRequiredField_ThrowsSchemaViolationException()
    {
        _harness = await TestHarness.CreateAsync(opts =>
        {
            opts.StrictResponseValidation = true;
        });

        // Stub accounts returning a response missing the required "selectedAccount" field
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"accounts":["U1234567"]}"""));

        var ex = await Should.ThrowAsync<IbkrSchemaViolationException>(
            _harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken));

        ex.MissingFields.ShouldContain("selectedAccount");
    }

    [Fact]
    public async Task NonStrictMode_MissingRequiredField_DoesNotThrow()
    {
        _harness = await TestHarness.CreateAsync(opts =>
        {
            opts.StrictResponseValidation = false;
        });

        // Same missing field, but non-strict mode -- should not throw
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"accounts":["U1234567"]}"""));

        // Should NOT throw -- non-strict mode logs a warning but continues
        var result = await _harness.Client.Accounts.GetAccountsAsync(
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Accounts.ShouldContain("U1234567");
    }

    [Fact]
    public async Task StrictMode_ExtensionDataDto_ExtraFieldsAllowed()
    {
        _harness = await TestHarness.CreateAsync(opts =>
        {
            opts.StrictResponseValidation = true;
        });

        // IserverAccountsResponse has [JsonExtensionData], so extra fields should be allowed
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"accounts":["U1234567"],"selectedAccount":"U1234567","unexpectedNewField":"surprise"}"""));

        var result = await _harness.Client.Accounts.GetAccountsAsync(
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Accounts.ShouldContain("U1234567");
    }

    [Fact]
    public async Task StrictMode_MatchingFields_PassesThrough()
    {
        _harness = await TestHarness.CreateAsync(opts =>
        {
            opts.StrictResponseValidation = true;
        });

        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/accounts",
            """{"accounts":["U1234567"],"selectedAccount":"U1234567"}""");

        var result = await _harness.Client.Accounts.GetAccountsAsync(
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Accounts.ShouldContain("U1234567");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_harness is not null)
        {
            await _harness.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
