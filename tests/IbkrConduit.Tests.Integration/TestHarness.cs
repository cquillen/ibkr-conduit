using System;
using System.Net.Http;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using IbkrConduit.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration;

/// <summary>
/// Sets up a WireMock server with the full IBKR DI pipeline for integration testing.
/// Handles synthetic credentials, LST handshake registration, session init stub,
/// and DI wiring. Owns all disposable resources.
/// </summary>
public sealed class TestHarness : IAsyncDisposable, IDisposable
{
    private ServiceProvider? _provider;
    private IbkrOAuthCredentials? _credentials;

    /// <summary>The WireMock server instance. Use to register endpoint stubs.</summary>
    public WireMockServer Server { get; }

    /// <summary>The fully wired IIbkrClient backed by WireMock.</summary>
    public IIbkrClient Client { get; private set; } = null!;

    private TestHarness()
    {
        Server = WireMockServer.Start();
    }

    /// <summary>
    /// Creates and initializes a test harness with synthetic credentials,
    /// LST handshake, session init, and the full DI pipeline.
    /// </summary>
    /// <summary>
    /// Creates and initializes a test harness with synthetic credentials,
    /// LST handshake, session init, and the full DI pipeline.
    /// </summary>
    public static Task<TestHarness> CreateAsync()
    {
        var harness = new TestHarness();
        harness.Initialize();
        return Task.FromResult(harness);
    }

    private void Initialize()
    {
        _credentials = TestCredentials.Create();

        // Register the server-side DH exchange for LST acquisition
        MockLstServer.Register(Server, _credentials);

        // Stub session initialization
        Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/auth/ssodh/init")
                .UsingPost()
                .WithHeader("Authorization", $"*oauth_consumer_key=\"{TestCredentials.ConsumerKey}\"*")
                .WithHeader("Authorization", $"*oauth_token=\"{TestCredentials.AccessToken}\"*")
                .WithHeader("Authorization", "*HMAC-SHA256*")
                .WithBody(b => b != null && b.Contains("publish")))
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"authenticated":true,"competing":false,"connected":true,"passed":true,"established":true}"""));

        // Build the full DI pipeline pointing at WireMock
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddIbkrClient(_credentials, new IbkrClientOptions
        {
            BaseUrl = Server.Url!,
        });

        _provider = services.BuildServiceProvider();
        Client = _provider.GetRequiredService<IIbkrClient>();
    }

    /// <summary>
    /// Registers a WireMock stub that requires the standard OAuth Authorization header
    /// with the correct consumer key, access token, and HMAC-SHA256 signature method.
    /// </summary>
    public void StubAuthenticated(HttpMethod method, string path, string responseBody) =>
        Server.Given(
            Request.Create()
                .WithPath(path)
                .UsingMethod(method.Method)
                .WithHeader("Authorization", $"*oauth_consumer_key=\"{TestCredentials.ConsumerKey}\"*")
                .WithHeader("Authorization", $"*oauth_token=\"{TestCredentials.AccessToken}\"*")
                .WithHeader("Authorization", "*HMAC-SHA256*"))
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(responseBody));

    /// <summary>Shorthand for <see cref="StubAuthenticated"/> with GET.</summary>
    public void StubAuthenticatedGet(string path, string responseBody) =>
        StubAuthenticated(HttpMethod.Get, path, responseBody);

    /// <summary>Shorthand for <see cref="StubAuthenticated"/> with POST.</summary>
    public void StubAuthenticatedPost(string path, string responseBody) =>
        StubAuthenticated(HttpMethod.Post, path, responseBody);

    /// <summary>
    /// Verifies that the full auth handshake occurred (LST + ssodh/init).
    /// </summary>
    public void VerifyHandshakeOccurred()
    {
        Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/oauth/live_session_token").UsingPost())
            .Count.ShouldBeGreaterThan(0, "Live Session Token handshake should have been called");
        Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/iserver/auth/ssodh/init").UsingPost())
            .Count.ShouldBeGreaterThan(0, "Session init should have been called");
    }

    /// <summary>
    /// Verifies that re-authentication occurred (LST + ssodh/init called at least twice).
    /// </summary>
    public void VerifyReauthenticationOccurred()
    {
        Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/oauth/live_session_token").UsingPost())
            .Count.ShouldBeGreaterThanOrEqualTo(2,
                "LST handshake should have been called at least twice (initial + re-auth)");
        Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/iserver/auth/ssodh/init").UsingPost())
            .Count.ShouldBeGreaterThanOrEqualTo(2,
                "Session init should have been called at least twice (initial + re-auth)");
    }

    /// <summary>
    /// Verifies that all requests sent to WireMock included the User-Agent header.
    /// </summary>
    public void VerifyUserAgentOnAllRequests()
    {
        foreach (var entry in Server.LogEntries)
        {
            entry.RequestMessage.Headers.ShouldContainKey("User-Agent",
                $"Request to {entry.RequestMessage.Path} missing User-Agent header");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }

        Server.Dispose();
        _credentials?.Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _provider?.Dispose();
        Server.Dispose();
        _credentials?.Dispose();
        GC.SuppressFinalize(this);
    }
}
