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
using IbkrConduit.Fyi;
using IbkrConduit.MarketData;
using IbkrConduit.Orders;
using IbkrConduit.Portfolio;
using IbkrConduit.Session;
using IbkrConduit.Watchlists;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Refit;

namespace IbkrConduit.Http;

/// <summary>
/// Registers all consumer-facing Refit clients and their corresponding operations implementations
/// through the full HTTP pipeline (TokenRefresh, ErrorNormalization, Resilience, RateLimiting, OAuth).
/// </summary>
internal static class ConsumerPipelineRegistration
{
    /// <summary>
    /// Registers the 9 consumer Refit clients and 9 operations singletons.
    /// </summary>
    public static void Register(
        IServiceCollection services,
        IbkrOAuthCredentials credentials,
        string baseUrl)
    {
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
}
