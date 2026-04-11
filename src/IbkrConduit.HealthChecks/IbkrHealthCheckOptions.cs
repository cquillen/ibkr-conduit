using System.Diagnostics.CodeAnalysis;

namespace IbkrConduit.HealthChecks;

/// <summary>
/// Configuration options for the IBKR health check.
/// Threshold configuration (token expiry, rate limiter, staleness) belongs on
/// <c>HealthStatusOptions</c> in the core library and is configured at
/// <c>AddIbkrClient()</c> registration time.
/// </summary>
[ExcludeFromCodeCoverage]
public class IbkrHealthCheckOptions
{
    /// <summary>When true, makes a live API call on each health check. Default: false.</summary>
    public bool ActiveProbe { get; set; }
}
