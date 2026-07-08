using System;
using System.Linq;
using System.Threading.RateLimiting;
using IbkrConduit.Accounts;
using IbkrConduit.Alerts;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Contracts;
using IbkrConduit.Diagnostics;
using IbkrConduit.EventContracts;
using IbkrConduit.Fyi;
using IbkrConduit.Health;
using IbkrConduit.MarketData;
using IbkrConduit.Orders;
using IbkrConduit.Portfolio;
using IbkrConduit.Session;
using IbkrConduit.Streaming;
using IbkrConduit.Watchlists;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IbkrConduit.Http;

/// <summary>
/// Extension methods for registering IBKR API clients with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    private const string _ibkrBaseUrl = "https://api.ibkr.com";

    /// <summary>
    /// Registers the full IBKR API client pipeline including all Refit interfaces,
    /// operations implementations, and the unified <see cref="IIbkrClient"/> facade.
    /// Consumer pipeline: Refit -> TokenRefreshHandler -> ResponseSchemaValidationHandler ->
    /// GlobalRateLimitingHandler -> EndpointRateLimitingHandler -> OAuthSigningHandler -> HttpClient.
    /// Internal session pipeline: Refit -> GlobalRateLimitingHandler ->
    /// EndpointRateLimitingHandler -> OAuthSigningHandler -> HttpClient (no TokenRefreshHandler).
    /// </summary>
    public static IServiceCollection AddIbkrClient(
        this IServiceCollection services,
        Action<IbkrClientOptions> configure)
    {
        if (services.Any(d => d.ServiceType == typeof(IbkrClientRegistrationMarker)))
        {
            throw new InvalidOperationException(
                "AddIbkrClient has already been called on this IServiceCollection. " +
                "Register at most one IbkrConduit per IServiceProvider, or use " +
                "IIbkrClientManager (AddIbkrClientManager) to host multiple accounts.");
        }

        services.AddSingleton<IbkrClientRegistrationMarker>();

        var clientOptions = new IbkrClientOptions();
        configure(clientOptions);

        ValidateOptions(clientOptions);

        var credentials = clientOptions.Credentials!;
        var baseUrl = clientOptions.BaseUrl ?? _ibkrBaseUrl;

        BuildTenantServices(services, credentials, clientOptions, baseUrl);
        return services;
    }

    /// <summary>Marker proving AddIbkrClientManager has already run.</summary>
    private sealed class IbkrClientManagerRegistrationMarker;

    /// <summary>
    /// Registers the multi-tenant <see cref="IIbkrClientManager"/> singleton with the
    /// given baseline options applied to every tenant. Credentials are supplied per
    /// tenant via <see cref="IIbkrClientManager.AddAsync"/>, not here.
    /// </summary>
    public static IServiceCollection AddIbkrClientManager(
        this IServiceCollection services,
        Action<IbkrClientOptions>? configureBaseline = null)
    {
        if (services.Any(d => d.ServiceType == typeof(IbkrClientManagerRegistrationMarker)))
        {
            throw new InvalidOperationException("AddIbkrClientManager has already been called on this IServiceCollection.");
        }
        services.AddSingleton<IbkrClientManagerRegistrationMarker>();

        var baseline = new IbkrClientOptions();
        configureBaseline?.Invoke(baseline);

        services.TryAddSingleton<ISharedRateGovernor, NoOpSharedRateGovernor>();
        services.AddSingleton<ITenantBuilder, TenantBuilder>();
        services.AddSingleton<IIbkrClientManager>(sp =>
            new IbkrClientManager(
                sp.GetRequiredService<ITenantBuilder>(),
                baseline,
                sp.GetRequiredService<ISharedRateGovernor>()));

        return services;
    }

    /// <summary>
    /// Registers one fully-isolated IbkrConduit graph (all Refit pipelines,
    /// operations, session lifecycle, health, and the IIbkrClient facade) into
    /// <paramref name="services"/> for a single tenant's credentials. Shared by
    /// the single-account AddIbkrClient path and IIbkrClientManager's per-tenant
    /// child providers, so both build an identical graph.
    /// </summary>
    internal static void BuildTenantServices(
        IServiceCollection services,
        IbkrOAuthCredentials credentials,
        IbkrClientOptions clientOptions,
        string baseUrl)
    {
        // Per-provider tenant identity for telemetry tagging. TryAdd so a future
        // manager-seeded TenantContext (registered before this call) takes precedence;
        // single-account usage falls back to the credentials' TenantId.
        services.TryAddSingleton(new TenantContext(credentials.TenantId));

        // Response schema validation map (built once, used by all consumer pipelines)
        var endpointMap = RefitEndpointMap.Build([
            typeof(IIbkrPortfolioApi),
            typeof(IIbkrContractApi),
            typeof(IIbkrOrderApi),
            typeof(IIbkrMarketDataApi),
            typeof(IIbkrAccountApi),
            typeof(IIbkrAlertApi),
            typeof(IIbkrWatchlistApi),
            typeof(IIbkrFyiApi),
            typeof(IIbkrEventContractApi),
        ]);
        services.AddSingleton(endpointMap);

        RateLimitingAndResilienceRegistration.Register(services);
        SessionServiceRegistration.Register(services, credentials, clientOptions, baseUrl);
        ConsumerPipelineRegistration.Register(services, credentials, clientOptions, endpointMap, baseUrl);
        StreamingAndFlexRegistration.Register(services, credentials, clientOptions, baseUrl);

        // Per-tenant diagnostics: owns the rate-limiter queue-depth gauge on a per-tenant Meter,
        // registered once against the RateLimiter singleton and disposed with the provider so
        // tenant churn accumulates no stale, untagged gauges (VCR-09 / MGR-4).
        services.AddSingleton(sp => new TenantDiagnostics(
            sp.GetRequiredService<TenantContext>(),
            sp.GetRequiredService<RateLimiter>()));

        // Health check infrastructure
        services.AddSingleton(_ => new LastSuccessfulCallTracker(TimeProvider.System));
        services.AddSingleton(BuildHealthStatusOptions(clientOptions));
        services.AddSingleton<SessionHealthState>();
        services.AddSingleton<IHealthStatusCollector>(sp =>
            new HealthStatusCollector(
                sp.GetRequiredService<IIbkrSessionApi>(),
                sp.GetRequiredService<ISessionTokenProvider>(),
                sp.GetRequiredService<IIbkrWebSocketClient>(),
                sp.GetRequiredService<LastSuccessfulCallTracker>(),
                sp.GetRequiredService<RateLimiter>(),
                sp.GetRequiredService<HealthStatusOptions>(),
                sp.GetRequiredService<SessionHealthState>(),
                TimeProvider.System));

        // Unified facade. Resolving TenantDiagnostics here — via the always-resolved facade, in
        // both the single-client and manager build paths — registers the per-tenant queue-depth
        // gauge exactly once and ties its lifetime to the provider (VCR-09 / MGR-4).
        services.AddSingleton<IIbkrClient>(sp =>
        {
            sp.GetRequiredService<TenantDiagnostics>();
            return ActivatorUtilities.CreateInstance<IbkrClient>(sp);
        });
    }

    /// <summary>
    /// The health staleness window defaults to this multiple of the tenant's tickle
    /// interval, so a longer <see cref="IbkrClientOptions.TickleIntervalSeconds"/> cannot
    /// silently outrun the staleness window (design doc §7.7, PVR-07). With the default
    /// 60-second tickle interval this yields the historical 120-second default.
    /// </summary>
    private const int _stalenessTickleIntervalMultiplier = 2;

    /// <summary>
    /// Builds the per-tenant <see cref="HealthStatusOptions"/>: the staleness threshold is
    /// derived from the tenant's tickle interval, then the consumer's
    /// <see cref="IbkrClientOptions.ConfigureHealthStatus"/> hook (if any) overrides any
    /// threshold. Runs on both facade paths via <see cref="BuildTenantServices"/>.
    /// </summary>
    private static HealthStatusOptions BuildHealthStatusOptions(IbkrClientOptions clientOptions)
    {
        var health = new HealthStatusOptions
        {
            StalenessTimeout = TimeSpan.FromSeconds(
                (long)clientOptions.TickleIntervalSeconds * _stalenessTickleIntervalMultiplier),
        };
        clientOptions.ConfigureHealthStatus?.Invoke(health);
        return health;
    }

    /// <summary>Marker proving AddIbkrClient has already run on a collection.</summary>
    private sealed class IbkrClientRegistrationMarker;

    /// <summary>
    /// Validates all <see cref="IbkrClientOptions"/> fields to fail fast on
    /// misconfiguration. Called at registration time by <see cref="AddIbkrClient"/> and,
    /// for the manager path, by <c>IbkrClientManager.AddAsync</c> on the effective
    /// (cloned + per-tenant-overridden) options before any network build.
    /// </summary>
    // CA2208: paramName values intentionally use "IbkrClientOptions.Property" paths
    // so consumers see which option is invalid, not the method parameter name "options".
