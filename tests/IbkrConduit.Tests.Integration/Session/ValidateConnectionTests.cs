using System;
using System.Threading.Tasks;
using IbkrConduit.Errors;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Session;

public class ValidateConnectionTests : IAsyncDisposable
{
    private TestHarness? _harness;

    [Fact]
    public async Task ValidateConnectionAsync_ValidCredentials_Succeeds()
    {
        _harness = await TestHarness.CreateAsync();

        await _harness.Client.ValidateConnectionAsync(TestContext.Current.CancellationToken);

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task ValidateConnectionAsync_LstEndpointReturns401_ThrowsConfigurationException()
    {
        _harness = await TestHarness.CreateAsync();

        // Override the LST endpoint to return 401 at highest priority
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/oauth/live_session_token")
                .UsingPost())
            .AtPriority(-1)
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => _harness.Client.ValidateConnectionAsync(TestContext.Current.CancellationToken));

        ex.CredentialHint.ShouldBe("ConsumerKey, AccessToken");
        ex.InnerException.ShouldNotBeNull();
    }

    [Fact]
    public async Task ValidateConnectionAsync_SsodhInitReturns401_ThrowsConfigurationException()
    {
        _harness = await TestHarness.CreateAsync();

        // Override ssodh/init to return 401 at highest priority
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/auth/ssodh/init")
                .UsingPost())
            .AtPriority(-1)
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => _harness.Client.ValidateConnectionAsync(TestContext.Current.CancellationToken));

        ex.InnerException.ShouldNotBeNull();
    }

    [Fact]
    public async Task ValidateConnectionAsync_AlreadyInitialized_ReturnsImmediately()
    {
        _harness = await TestHarness.CreateAsync();

        // First call initializes
        await _harness.Client.ValidateConnectionAsync(TestContext.Current.CancellationToken);

        // Second call should return immediately (idempotent) — no extra LST calls
        await _harness.Client.ValidateConnectionAsync(TestContext.Current.CancellationToken);

        // Only one LST handshake
        _harness.Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/oauth/live_session_token").UsingPost())
            .Count.ShouldBe(1);
    }

    public async ValueTask DisposeAsync()
    {
        if (_harness != null)
        {
            await _harness.DisposeAsync();
        }
    }
}
