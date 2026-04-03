using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.RateLimiting;
using IbkrConduit.Auth;
using IbkrConduit.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Refit;

namespace IbkrConduit.Http;

/// <summary>
/// Registers session-related services: LST client, session token provider,
/// session API pipeline, lifecycle notifier, session manager, and tickle timer.
/// </summary>
internal static class SessionServiceRegistration
{
    /// <summary>
    /// Registers all session infrastructure services into the container.
    /// </summary>
    public static void Register(
        IServiceCollection services,
        IbkrOAuthCredentials credentials,
        IbkrClientOptions clientOptions,
        string baseUrl)
    {
        // LST client (plain HttpClient via IHttpClientFactory, not through Refit pipeline)
        var lstClientName = $"IbkrConduit-LST-{credentials.TenantId}";
        services.AddHttpClient(lstClientName, c =>
        {
            c.BaseAddress = new Uri(baseUrl + "/v1/api/");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        });

        services.AddSingleton<ILiveSessionTokenClient>(sp =>
            new LiveSessionTokenClient(
                sp.GetRequiredService<IHttpClientFactory>(),
                lstClientName,
                sp.GetRequiredService<ILogger<LiveSessionTokenClient>>()));

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
                sp.GetRequiredService<ILogger<TickleTimer>>(),
                clientOptions.TickleIntervalSeconds));

        // Internal session API client:
        //   ResilienceHandler -> GlobalRateLimitingHandler -> EndpointRateLimitingHandler -> OAuthSigningHandler
        services.AddRefitClient<IIbkrSessionApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            })
            .AddHttpMessageHandler(sp =>
                new ResilienceHandler(
                    sp.GetRequiredService<ResiliencePipeline<HttpResponseMessage>>()))
            .AddHttpMessageHandler(sp =>
                new GlobalRateLimitingHandler(
                    sp.GetRequiredService<RateLimiter>()))
            .AddHttpMessageHandler(sp =>
                new EndpointRateLimitingHandler(
                    sp.GetRequiredService<IReadOnlyDictionary<string, RateLimiter>>()))
            .AddHttpMessageHandler(sp =>
                new OAuthSigningHandler(
                    sp.GetRequiredService<ISessionTokenProvider>(),
                    credentials.ConsumerKey,
                    credentials.AccessToken));

        // Session lifecycle notifier
        services.AddSingleton<ISessionLifecycleNotifier, SessionLifecycleNotifier>();

        // Session manager
        services.AddSingleton<ISessionManager>(sp =>
            new SessionManager(
                sp.GetRequiredService<ISessionTokenProvider>(),
                sp.GetRequiredService<ITickleTimerFactory>(),
                sp.GetRequiredService<IIbkrSessionApi>(),
                clientOptions,
                sp.GetRequiredService<ISessionLifecycleNotifier>(),
                sp.GetRequiredService<ILogger<SessionManager>>()));
    }
}