#pragma warning disable CA2208
    internal static void ValidateOptions(IbkrClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options.Credentials, "IbkrClientOptions.Credentials");

        if (options.TickleIntervalSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                "IbkrClientOptions.TickleIntervalSeconds",
                options.TickleIntervalSeconds,
                "TickleIntervalSeconds must be greater than zero.");
        }

        if (options.TickleFailureIntervalSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                "IbkrClientOptions.TickleFailureIntervalSeconds",
                options.TickleFailureIntervalSeconds,
                "TickleFailureIntervalSeconds must be greater than zero.");
        }

        if (options.WebSocketHeartbeatIntervalSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                "IbkrClientOptions.WebSocketHeartbeatIntervalSeconds",
                options.WebSocketHeartbeatIntervalSeconds,
                "WebSocketHeartbeatIntervalSeconds must be greater than zero.");
        }

        if (options.StreamingBufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                "IbkrClientOptions.StreamingBufferSize",
                options.StreamingBufferSize,
                "StreamingBufferSize must be greater than zero.");
        }

        if (options.PreflightCacheDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                "IbkrClientOptions.PreflightCacheDuration",
                options.PreflightCacheDuration,
                "PreflightCacheDuration must be greater than zero.");
        }

        if (options.ProactiveRefreshMargin <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                "IbkrClientOptions.ProactiveRefreshMargin",
                options.ProactiveRefreshMargin,
                "ProactiveRefreshMargin must be greater than zero.");
        }

        if (options.FlexPollTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                "IbkrClientOptions.FlexPollTimeout",
                options.FlexPollTimeout,
                "FlexPollTimeout must be greater than zero.");
        }

        if (options.ConfirmationTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                "IbkrClientOptions.ConfirmationTimeout",
                options.ConfirmationTimeout,
                "ConfirmationTimeout must be greater than zero.");
        }

        if (options.BaseUrl is not null && !Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException(
                $"BaseUrl must be a valid absolute URI, got: '{options.BaseUrl}'.",
                "IbkrClientOptions.BaseUrl");
        }

        if (options.WebSocketBaseUrl is not null && !Uri.TryCreate(options.WebSocketBaseUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException(
                $"WebSocketBaseUrl must be a valid absolute URI, got: '{options.WebSocketBaseUrl}'.",
                "IbkrClientOptions.WebSocketBaseUrl");
        }
    }
#pragma warning restore CA2208
}
