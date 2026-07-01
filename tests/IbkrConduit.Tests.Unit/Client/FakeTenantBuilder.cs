using System;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using IbkrConduit.Session;

namespace IbkrConduit.Tests.Unit.Client;

// Records build/dispose without any I/O. Returns IManagedTenant (from Task 6),
// so the fake needs no real ServiceProvider.
internal sealed class FakeTenantBuilder : ITenantBuilder
{
    public bool ThrowOnBuild { get; set; }

    // When set, BuildAsync suspends on this gate before completing, letting a test
    // hold a build in flight (e.g. to race a Remove against it).
    public TaskCompletionSource? Gate { get; set; }

    public List<FakeManagedTenant> Built { get; } = new();
    public FakeManagedTenant? LastTenant => Built.Count > 0 ? Built[^1] : null;

    public async Task<IManagedTenant> BuildAsync(
        string tenantId, IbkrOAuthCredentials credentials, IbkrClientOptions effectiveOptions,
        ISharedRateGovernor sharedGovernor, CancellationToken cancellationToken)
    {
        if (ThrowOnBuild)
        {
            credentials.Dispose();                       // builder owns cleanup on failure (per ITenantBuilder contract)
            throw new InvalidOperationException("auth failed");
        }
        if (Gate is not null)
        {
            await Gate.Task;
        }
        var tenant = new FakeManagedTenant();
        Built.Add(tenant);
        return tenant;
    }
}

internal sealed class FakeManagedTenant : IManagedTenant
{
    public bool Disposed { get; private set; }
    public IIbkrClient Client { get; } = new StubIbkrClient();
    public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
}
