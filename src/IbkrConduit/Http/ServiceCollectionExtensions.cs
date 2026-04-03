using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.RateLimiting;
using IbkrConduit.Accounts;
using IbkrConduit.Alerts;
using IbkrConduit.Allocation;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Contracts;
using IbkrConduit.Flex;
using IbkrConduit.Fyi;
using IbkrConduit.MarketData;
using IbkrConduit.Orders;
using IbkrConduit.Portfolio;
using IbkrConduit.Session;
using IbkrConduit.Streaming;
using IbkrConduit.Watchlists;
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
        IbkrOAuthCredentials credentials,
        IbkrClientOptions? options = null)
    {
        var clientOptions = options ?? new IbkrClientOptions();
        var baseUrl = clientOptions.BaseUrl ?? _ibkrBaseUrl;

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

        // Consumer Refit clients (all go through the full pipeline):
        //   TokenRefreshHandler -> ErrorNormalizationHandler -> ResilienceHandler ->
        //   GlobalRateLimitingHandler -> EndpointRateLimitingHandler -> OAuthSigningHandler
        RegisterConsumerRefitClient<IIbkrPortfolioApi>(services, credentials, baseUrl);
        RegisterConsumerRefitClient<IIbkrContractApi>(services, credentials, baseUrl);
        RegisterConsumerRefitClient<IIbkrOrderApi>(services, credentials, baseUrl);
        RegisterConsumerRefitClient<IIbkrMarketDataApi>(services, credentials, baseUrl);
        RegisterConsumerRefitClient<IIbkrAccountApi>(services, credentials, baseUrl);
        RegisterConsumerRefitClient<IIbkrAlertApi>(services, credentials, baseUrl);
        RegisterConsumerRefitClient<IIbkrWatchlistApi>(services, credentials, baseUrl);
        RegisterConsumerRefitClient<IIbkrFyiApi>(services, credentials, baseUrl);
        RegisterConsumerRefitClient<IIbkrAllocationApi>(services, credentials, baseUrl);

        // Operations implementations
        services.AddSingleton<IPortfolioOperations, PortfolioOperations>();
        services.AddSingleton<IContractOperations, ContractOperations>();
        services.AddSingleton<IOrderOperations, OrderOperations>();
        services.AddSingleton<IMarketDataOperations, MarketDataOperations>();
        services.AddSingleton<IAccountOperations, AccountOperations>();
        services.AddSingleton<IAlertOperations, AlertOperations>();
        services.AddSingleton<IWatchlistOperations, WatchlistOperations>();
        services.AddSingleton<IFyiOperations, FyiOperations>();
        services.AddSingleton<IAllocationOperations, AllocationOperations>();

        // WebSocket streaming
        services.AddSingleton<IIbkrWebSocketClient>(sp =>
            new IbkrWebSocketClient(
                sp.GetRequiredService<IIbkrSessionApi>(),
                credentials,
                sp.GetRequiredService<ISessionLifecycleNotifier>(),
                sp.GetRequiredService<ILogger<IbkrWebSocketClient>>(),
                () => new ClientWebSocketAdapter()));
        services.AddSingleton<IStreamingOperations>(sp =>
            new StreamingOperations(
                sp.GetRequiredService<IIbkrWebSocketClient>()));

        // Flex Web Service (plain HTTP via IHttpClientFactory, no signing pipeline)
        if (!string.IsNullOrEmpty(clientOptions.FlexToken))
        {
            var flexToken = clientOptions.FlexToken;
            var flexClientName = $"IbkrConduit-Flex-{credentials.TenantId}";
            services.AddHttpClient(flexClientName, c =>
            {
                c.DefaultRequestHeaders.UserAgent.ParseAdd("IbkrConduit/1.0");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            });

            services.AddSingleton(sp =>
                new FlexClient(
                    sp.GetRequiredService<IHttpClientFactory>(),
                    flexClientName,
                    flexToken,
                    sp.GetRequiredService<ILogger<FlexClient>>()));
            services.AddSingleton<IFlexOperations>(sp =>
                new FlexOperations(sp.GetRequiredService<FlexClient>()));
        }
        else
        {
            services.AddSingleton<IFlexOperations>(_ => new FlexOperations(null));
        }

        // Unified facade
        services.AddSingleton<IIbkrClient, IbkrClient>();

        return services;
    }

    /// <summary>
    /// Registers a consumer-facing Refit client through the full HTTP pipeline.
    /// </summary>
    private static void RegisterConsumerRefitClient<TApi>(
        IServiceCollection services,
        IbkrOAuthCredentials credentials,
        string baseUrl) where TApi : class
    {
        services.AddRefitClient<TApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            })
            .AddHttpMessageHandler(sp =>
                new TokenRefreshHandler(
                    sp.GetRequiredService<ISessionManager>()))
            .AddHttpMessageHandler(_ =>
                new ErrorNormalizationHandler())
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
