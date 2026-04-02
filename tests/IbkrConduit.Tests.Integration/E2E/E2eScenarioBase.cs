using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Tests.Integration.E2E;

/// <summary>
/// Base class for E2E scenario tests. Provides DI setup and IIbkrClient creation.
/// </summary>
[ExcludeFromCodeCoverage]
public abstract class E2eScenarioBase : IAsyncDisposable
{
    private ServiceProvider? _provider;
    private IbkrOAuthCredentials? _credentials;

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

        _provider = services.BuildServiceProvider();
        var client = _provider.GetRequiredService<IIbkrClient>();
        return (_provider, client);
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
