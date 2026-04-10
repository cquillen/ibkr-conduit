using System.Threading.RateLimiting;
using IbkrConduit.Auth;
using IbkrConduit.Session;
using IbkrConduit.Streaming;

namespace IbkrConduit.Health;

/// <summary>
/// Collects health signals from all IBKR connection components and produces
/// an aggregated <see cref="IbkrHealthStatus"/> snapshot.
/// </summary>
internal sealed class HealthStatusCollector : IHealthStatusCollector
{
    private const int _globalTokenLimit = 10;

    private readonly IIbkrSessionApi _sessionApi;
    private readonly ISessionTokenProvider _tokenProvider;
    private readonly IIbkrWebSocketClient _wsClient;
    private readonly LastSuccessfulCallHandler _lastCallHandler;
    private readonly RateLimiter _globalLimiter;
    private readonly HealthStatusOptions _options;

    /// <summary>
    /// Creates a new <see cref="HealthStatusCollector"/>.
    /// </summary>
    /// <param name="sessionApi">Session API for active probes.</param>
    /// <param name="tokenProvider">Token provider for expiry checks.</param>
    /// <param name="wsClient">WebSocket client for streaming health.</param>
    /// <param name="lastCallHandler">Handler tracking last successful API call.</param>
    /// <param name="globalLimiter">Global rate limiter instance.</param>
    /// <param name="options">Health check threshold configuration.</param>
    public HealthStatusCollector(
        IIbkrSessionApi sessionApi,
        ISessionTokenProvider tokenProvider,
        IIbkrWebSocketClient wsClient,
        LastSuccessfulCallHandler lastCallHandler,
        RateLimiter globalLimiter,
        HealthStatusOptions options)
    {
        _sessionApi = sessionApi;
        _tokenProvider = tokenProvider;
        _wsClient = wsClient;
        _lastCallHandler = lastCallHandler;
        _globalLimiter = globalLimiter;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<IbkrHealthStatus> GetHealthStatusAsync(
        bool activeProbe = false, CancellationToken cancellationToken = default)
    {
        var session = activeProbe
            ? await CollectActiveSessionHealthAsync(cancellationToken)
            : CollectPassiveSessionHealth();

        var streaming = CollectStreamingHealth();
        var token = CollectTokenHealth();
        var rateLimiter = CollectRateLimiterHealth();
        var lastCall = _lastCallHandler.LastSuccessfulCall;
        var now = DateTimeOffset.UtcNow;

        var overallStatus = EvaluateOverallStatus(session, streaming, token, rateLimiter, lastCall, now);

        return new IbkrHealthStatus
        {
            OverallStatus = overallStatus,
            Session = session,
            Streaming = streaming,
            Token = token,
            RateLimiter = rateLimiter,
            LastSuccessfulCall = lastCall,
            CheckedAt = now,
        };
    }

    private static BrokerageSessionHealth CollectPassiveSessionHealth() =>
        new(Authenticated: true, Connected: true, Competing: false, Established: true, FailReason: null);

    private async Task<BrokerageSessionHealth> CollectActiveSessionHealthAsync(CancellationToken cancellationToken)
    {
        var authStatus = await _sessionApi.GetAuthStatusAsync(cancellationToken);
        return new BrokerageSessionHealth(
            Authenticated: authStatus.Authenticated,
            Connected: authStatus.Connected,
            Competing: authStatus.Competing,
            Established: authStatus.Established,
            FailReason: authStatus.Fail);
    }

    private StreamingHealth? CollectStreamingHealth()
    {
        var isConnected = _wsClient.IsConnected;
        var activeSubs = _wsClient.ActiveSubscriptionCount;

        if (!isConnected && activeSubs == 0)
        {
            return null;
        }

        return new StreamingHealth(
            IsConnected: isConnected,
            ActiveSubscriptions: activeSubs,
            LastMessageAt: _wsClient.LastMessageReceivedAt);
    }

    private OAuthTokenHealth CollectTokenHealth()
    {
        var expiry = _tokenProvider.CurrentTokenExpiry;
        if (expiry is null)
        {
            return new OAuthTokenHealth(IsExpired: false, TimeUntilExpiry: null);
        }

        var remaining = expiry.Value - DateTimeOffset.UtcNow;
        return new OAuthTokenHealth(
            IsExpired: remaining <= TimeSpan.Zero,
            TimeUntilExpiry: remaining);
    }

    private RateLimiterHealth CollectRateLimiterHealth()
    {
        var stats = _globalLimiter.GetStatistics();
        var available = (int)(stats?.CurrentAvailablePermits ?? _globalTokenLimit);
        var used = _globalTokenLimit - available;
        var utilizationPercent = (double)used / _globalTokenLimit * 100;

        return new RateLimiterHealth(
            BurstRemaining: available,
            SustainedRemaining: available,
            BurstUtilizationPercent: utilizationPercent,
            SustainedUtilizationPercent: utilizationPercent);
    }

    private HealthState EvaluateOverallStatus(
        BrokerageSessionHealth session,
        StreamingHealth? streaming,
        OAuthTokenHealth token,
        RateLimiterHealth rateLimiter,
        DateTimeOffset? lastCall,
        DateTimeOffset now)
    {
        // Unhealthy conditions
        if (!session.Authenticated || !session.Connected)
        {
            return HealthState.Unhealthy;
        }

        if (token.IsExpired)
        {
            return HealthState.Unhealthy;
        }

        if (lastCall is null && _tokenProvider.CurrentTokenExpiry is not null)
        {
            // Token exists but no successful call has ever been made — likely broken
            return HealthState.Unhealthy;
        }

        if (lastCall is not null && (now - lastCall.Value) > _options.StalenessTimeout)
        {
            return HealthState.Unhealthy;
        }

        // Degraded conditions
        if (session.Competing)
        {
            return HealthState.Degraded;
        }

        if (token.TimeUntilExpiry is not null && token.TimeUntilExpiry.Value < _options.TokenExpiryWarning)
        {
            return HealthState.Degraded;
        }

        if (streaming is { IsConnected: false, ActiveSubscriptions: > 0 })
        {
            return HealthState.Degraded;
        }

        if (rateLimiter.BurstUtilizationPercent > _options.RateLimiterThresholdPercent
            || rateLimiter.SustainedUtilizationPercent > _options.RateLimiterThresholdPercent)
        {
            return HealthState.Degraded;
        }

        return HealthState.Healthy;
    }
}
