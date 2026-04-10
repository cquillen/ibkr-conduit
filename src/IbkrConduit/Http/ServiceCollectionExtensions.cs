using System;
using IbkrConduit.Accounts;
using IbkrConduit.Alerts;
using IbkrConduit.Client;
using IbkrConduit.Contracts;
using IbkrConduit.EventContracts;
using IbkrConduit.Fyi;
using IbkrConduit.Health;
using IbkrConduit.MarketData;
using IbkrConduit.Orders;
using IbkrConduit.Portfolio;
using IbkrConduit.Session;
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

        ArgumentNullException.ThrowIfNull(clientOptions.Credentials, "IbkrClientOptions.Credentials");

        var credentials = clientOptions.Credentials;
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
        services.AddSingleton<LastSuccessfulCallTracker>();
        services.AddSingleton(new HealthStatusOptions());
        services.AddSingleton<IHealthStatusCollector, HealthStatusCollector>();

        // Unified facade
        services.AddSingleton<IIbkrClient, IbkrClient>();

        return services;
    }
}
