namespace IbkrConduit.HealthChecks;

/// <summary>
/// Configuration options for the IBKR health check.
/// </summary>
public class IbkrHealthCheckOptions
{
    /// <summary>When true, makes a live API call on each health check. Default: false.</summary>
    public bool ActiveProbe { get; set; }

    /// <summary>Token expiry warning threshold. Default: 5 minutes.</summary>
    public TimeSpan TokenExpiryWarning { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Rate limiter utilization threshold percentage (0-100). Default: 80.</summary>
    public double RateLimiterThresholdPercent { get; set; } = 80;

    /// <summary>Maximum time since last successful API call before unhealthy. Default: 120 seconds.</summary>
    public TimeSpan StalenessTimeout { get; set; } = TimeSpan.FromSeconds(120);
}
