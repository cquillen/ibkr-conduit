using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IbkrConduit.HealthChecks;

/// <summary>
/// Extension methods for registering the IBKR health check.
/// </summary>
[ExcludeFromCodeCoverage]
public static class HealthChecksBuilderExtensions
{
    /// <summary>
    /// Adds the IBKR connection health check.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="configure">Optional configuration for health check options.</param>
    /// <param name="name">The name of the health check. Default: "ibkr".</param>
    /// <param name="failureStatus">The failure status to use. Default: null (uses framework default).</param>
    /// <param name="tags">Tags for the health check.</param>
    /// <returns>The health checks builder for chaining.</returns>
    public static IHealthChecksBuilder AddIbkrHealthCheck(
        this IHealthChecksBuilder builder,
        Action<IbkrHealthCheckOptions>? configure = null,
        string name = "ibkr",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        if (configure is not null)
        {
            builder.Services.Configure(configure);
        }

        return builder.AddCheck<IbkrHealthCheck>(name, failureStatus, tags ?? []);
    }
}
