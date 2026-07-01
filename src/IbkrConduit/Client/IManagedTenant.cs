namespace IbkrConduit.Client;

/// <summary>
/// One live tenant owned by the manager — abstracted so the manager's registry
/// and lifecycle logic is unit-testable with a fake. Disposal performs the
/// tenant's graceful teardown.
/// </summary>
internal interface IManagedTenant : IAsyncDisposable
{
    /// <summary>The tenant's client facade.</summary>
    IIbkrClient Client { get; }
}
