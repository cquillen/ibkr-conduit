using System.Diagnostics.CodeAnalysis;

namespace IbkrConduit.Health;

/// <summary>
/// Aggregated health status snapshot of the IBKR connection.
/// </summary>
[ExcludeFromCodeCoverage]
public record IbkrHealthStatus
{
    /// <summary>Overall health assessment.</summary>
    public required HealthState OverallStatus { get; init; }

    /// <summary>Brokerage session health.</summary>
    public required BrokerageSessionHealth Session { get; init; }

    /// <summary>WebSocket streaming health, or null if streaming is not active.</summary>
    public required StreamingHealth? Streaming { get; init; }

    /// <summary>OAuth token health.</summary>
    public required OAuthTokenHealth Token { get; init; }

    /// <summary>Rate limiter health.</summary>
    public required RateLimiterHealth RateLimiter { get; init; }

    /// <summary>Timestamp of the last successful API call, or null if none recorded.</summary>
    public required DateTimeOffset? LastSuccessfulCall { get; init; }

    /// <summary>When this health check was performed.</summary>
    public required DateTimeOffset CheckedAt { get; init; }
}

/// <summary>
/// Health status of the brokerage session.
/// </summary>
/// <param name="Authenticated">Whether the session is authenticated.</param>
/// <param name="Connected">Whether the session is connected to the backend.</param>
/// <param name="Competing">Whether this session is competing with another.</param>
/// <param name="Established">Whether the session has been fully established.</param>
/// <param name="FailReason">Failure reason, if any.</param>
[ExcludeFromCodeCoverage]
public record BrokerageSessionHealth(
    bool Authenticated,
    bool Connected,
    bool Competing,
    bool Established,
    string? FailReason);

/// <summary>
/// Health status of the WebSocket streaming connection.
/// </summary>
/// <param name="IsConnected">Whether the WebSocket connection is currently open.</param>
/// <param name="ActiveSubscriptions">Number of active topic subscriptions.</param>
/// <param name="LastMessageAt">Timestamp of the last received WebSocket message, or null.</param>
[ExcludeFromCodeCoverage]
public record StreamingHealth(
    bool IsConnected,
    int ActiveSubscriptions,
    DateTimeOffset? LastMessageAt);

/// <summary>
/// Health status of the OAuth token.
/// </summary>
/// <param name="IsExpired">Whether the current token has expired.</param>
/// <param name="TimeUntilExpiry">Time remaining until the token expires, or null if no token exists.</param>
[ExcludeFromCodeCoverage]
public record OAuthTokenHealth(
    bool IsExpired,
    TimeSpan? TimeUntilExpiry);

/// <summary>
/// Health status of the rate limiter. The library uses a single global token bucket
/// rate limiter, so both burst and sustained fields report the same values.
/// </summary>
/// <param name="BurstRemaining">Remaining tokens in the global rate limiter (burst view).</param>
/// <param name="SustainedRemaining">Remaining tokens in the global rate limiter (sustained view).</param>
/// <param name="BurstUtilizationPercent">Percentage of global limiter capacity used (burst view).</param>
/// <param name="SustainedUtilizationPercent">Percentage of global limiter capacity used (sustained view).</param>
[ExcludeFromCodeCoverage]
public record RateLimiterHealth(
    int BurstRemaining,
    int SustainedRemaining,
    double BurstUtilizationPercent,
    double SustainedUtilizationPercent);

/// <summary>
/// Configuration options for health status thresholds.
/// </summary>
[ExcludeFromCodeCoverage]
public class HealthStatusOptions
{
    /// <summary>
    /// Time before token expiry that triggers a degraded warning. Default: 5 minutes.
    /// </summary>
    public TimeSpan TokenExpiryWarning { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Rate limiter utilization percentage threshold for degraded status. Default: 80%.
    /// </summary>
    public double RateLimiterThresholdPercent { get; set; } = 80;

    /// <summary>
    /// Duration after which a stale last-successful-call triggers unhealthy status. Default: 120 seconds.
    /// </summary>
    public TimeSpan StalenessTimeout { get; set; } = TimeSpan.FromSeconds(120);
}
