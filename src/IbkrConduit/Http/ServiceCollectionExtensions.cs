using System;
using System.Net.Http;
using IbkrConduit.Auth;
using IbkrConduit.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Refit;

namespace IbkrConduit.Http;

/// <summary>
/// Extension methods for registering IBKR API clients with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    private const string _ibkrBaseUrl = "https://api.ibkr.com";

    /// <summary>
    /// Registers the IBKR API client pipeline for the given tenant.
    /// Consumer pipeline: Refit -> TokenRefreshHandler -> OAuthSigningHandler -> HttpClient.
    /// Internal session pipeline: Refit -> OAuthSigningHandler -> HttpClient (no TokenRefreshHandler).
    /// </summary>
    public static IServiceCollection AddIbkrClient<TApi>(
        this IServiceCollection services,
        IbkrOAuthCredentials credentials,
        IbkrClientOptions? options = null) where TApi : class
    {
        var clientOptions = options ?? new IbkrClientOptions();

        // LST client (plain HttpClient, not through Refit pipeline)
        services.AddSingleton<ILiveSessionTokenClient>(sp =>
        {
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(_ibkrBaseUrl + "/v1/api/"),
            };
            return new LiveSessionTokenClient(httpClient);
        });

        // Session token provider
        services.AddSingleton<ISessionTokenProvider>(sp =>
            new SessionTokenProvider(
                credentials,
                sp.GetRequiredService<ILiveSessionTokenClient>()));

        // Client options
        services.AddSingleton(clientOptions);

        // Tickle timer factory
        services.AddSingleton<ITickleTimerFactory>(sp =>
            new TickleTimerFactory(
                sp.GetRequiredService<ILogger<TickleTimer>>()));

        // Internal session API client (signing only, no TokenRefreshHandler)
        services.AddRefitClient<IIbkrSessionApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(_ibkrBaseUrl))
            .AddHttpMessageHandler(sp =>
                new OAuthSigningHandler(
                    sp.GetRequiredService<ISessionTokenProvider>(),
                    credentials.ConsumerKey,
                    credentials.AccessToken));

        // Session manager
        services.AddSingleton<ISessionManager>(sp =>
            new SessionManager(
                sp.GetRequiredService<ISessionTokenProvider>(),
                sp.GetRequiredService<ITickleTimerFactory>(),
                sp.GetRequiredService<IIbkrSessionApi>(),
                clientOptions,
                sp.GetRequiredService<ILogger<SessionManager>>()));

        // Consumer Refit client: TokenRefreshHandler -> OAuthSigningHandler -> HttpClient
        services.AddRefitClient<TApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(_ibkrBaseUrl))
            .AddHttpMessageHandler(sp =>
                new TokenRefreshHandler(
                    sp.GetRequiredService<ISessionManager>()))
            .AddHttpMessageHandler(sp =>
                new OAuthSigningHandler(
                    sp.GetRequiredService<ISessionTokenProvider>(),
                    credentials.ConsumerKey,
                    credentials.AccessToken,
                    sp.GetRequiredService<ISessionManager>()));

        return services;
    }
}
