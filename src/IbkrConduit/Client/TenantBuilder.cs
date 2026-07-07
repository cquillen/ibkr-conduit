using IbkrConduit.Auth;
using IbkrConduit.Diagnostics;
using IbkrConduit.Http;
using IbkrConduit.Session;
using IbkrConduit.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Client;

/// <summary>
/// Builds a tenant's child <see cref="ServiceProvider"/> by reusing
/// <see cref="ServiceCollectionExtensions.BuildTenantServices"/>, seeding the
/// shared root services, then eagerly initializing the session and WebSocket.
/// </summary>
internal sealed class TenantBuilder(ILoggerFactory loggerFactory) : ITenantBuilder
{
    public async Task<IManagedTenant> BuildAsync(
        string tenantId,
        IbkrOAuthCredentials credentials,
        IbkrClientOptions effectiveOptions,
        ISharedRateGovernor sharedGovernor,
        CancellationToken cancellationToken)
    {
        // The managed tenant owns the single bounded logout on teardown, so the child
        // session manager must not log out a second time when its provider is disposed.
        effectiveOptions.SkipLogoutOnDispose = true;

        ServiceProvider? provider = null;
        try
        {
            var services = new ServiceCollection();
            services.AddSingleton(loggerFactory);
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            services.AddSingleton(sharedGovernor);          // shared instance wins (TryAdd in pipeline)
            services.AddSingleton(new TenantContext(tenantId));

            var baseUrl = effectiveOptions.BaseUrl ?? "https://api.ibkr.com";
            ServiceCollectionExtensions.BuildTenantServices(services, credentials, effectiveOptions, baseUrl);

            provider = services.BuildServiceProvider();
            var client = provider.GetRequiredService<IIbkrClient>();
            // Eager: force session init then connect the stream.
            await provider.GetRequiredService<ISessionManager>()
                .EnsureInitializedAsync(cancellationToken);
            await provider.GetRequiredService<IIbkrWebSocketClient>()
                .ConnectAsync(cancellationToken);
            return new ManagedTenant(provider, client, credentials, effectiveOptions.LogoutTimeout);
        }
        catch
        {
            // Ownership is unconditional on failure — dispose the (possibly partially built)
            // provider AND the credentials on EVERY throw path, including a synchronous throw
            // from service construction before the provider exists (MGR-2). Success instead
            // hands credential ownership to the returned ManagedTenant.
            if (provider is not null)
            {
                await provider.DisposeAsync();
            }

            credentials.Dispose();
            throw;
        }
    }
}
