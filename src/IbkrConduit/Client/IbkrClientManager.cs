using System.Collections.Concurrent;
using IbkrConduit.Auth;
using IbkrConduit.Http;
using IbkrConduit.Session;

namespace IbkrConduit.Client;

/// <inheritdoc />
internal sealed class IbkrClientManager(
    ITenantBuilder builder,
    IbkrClientOptions baselineOptions,
    ISharedRateGovernor sharedGovernor) : IIbkrClientManager
{
    private readonly ConcurrentDictionary<string, IManagedTenant> _tenants = new(StringComparer.Ordinal);
    private int _disposed;

    /// <summary>
    /// Test-only synchronization seam: invoked once immediately after a tenant is installed
    /// (a successful <c>TryUpdate</c>) and before the post-install disposed re-check, so a
    /// test can deterministically interleave <see cref="DisposeAsync"/> between the two and
    /// exercise the add/dispose race (MGR-3). Null in production.
    /// </summary>
    internal Func<CancellationToken, Task>? PostInstallHookForTest { get; set; }

    public IReadOnlyCollection<string> ActiveTenants =>
        _tenants.Where(kv => kv.Value is not ReservedTenant).Select(kv => kv.Key).ToArray();

    public async Task<IIbkrClient> AddAsync(
        string tenantId,
        IbkrOAuthCredentials credentials,
        Action<IbkrClientOptions>? configureOverrides = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        // Ownership is unconditional (see IIbkrClientManager.AddAsync): every throw path from
        // here disposes the caller's credentials exactly once, so callers never do. The entry
        // guards throw BEFORE the clone exists, so they dispose the originals directly.
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        }
        catch
        {
            credentials.Dispose();
            throw;
        }

        var normalized = credentials with { TenantId = tenantId };

        var sentinel = new ReservedTenant();
        if (!_tenants.TryAdd(tenantId, sentinel))
        {
            normalized.Dispose();
            throw new InvalidOperationException($"Tenant '{tenantId}' is already active.");
        }

        var builderInvoked = false;
        try
        {
            var effective = baselineOptions.Clone();
            configureOverrides?.Invoke(effective);
            effective.Credentials = normalized;

            // Fail fast on invalid effective (post-override) options with the same
            // ArgumentException shapes as AddIbkrClient — before the sentinel-holding
            // network build (MGR-6).
            Http.ServiceCollectionExtensions.ValidateOptions(effective);

            // From here the builder owns credential disposal on any failure (it disposes them
            // on every throw path, success hands ownership to the ManagedTenant it returns).
            builderInvoked = true;
            var tenant = await builder.BuildAsync(tenantId, normalized, effective, sharedGovernor, cancellationToken);
            if (!_tenants.TryUpdate(tenantId, tenant, sentinel))
            {
                // Our reservation was revoked mid-build (a concurrent Remove won). Don't resurrect it.
                await tenant.DisposeAsync(cancellationToken);
                throw new InvalidOperationException($"Tenant '{tenantId}' was removed during initialization.");
            }

            if (PostInstallHookForTest is not null)
            {
                await PostInstallHookForTest(cancellationToken);
            }

            // The manager may have been disposed while our build ran: DisposeAsync's drain
            // could have already passed this slot. If so, tear the just-installed tenant down
            // ourselves so it can never be orphaned in a disposed manager (MGR-3).
            if (Volatile.Read(ref _disposed) == 1)
            {
                if (_tenants.TryRemove(new KeyValuePair<string, IManagedTenant>(tenantId, tenant)))
                {
                    await tenant.DisposeAsync(cancellationToken);
                }
                throw new ObjectDisposedException(GetType().FullName);
            }

            return tenant.Client;
        }
        catch
        {
            // Only retract OUR sentinel — never a competing tenant that may already hold the slot.
            _tenants.TryRemove(new KeyValuePair<string, IManagedTenant>(tenantId, sentinel));

            // If the builder ran it already disposed the credentials (or handed them to a
            // tenant we tore down above); otherwise the failure preceded builder ownership,
            // so dispose them here (MGR-2).
            if (!builderInvoked)
            {
                normalized.Dispose();
            }
            throw;
        }
    }

    public bool TryGetClient(string tenantId, out IIbkrClient client)
    {
        if (_tenants.TryGetValue(tenantId, out var tenant) && tenant is not ReservedTenant)
        {
            client = tenant.Client;
            return true;
        }
        client = null!;
        return false;
    }

    public IIbkrClient GetClient(string tenantId) =>
        TryGetClient(tenantId, out var client)
            ? client
            : throw new KeyNotFoundException($"Tenant '{tenantId}' is not active.");

    public async Task<bool> RemoveAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        if (!_tenants.TryRemove(tenantId, out var tenant) || tenant is ReservedTenant)
        {
            return false;
        }
        // Thread the caller's token into teardown: a cancelled/short-timeout token abandons
        // the best-effort logout promptly while the tenant's resources are still disposed (MGR-1).
        await tenant.DisposeAsync(cancellationToken);
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }
        // Drain until the registry is empty rather than iterating a one-shot snapshot: a tenant
        // (or sentinel) installed by an AddAsync racing this dispose is still torn down. The
        // racing AddAsync's post-install disposed re-check covers the symmetric window (MGR-3).
        while (!_tenants.IsEmpty)
        {
            foreach (var id in _tenants.Keys.ToArray())
            {
                if (_tenants.TryRemove(id, out var tenant) && tenant is not ReservedTenant)
                {
                    try
                    {
                        await tenant.DisposeAsync();
                    }
                    catch
                    {
                        // A single tenant's teardown failure must not strand the others.
                    }
                }
            }
        }
    }

    /// <summary>Placeholder entry occupying a tenant slot while its build is in flight.</summary>
    private sealed class ReservedTenant : IManagedTenant
    {
        public IIbkrClient Client => throw new InvalidOperationException("Tenant is still initializing.");
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public ValueTask DisposeAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
