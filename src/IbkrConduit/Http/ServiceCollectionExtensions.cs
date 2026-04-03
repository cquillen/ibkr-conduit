using System;
using IbkrConduit.Client;
using IbkrConduit.Session;
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
    /// Consumer pipeline: Refit -> TokenRefreshHandler -> ErrorNormalizationHandler ->
    /// ResilienceHandler -> GlobalRateLimitingHandler -> EndpointRateLimitingHandler ->
    /// OAuthSigningHandler -> HttpClient.
    /// Internal session pipeline: Refit -> ResilienceHandler -> GlobalRateLimitingHandler ->
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

        RateLimitingAndResilienceRegistration.Register(services);
        SessionServiceRegistration.Register(services, credentials, clientOptions, baseUrl);
        ConsumerPipelineRegistration.Register(services, credentials, baseUrl);
        StreamingAndFlexRegistration.Register(services, credentials, clientOptions, baseUrl);

        // Unified facade
        services.AddSingleton<IIbkrClient, IbkrClient>();

        return services;
    }
}
