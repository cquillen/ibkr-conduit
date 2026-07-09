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
        ISessionManager? sessionManager = null;
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
            sessionManager = provider.GetRequiredService<ISessionManager>();
            // Eager: force session init then connect the stream.
            await sessionManager.EnsureInitializedAsync(cancellationToken);
            await provider.GetRequiredService<IIbkrWebSocketClient>()
                .ConnectAsync(cancellationToken);
            return new ManagedTenant(provider, client, credentials, effectiveOptions.LogoutTimeout);
        }
        catch
        {
            // TEN-1: eager init above already suppressed the child SessionManager's own
            // dispose-time logout (SkipLogoutOnDispose), on the assumption that the returned
            // ManagedTenant would own the single bounded logout. If a LATER step in this method
            // fails (e.g. the eager WebSocket connect) after the session was already brought up,
            // no ManagedTenant is ever returned to own that logout — so issue the same bounded
            // best-effort logout here, before the provider that owns the HTTP pipeline is torn
            // down. Bounded by (caller token ∪ effectiveOptions.LogoutTimeout), same as
            // ManagedTenant.DisposeAsync, so a hung logout can never block this failure path.
            //
            // FO-2: gate on the session manager's own SessionEstablished flag — set the moment
            // ssodh reports authenticated=true — NOT a local flag set only after
            // EnsureInitializedAsync RETURNS. This way, if a post-authenticated=true init step
            // (suppression / tickle-start) throws out of EnsureInitializedAsync, the server session
            // is still torn down here rather than leaked.
            if (sessionManager?.SessionEstablished == true && provider is not null)
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linked.CancelAfter(effectiveOptions.LogoutTimeout);
                try
                {
                    await provider.GetRequiredService<IIbkrSessionApi>().LogoutAsync(linked.Token);
                }
                catch
                {
                    // Best-effort cleanup — a logout failure or cancellation must never block teardown.
                }
            }

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
