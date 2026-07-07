using IbkrConduit.Auth;
using IbkrConduit.Session;
using Microsoft.Extensions.DependencyInjection;

namespace IbkrConduit.Client;

/// <summary>
/// Real tenant: its isolated child provider, the resolved client, and the
/// manager-owned credentials. Disposal does a single best-effort IBKR logout (frees the
/// server-side session slot) BEFORE tearing down the child provider — which stops the
/// tickle timer and closes the socket — then disposes the credentials. The logout runs
/// under a linked CTS (caller token ∪ an internal cap) so it can be abandoned promptly on
/// cancellation and can never block teardown for minutes, even with no caller token. The
/// child <see cref="SessionManager"/>'s own dispose-time logout is suppressed for this
/// path (<see cref="IbkrClientOptions.SkipLogoutOnDispose"/>), so exactly one logout is
/// issued (VCR-08 / MGR-1).
/// </summary>
internal sealed class ManagedTenant(
    ServiceProvider provider,
    IIbkrClient client,
    IbkrOAuthCredentials credentials,
    TimeSpan logoutTimeout) : IManagedTenant
{
    public IIbkrClient Client { get; } = client;

    public ValueTask DisposeAsync() => DisposeAsync(CancellationToken.None);

    public async ValueTask DisposeAsync(CancellationToken cancellationToken)
    {
        // Best-effort logout bounded by (caller token ∪ internal cap). Cancellation (or the
        // cap firing) abandons ONLY the logout — the child provider and credentials are
        // always disposed below.
        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            linked.CancelAfter(logoutTimeout);
            try
            {
                await provider.GetRequiredService<IIbkrSessionApi>().LogoutAsync(linked.Token);
            }
            catch
            {
                // Best-effort cleanup — a logout failure or cancellation must never block teardown.
            }
        }

        await provider.DisposeAsync();   // disposes session manager, tickle timer, socket, etc.
        credentials.Dispose();
    }
}
