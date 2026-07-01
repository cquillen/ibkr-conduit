using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace IbkrConduit.Http;

/// <summary>
/// Process-wide gate shared by every tenant, acquired before each tenant's own
/// rate limiter. The default implementation is a no-op; a future spec replaces
/// it with an adaptive IP-level governor (penalty-box back-off, concurrency
/// ceiling) without changing call sites. See the multi-tenant design spec, item B.
/// </summary>
internal interface ISharedRateGovernor
{
    /// <summary>Acquires permission to proceed with one outbound request.</summary>
    ValueTask AcquireAsync(CancellationToken cancellationToken);
}

/// <summary>Pass-through governor: imposes no shared limit.</summary>
[ExcludeFromCodeCoverage]
internal sealed class NoOpSharedRateGovernor : ISharedRateGovernor
{
    /// <inheritdoc />
    public ValueTask AcquireAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
