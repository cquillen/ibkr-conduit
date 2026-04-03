using System;
using System.Text.Json;
using System.Threading.Tasks;
using IbkrConduit.Session;
using IbkrConduit.Tests.Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Session;

/// <summary>
/// Integration tests for session management endpoints (ssodh/init, tickle, auth/status, suppress).
/// These endpoints are on the internal pipeline (no TokenRefreshHandler).
/// </summary>
public class SessionTests : IAsyncLifetime, IDisposable
{
    private TestHarness _harness = null!;
    private IIbkrSessionApi _sessionApi = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        _harness = await TestHarness.CreateAsync();

        // Override ssodh/init stub with full fixture response (higher priority than TestHarness default)
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/auth/ssodh/init")
                .UsingPost())
            .AtPriority(-1)
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Session", "POST-ssodh-init")));

        // Override tickle stub with full fixture response
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/tickle")
                .UsingPost())
            .AtPriority(-1)
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Session", "POST-tickle")));

        // Stub auth status (not stubbed by TestHarness)
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/auth/status")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Session", "GET-auth-status")));

        // Stub suppress with fixture response (higher priority)
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/questions/suppress")
                .UsingPost())
            .AtPriority(-1)
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Session", "POST-suppress")));

        // Stub suppress/reset (not stubbed by TestHarness)
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/questions/suppress/reset")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Session", "POST-suppress-reset")));

        // Initialize session (LST + ssodh/init) so OAuthSigningHandler has a valid token
        var sessionManager = _harness.GetRequiredService<ISessionManager>();
        await sessionManager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        _sessionApi = _harness.GetRequiredService<IIbkrSessionApi>();
    }

    /// <summary>
    /// Verifies GET /iserver/auth/status returns all expected fields including server info.
    /// </summary>
    [Fact]
    public async Task GetAuthStatus_ReturnsAllFields()
    {
        var result = await _sessionApi.GetAuthStatusAsync(TestContext.Current.CancellationToken);

        result.Authenticated.ShouldBeTrue();
        result.Established.ShouldBeTrue();
        result.Competing.ShouldBeFalse();
        result.Connected.ShouldBeTrue();
        result.Message.ShouldBe("");
        result.Fail.ShouldBe("");
        result.Mac.ShouldBe("AA:BB:CC:DD:EE:FF");
        result.ServerInfo.ShouldNotBeNull();
        result.ServerInfo!.ServerName.ShouldBe("TestServer01");
        result.ServerInfo!.ServerVersion.ShouldBe("Build 10.44.1d, Mar 3, 2026 1:55:32 PM");
        result.HardwareInfo.ShouldBe("test1234|AA:BB:CC:DD:EE:FF");

        _harness.VerifyUserAgentOnAllRequests();
    }

    /// <summary>
    /// Verifies POST /tickle returns session, hmds, and iserver auth status fields.
    /// </summary>
    [Fact]
    public async Task Tickle_ReturnsAllFields()
    {
        var result = await _sessionApi.TickleAsync(TestContext.Current.CancellationToken);

        result.Session.ShouldBe("abc123def456");
        result.Hmds.ShouldNotBeNull();
        result.Hmds!.Value.GetProperty("error").GetString().ShouldBe("no bridge");
        result.Iserver.ShouldNotBeNull();
        result.Iserver!.AuthStatus.ShouldNotBeNull();
        result.Iserver!.AuthStatus!.Authenticated.ShouldBeTrue();
        result.Iserver!.AuthStatus!.Established.ShouldBeTrue();
        result.Iserver!.AuthStatus!.Competing.ShouldBeFalse();
        result.Iserver!.AuthStatus!.Connected.ShouldBeTrue();
        result.Iserver!.AuthStatus!.Message.ShouldBe("");
        result.Iserver!.AuthStatus!.Mac.ShouldBe("AA:BB:CC:DD:EE:FF");
        result.Iserver!.AuthStatus!.ServerInfo.ShouldNotBeNull();
        result.Iserver!.AuthStatus!.ServerInfo!.ServerName.ShouldBe("TestServer01");
        result.Iserver!.AuthStatus!.HardwareInfo.ShouldBe("test1234|AA:BB:CC:DD:EE:FF");

        _harness.VerifyUserAgentOnAllRequests();
    }

    /// <summary>
    /// Verifies POST /iserver/questions/suppress returns status.
    /// </summary>
    [Fact]
    public async Task Suppress_ReturnsStatus()
    {
        var result = await _sessionApi.SuppressQuestionsAsync(
            new SuppressRequest(["o163"]), TestContext.Current.CancellationToken);

        result.Status.ShouldBe("submitted");
    }

    /// <summary>
    /// Verifies POST /iserver/questions/suppress/reset returns status.
    /// </summary>
    [Fact]
    public async Task SuppressReset_ReturnsStatus()
    {
        var result = await _sessionApi.ResetSuppressedQuestionsAsync(TestContext.Current.CancellationToken);

        result.Status.ShouldBe("submitted");
    }

    /// <summary>
    /// Verifies POST /iserver/auth/ssodh/init returns all fields including server info.
    /// </summary>
    [Fact]
    public async Task SsodhInit_ReturnsAllFields()
    {
        var result = await _sessionApi.InitializeBrokerageSessionAsync(
            new SsodhInitRequest(Publish: true, Compete: true), TestContext.Current.CancellationToken);

        result.Authenticated.ShouldBeTrue();
        result.Established.ShouldBeTrue();
        result.Competing.ShouldBeFalse();
        result.Connected.ShouldBeTrue();
        result.Message.ShouldBe("");
        result.Mac.ShouldBe("AA:BB:CC:DD:EE:FF");
        result.ServerInfo.ShouldNotBeNull();
        result.ServerInfo!.ServerName.ShouldBe("TestServer01");
        result.ServerInfo!.ServerVersion.ShouldBe("Build 10.44.1d, Mar 3, 2026 1:55:32 PM");
        result.HardwareInfo.ShouldBe("test1234|AA:BB:CC:DD:EE:FF");

        _harness.VerifyUserAgentOnAllRequests();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _harness.DisposeAsync();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _harness.Dispose();
        GC.SuppressFinalize(this);
    }
}
