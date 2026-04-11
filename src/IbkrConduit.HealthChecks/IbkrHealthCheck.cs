using IbkrConduit.Client;
using IbkrConduit.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace IbkrConduit.HealthChecks;

/// <summary>
/// ASP.NET Core health check that reports IBKR connection status by delegating
/// to <see cref="IIbkrClient.GetHealthStatusAsync"/>.
/// </summary>
public sealed class IbkrHealthCheck : IHealthCheck
{
    private readonly IIbkrClient _client;
    private readonly IbkrHealthCheckOptions _options;

    /// <summary>
    /// Creates a new <see cref="IbkrHealthCheck"/>.
    /// </summary>
    /// <param name="client">The IBKR client to check health for.</param>
    /// <param name="options">Health check configuration options.</param>
    public IbkrHealthCheck(IIbkrClient client, IOptions<IbkrHealthCheckOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var status = await _client.GetHealthStatusAsync(_options.ActiveProbe, cancellationToken);
            var data = BuildData(status);

            return status.OverallStatus switch
            {
                HealthState.Healthy => HealthCheckResult.Healthy("IBKR connection is healthy.", data),
                HealthState.Degraded => HealthCheckResult.Degraded("IBKR connection is degraded.", data: data),
                HealthState.Unhealthy => HealthCheckResult.Unhealthy("IBKR connection is unhealthy.", data: data),
                _ => HealthCheckResult.Unhealthy("Unknown health state.", data: data),
            };
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("IBKR health check failed with an exception.", ex);
        }
    }

    private static Dictionary<string, object> BuildData(IbkrHealthStatus status)
    {
        var data = new Dictionary<string, object>
        {
            ["overallStatus"] = status.OverallStatus.ToString(),
            ["checkedAt"] = status.CheckedAt,
            ["session.authenticated"] = status.Session.Authenticated,
            ["session.connected"] = status.Session.Connected,
            ["session.competing"] = status.Session.Competing,
            ["session.established"] = status.Session.Established,
            ["token.isExpired"] = status.Token.IsExpired,
            ["rateLimiter.burstUtilizationPercent"] = status.RateLimiter.BurstUtilizationPercent,
            ["rateLimiter.sustainedUtilizationPercent"] = status.RateLimiter.SustainedUtilizationPercent,
        };

        if (status.Session.FailReason is not null)
        {
            data["session.failReason"] = status.Session.FailReason;
        }

        if (status.Token.TimeUntilExpiry is not null)
        {
            data["token.timeUntilExpiry"] = status.Token.TimeUntilExpiry.Value.ToString();
        }

        if (status.Streaming is not null)
        {
            data["streaming.isConnected"] = status.Streaming.IsConnected;
            data["streaming.activeSubscriptions"] = status.Streaming.ActiveSubscriptions;

            if (status.Streaming.LastMessageAt is not null)
            {
                data["streaming.lastMessageAt"] = status.Streaming.LastMessageAt.Value;
            }
        }

        if (status.LastSuccessfulCall is not null)
        {
            data["lastSuccessfulCall"] = status.LastSuccessfulCall.Value;
        }

        return data;
    }
}
