using IbkrConduit.Auth;
using IbkrConduit.Http;
using IbkrConduit.Session;

namespace IbkrConduit.Client;

/// <summary>
/// Builds a fully-isolated, live tenant graph: child provider + eager session
/// init + WebSocket connect. Abstracted so the manager's registry/lifecycle logic
/// is unit-testable without network. The real implementation is <see cref="TenantBuilder"/>.
/// </summary>
internal interface ITenantBuilder
{
    /// <summary>
    /// Builds the child provider for <paramref name="credentials"/>, eagerly
    /// authenticates and connects, and returns the live tenant. On success,
    /// ownership of <paramref name="credentials"/> transfers to the returned
    /// <see cref="IManagedTenant"/>; the caller must not dispose them. On failure,
    /// both the partially-built provider and <paramref name="credentials"/> are
    /// disposed before the exception is re-thrown; the caller must not dispose them
    /// afterwards.
    /// </summary>
    Task<IManagedTenant> BuildAsync(
        string tenantId,
        IbkrOAuthCredentials credentials,
        IbkrClientOptions effectiveOptions,
        ISharedRateGovernor sharedGovernor,
        CancellationToken cancellationToken);
}
