using IbkrConduit.Auth;
using IbkrConduit.Session;

namespace IbkrConduit.Client;

/// <summary>
/// Manages multiple isolated IbkrConduit instances — one per credential/tenant —
/// in a single process, with runtime add/remove. See the multi-tenant design spec.
/// </summary>
public interface IIbkrClientManager : IAsyncDisposable
{
    /// <summary>
    /// Adds a tenant: builds its isolated graph, eagerly authenticates and connects
    /// the WebSocket, and returns its client. Throws <see cref="InvalidOperationException"/>
    /// if <paramref name="tenantId"/> is already active; throws (and leaves nothing
    /// registered) if authentication fails. The manager takes ownership of
    /// <paramref name="credentials"/> and disposes them on remove/dispose.
    /// Ownership is unconditional: <paramref name="credentials"/> are disposed even
    /// when this call throws — both on the already-active path and the
    /// authentication-failure path — so callers must never dispose them themselves.
    /// </summary>
    Task<IIbkrClient> AddAsync(
        string tenantId,
        IbkrOAuthCredentials credentials,
        Action<IbkrClientOptions>? configureOverrides = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a tenant's client, or <c>false</c> if not active.</summary>
    bool TryGetClient(string tenantId, out IIbkrClient client);

    /// <summary>Gets a tenant's client; throws if not active.</summary>
    IIbkrClient GetClient(string tenantId);

    /// <summary>Tears a tenant down (cancel in-flight, close socket, logout, dispose). Returns <c>false</c> if not active.</summary>
    Task<bool> RemoveAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>The currently active tenant ids.</summary>
    IReadOnlyCollection<string> ActiveTenants { get; }
}
