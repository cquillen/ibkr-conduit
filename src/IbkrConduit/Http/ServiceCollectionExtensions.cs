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
using Polly.Retry;
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
    /// Consumer pipeline: Refit -> TokenRefreshHandler -> ResilienceHandler ->
    /// GlobalRateLimitingHandler -> EndpointRateLimitingHandler -> OAuthSigningHandler -> HttpClient.
    /// Internal session pipeline: Refit -> ResilienceHandler -> GlobalRateLimitingHandler ->
    /// EndpointRateLimitingHandler -> OAuthSigningHandler -> HttpClient (no TokenRefreshHandler).
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

        // Rate limiting and resilience singletons (shared across pipelines)
        var globalRateLimiter = CreateGlobalRateLimiter();
        var endpointRateLimiters = CreateEndpointRateLimiters();
        var resiliencePipeline = CreateResiliencePipeline();

        services.AddSingleton<RateLimiter>(globalRateLimiter);
        services.AddSingleton<IReadOnlyDictionary<string, RateLimiter>>(endpointRateLimiters);
        services.AddSingleton(resiliencePipeline);

        // Internal session API client:
        //   ResilienceHandler -> GlobalRateLimitingHandler -> EndpointRateLimitingHandler -> OAuthSigningHandler
        services.AddRefitClient<IIbkrSessionApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(_ibkrBaseUrl))
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

        // Session manager
        services.AddSingleton<ISessionManager>(sp =>
            new SessionManager(
                sp.GetRequiredService<ISessionTokenProvider>(),
                sp.GetRequiredService<ITickleTimerFactory>(),
                sp.GetRequiredService<IIbkrSessionApi>(),
                clientOptions,
                sp.GetRequiredService<ILogger<SessionManager>>()));

        // Consumer Refit client:
        //   TokenRefreshHandler -> ResilienceHandler -> GlobalRateLimitingHandler ->
        //   EndpointRateLimitingHandler -> OAuthSigningHandler
        services.AddRefitClient<TApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(_ibkrBaseUrl))
            .AddHttpMessageHandler(sp =>
                new TokenRefreshHandler(
                    sp.GetRequiredService<ISessionManager>()))
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
                    credentials.AccessToken,
                    sp.GetRequiredService<ISessionManager>()));

        return services;
    }

    /// <summary>
    /// Creates the global token bucket rate limiter (10 req/s, queue 500).
    /// </summary>
    private static TokenBucketRateLimiter CreateGlobalRateLimiter() =>
        new(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = 10,
            AutoReplenishment = true,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 500,
        });

    /// <summary>
    /// Creates the per-endpoint rate limiters for known IBKR endpoints.
    /// </summary>
    private static Dictionary<string, RateLimiter> CreateEndpointRateLimiters()
    {
        var limiters = new Dictionary<string, RateLimiter>
        {
            ["/iserver/account/orders"] = CreateEndpointLimiter(1, TimeSpan.FromSeconds(5), 50),
            ["/iserver/account/trades"] = CreateEndpointLimiter(1, TimeSpan.FromSeconds(5), 50),
            ["/iserver/account/pnl/partitioned"] = CreateEndpointLimiter(1, TimeSpan.FromSeconds(5), 50),
            ["/iserver/marketdata/snapshot"] = CreateEndpointLimiter(10, TimeSpan.FromSeconds(1), 50),
            ["/iserver/scanner/params"] = CreateEndpointLimiter(1, TimeSpan.FromMinutes(15), 50),
            ["/iserver/scanner/run"] = CreateEndpointLimiter(1, TimeSpan.FromSeconds(1), 50),
            ["/portfolio/accounts"] = CreateEndpointLimiter(1, TimeSpan.FromSeconds(5), 50),
            ["/portfolio/subaccounts"] = CreateEndpointLimiter(1, TimeSpan.FromSeconds(5), 50),
        };

        return limiters;
    }

    private static TokenBucketRateLimiter CreateEndpointLimiter(
        int tokenLimit, TimeSpan replenishmentPeriod, int queueLimit) =>
        new(new TokenBucketRateLimiterOptions
        {
            TokenLimit = tokenLimit,
            ReplenishmentPeriod = replenishmentPeriod,
            TokensPerPeriod = tokenLimit,
            AutoReplenishment = true,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = queueLimit,
        });

    /// <summary>
    /// Creates the Polly resilience pipeline for retrying transient HTTP errors.
    /// Retries 5xx, 408, and 429 with exponential backoff (1s, 2s, 4s) and jitter.
    /// </summary>
    private static ResiliencePipeline<HttpResponseMessage> CreateResiliencePipeline() =>
        new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(1),
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(r => (int)r.StatusCode >= 500)
                    .HandleResult(r => r.StatusCode == HttpStatusCode.RequestTimeout)
                    .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests),
            })
            .Build();
}
