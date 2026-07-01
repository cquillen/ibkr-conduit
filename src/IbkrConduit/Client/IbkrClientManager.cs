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

    public IReadOnlyCollection<string> ActiveTenants =>
        _tenants.Where(kv => kv.Value is not ReservedTenant).Select(kv => kv.Key).ToArray();

    public async Task<IIbkrClient> AddAsync(
        string tenantId,
        IbkrOAuthCredentials credentials,
        Action<IbkrClientOptions>? configureOverrides = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(credentials);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);

        var normalized = credentials with { TenantId = tenantId };

        var sentinel = new ReservedTenant();
        if (!_tenants.TryAdd(tenantId, sentinel))
        {
            normalized.Dispose();
            throw new InvalidOperationException($"Tenant '{tenantId}' is already active.");
        }

        try
        {
            var effective = baselineOptions.Clone();
            configureOverrides?.Invoke(effective);
            effective.Credentials = normalized;

            var tenant = await builder.BuildAsync(tenantId, normalized, effective, sharedGovernor, cancellationToken);
            if (!_tenants.TryUpdate(tenantId, tenant, sentinel))
            {
                // Our reservation was revoked mid-build (a concurrent Remove won). Don't resurrect it.
                await tenant.DisposeAsync();
                throw new InvalidOperationException($"Tenant '{tenantId}' was removed during initialization.");
            }
            return tenant.Client;
        }
        catch
        {
            // Only retract OUR sentinel — never a competing tenant that may already hold the slot.
            _tenants.TryRemove(new KeyValuePair<string, IManagedTenant>(tenantId, sentinel));
            throw;   // builder already disposed creds + provider on failure
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
        await tenant.DisposeAsync();
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }
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

    /// <summary>Placeholder entry occupying a tenant slot while its build is in flight.</summary>
    private sealed class ReservedTenant : IManagedTenant
    {
        public IIbkrClient Client => throw new InvalidOperationException("Tenant is still initializing.");
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
