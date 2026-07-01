using IbkrConduit.Auth;
using IbkrConduit.Session;
using Microsoft.Extensions.DependencyInjection;

namespace IbkrConduit.Client;

/// <summary>
/// Real tenant: its isolated child provider, the resolved client, and the
/// manager-owned credentials. Disposal does a best-effort IBKR logout (frees the
/// server-side session slot) BEFORE tearing down the child provider — which stops
/// the tickle timer and closes the socket — then disposes the credentials.
/// </summary>
internal sealed class ManagedTenant(
    ServiceProvider provider, IIbkrClient client, IbkrOAuthCredentials credentials) : IManagedTenant
{
    public IIbkrClient Client { get; } = client;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await provider.GetRequiredService<IIbkrSessionApi>().LogoutAsync();
        }
        catch
        {
            // Best-effort cleanup — never let a logout failure block teardown.
        }

        await provider.DisposeAsync();   // disposes session manager, tickle timer, socket, etc.
        credentials.Dispose();
    }
}
