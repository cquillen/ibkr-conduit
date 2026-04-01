using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using IbkrConduit.Tests.Integration.Recording;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Tests.Integration.E2E;

/// <summary>
/// Base class for E2E scenario tests. Provides DI setup, IIbkrClient creation,
/// and optional response recording infrastructure.
/// </summary>
[ExcludeFromCodeCoverage]
public abstract class E2eScenarioBase : IAsyncDisposable
{
    private ServiceProvider? _provider;
    private IbkrOAuthCredentials? _credentials;

    /// <summary>
    /// Recording context shared across all DI containers in this test instance.
    /// Always initialized so subclasses can register it in custom DI pipelines.
    /// </summary>
    protected readonly RecordingContext _recordingContext = new();

    /// <summary>
    /// Creates the full DI pipeline and returns the IIbkrClient facade.
    /// Also returns the ServiceProvider for resolving internal APIs (e.g., IIbkrSessionApi).
    /// </summary>
    protected (ServiceProvider Provider, IIbkrClient Client) CreateClient()
    {
        _credentials = OAuthCredentialsFactory.FromEnvironment();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIbkrClient(_credentials);

        // Register recording infrastructure if IBKR_RECORD_RESPONSES=true
        services.AddSingleton(_recordingContext);
        if (string.Equals(
            Environment.GetEnvironmentVariable("IBKR_RECORD_RESPONSES"),
            "true", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IHttpMessageHandlerBuilderFilter>(
                new RecordingHandlerFilter(_recordingContext));
        }

        _provider = services.BuildServiceProvider();
        var client = _provider.GetRequiredService<IIbkrClient>();
        return (_provider, client);
    }

    /// <summary>
    /// Begins recording API interactions under the given scenario name.
    /// </summary>
    protected void StartRecording(string scenarioName)
    {
        _recordingContext.Reset(scenarioName);
    }

    /// <summary>
    /// Stops recording (sets scenario name to null so handler becomes no-op).
    /// </summary>
    protected void StopRecording()
    {
        _recordingContext.ScenarioName = null;
    }

    /// <summary>
    /// Convenience accessor for the CancellationToken from the test context.
    /// </summary>
    protected static CancellationToken CT => TestContext.Current.CancellationToken;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }

        _credentials?.Dispose();
        GC.SuppressFinalize(this);
    }
}
