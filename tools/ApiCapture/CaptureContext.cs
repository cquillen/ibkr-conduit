using ApiCapture.Recording;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using IbkrConduit.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ApiCapture;

/// <summary>
/// Shared infrastructure for all capture commands. Loads credentials, builds an
/// authenticated <see cref="HttpClient"/> with recording support, and initializes
/// the brokerage session.
/// </summary>
public sealed class CaptureContext : IAsyncDisposable
{
    private ServiceProvider? _provider;
    private IbkrOAuthCredentials? _credentials;

    /// <summary>
    /// The <see cref="HttpClient"/> wired with recording and OAuth signing handlers.
    /// Use this for all capture HTTP calls.
    /// </summary>
    public HttpClient CaptureClient { get; private set; } = null!;

    /// <summary>
    /// The recording handler, exposed so callers can access <see cref="RecordingDelegatingHandler.LastWrittenPath"/>.
    /// </summary>
    public RecordingDelegatingHandler RecordingHandler { get; private set; } = null!;

    /// <summary>
    /// The recording context that tracks scenario state and step counters.
    /// </summary>
    public RecordingContext Recording { get; } = new();

    /// <summary>
    /// The primary account ID discovered during initialization.
    /// </summary>
    public string AccountId { get; private set; } = string.Empty;

    /// <summary>
    /// Initializes the capture context by loading credentials, establishing the
    /// brokerage session, and building the recording HTTP client pipeline.
    /// </summary>
    /// <param name="outputDirectory">Directory where recording JSON files are written.</param>
    public async Task InitializeAsync(string outputDirectory)
    {
        _credentials = OAuthCredentialsFactory.FromEnvironment();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddIbkrClient(_credentials);
        _provider = services.BuildServiceProvider();

        // Use the library client to init session and discover account
        var client = _provider.GetRequiredService<IIbkrClient>();
        var accounts = await client.Portfolio.GetAccountsAsync();
        AccountId = accounts[0].Id;

        Console.WriteLine($"Session initialized. Account: {AccountId}");

        // Build a raw HttpClient with recording + signing for captures
        var tokenProvider = _provider.GetRequiredService<ISessionTokenProvider>();

        RecordingHandler = new RecordingDelegatingHandler(Recording, outputDirectory)
        {
            InnerHandler = new OAuthSigningHandler(
                tokenProvider,
                _credentials.ConsumerKey,
                _credentials.AccessToken)
            {
                InnerHandler = new HttpClientHandler(),
            },
        };

        CaptureClient = new HttpClient(RecordingHandler)
        {
            BaseAddress = new Uri("https://api.ibkr.com"),
        };
    }

    /// <summary>
    /// Disposes the service provider, credentials, and HTTP client.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        CaptureClient?.Dispose();
        _credentials?.Dispose();

        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }
    }
}
