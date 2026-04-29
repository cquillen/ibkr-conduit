using System;
using System.Threading.RateLimiting;
using IbkrConduit.Accounts;
using IbkrConduit.Alerts;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Contracts;
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
        var clientOptions = new IbkrClientOptions();
        configure(clientOptions);

        ValidateOptions(clientOptions);

        var credentials = clientOptions.Credentials!;
        var baseUrl = clientOptions.BaseUrl ?? _ibkrBaseUrl;

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

        // Health check infrastructure
        services.AddSingleton(_ => new LastSuccessfulCallTracker(TimeProvider.System));
        services.AddSingleton(new HealthStatusOptions());
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

        // Unified facade
        services.AddSingleton<IIbkrClient, IbkrClient>();

        return services;
    }

    /// <summary>
    /// Validates all <see cref="IbkrClientOptions"/> fields at registration time
    /// to fail fast on misconfiguration.
    /// </summary>
    // CA2208: paramName values intentionally use "IbkrClientOptions.Property" paths
    // so consumers see which option is invalid, not the method parameter name "options".
#pragma warning disable CA2208
    private static void ValidateOptions(IbkrClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options.Credentials, "IbkrClientOptions.Credentials");

        if (options.TickleIntervalSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                "IbkrClientOptions.TickleIntervalSeconds",
                options.TickleIntervalSeconds,
                "TickleIntervalSeconds must be greater than zero.");
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

        if (options.BaseUrl is not null && !Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException(
                $"BaseUrl must be a valid absolute URI, got: '{options.BaseUrl}'.",
                "IbkrClientOptions.BaseUrl");
        }
    }
#pragma warning restore CA2208
}
