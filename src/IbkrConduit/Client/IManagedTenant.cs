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

    /// <summary>
    /// Graceful teardown honoring <paramref name="cancellationToken"/>: the best-effort
    /// logout is abandoned promptly on cancellation while the tenant's resources are still
    /// disposed. This is the token-threaded entry point for <c>RemoveAsync</c>; the
    /// parameterless <see cref="IAsyncDisposable.DisposeAsync"/> delegates here with
    /// <see cref="System.Threading.CancellationToken.None"/> (bounded by an internal cap).
    /// Kept on this <em>internal</em> interface only — no public surface grows (VCR-08 / MGR-1).
    /// </summary>
    ValueTask DisposeAsync(CancellationToken cancellationToken);
}
