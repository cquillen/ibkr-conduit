using System;
using System.Net.Http;
using IbkrConduit.Auth;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace IbkrConduit.Http;

/// <summary>
/// Extension methods for registering IBKR API clients with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    private const string _ibkrBaseUrl = "https://api.ibkr.com/v1/api/";

    /// <summary>
    /// Registers the IBKR API client pipeline for the given tenant.
    /// Pipeline: Refit → OAuthSigningHandler → HttpClient → IBKR API.
    /// </summary>
    public static IServiceCollection AddIbkrClient<TApi>(
        this IServiceCollection services,
        IbkrOAuthCredentials credentials) where TApi : class
    {
        services.AddSingleton<ILiveSessionTokenClient>(sp =>
        {
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(_ibkrBaseUrl),
            };
            return new LiveSessionTokenClient(httpClient);
        });

        services.AddSingleton<ISessionTokenProvider>(sp =>
            new SessionTokenProvider(
                credentials,
                sp.GetRequiredService<ILiveSessionTokenClient>()));

        services.AddTransient<OAuthSigningHandler>(sp =>
            new OAuthSigningHandler(
                sp.GetRequiredService<ISessionTokenProvider>(),
                credentials.ConsumerKey,
                credentials.AccessToken));

        services.AddRefitClient<TApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(_ibkrBaseUrl))
            .AddHttpMessageHandler<OAuthSigningHandler>();

        return services;
    }
}
