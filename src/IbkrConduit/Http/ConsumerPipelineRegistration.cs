using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.RateLimiting;
using IbkrConduit.Accounts;
using IbkrConduit.Alerts;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Contracts;
using IbkrConduit.Errors;
using IbkrConduit.EventContracts;
using IbkrConduit.Fyi;
using IbkrConduit.MarketData;
using IbkrConduit.Orders;
using IbkrConduit.Portfolio;
using IbkrConduit.Session;
using IbkrConduit.Watchlists;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Refit;

namespace IbkrConduit.Http;

/// <summary>
/// Registers all consumer-facing Refit clients and their corresponding operations implementations
/// through the full HTTP pipeline (TokenRefresh, ErrorNormalization, Resilience, RateLimiting, OAuth).
/// </summary>
internal static class ConsumerPipelineRegistration
{
    /// <summary>
    /// Registers the 8 consumer Refit clients and 8 operations singletons.
    /// </summary>
    public static void Register(
        IServiceCollection services,
        IbkrOAuthCredentials credentials,
        IbkrClientOptions clientOptions,
        RefitEndpointMap endpointMap,
        string baseUrl)
    {
        // Consumer Refit clients (all go through the full pipeline):
        //   TokenRefreshHandler -> ResponseSchemaValidationHandler ->
        //   GlobalRateLimitingHandler -> EndpointRateLimitingHandler -> OAuthSigningHandler
        RegisterConsumerRefitClient<IIbkrPortfolioApi>(services, credentials, clientOptions, endpointMap, baseUrl);
        RegisterConsumerRefitClient<IIbkrContractApi>(services, credentials, clientOptions, endpointMap, baseUrl);
        RegisterConsumerRefitClient<IIbkrOrderApi>(services, credentials, clientOptions, endpointMap, baseUrl);
        RegisterConsumerRefitClient<IIbkrMarketDataApi>(services, credentials, clientOptions, endpointMap, baseUrl);
        RegisterConsumerRefitClient<IIbkrAccountApi>(services, credentials, clientOptions, endpointMap, baseUrl);
        RegisterConsumerRefitClient<IIbkrAlertApi>(services, credentials, clientOptions, endpointMap, baseUrl);
        RegisterConsumerRefitClient<IIbkrWatchlistApi>(services, credentials, clientOptions, endpointMap, baseUrl);
        RegisterConsumerRefitClient<IIbkrFyiApi>(services, credentials, clientOptions, endpointMap, baseUrl);
        RegisterConsumerRefitClient<IIbkrEventContractApi>(services, credentials, clientOptions, endpointMap, baseUrl);

        // Shared infrastructure
        services.AddSingleton<ResultFactory>();

        // Operations implementations
        services.AddSingleton<IPortfolioOperations, PortfolioOperations>();
        services.AddSingleton<IContractOperations, ContractOperations>();
        services.AddSingleton<IOrderOperations, OrderOperations>();
        services.AddSingleton<IMarketDataOperations, MarketDataOperations>();
        services.AddSingleton<IAccountOperations, AccountOperations>();
        services.AddSingleton<IAlertOperations, AlertOperations>();
        services.AddSingleton<IWatchlistOperations, WatchlistOperations>();
        services.AddSingleton<IFyiOperations, FyiOperations>();
        services.AddSingleton<IEventContractOperations, EventContractOperations>();
    }

    /// <summary>
    /// Registers a consumer-facing Refit client through the full HTTP pipeline.
    /// </summary>
    private static void RegisterConsumerRefitClient<TApi>(
        IServiceCollection services,
        IbkrOAuthCredentials credentials,
        IbkrClientOptions clientOptions,
        RefitEndpointMap endpointMap,
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
                    sp.GetRequiredService<ISessionManager>(),
                    sp.GetRequiredService<ILogger<TokenRefreshHandler>>()))
            .AddHttpMessageHandler(sp =>
                new ResponseSchemaValidationHandler(
                    clientOptions,
                    endpointMap,
                    sp.GetRequiredService<ILogger<ResponseSchemaValidationHandler>>()))
            .AddHttpMessageHandler(sp =>
                new GlobalRateLimitingHandler(
                    sp.GetRequiredService<RateLimiter>(),
                    sp.GetRequiredService<ILogger<GlobalRateLimitingHandler>>()))
            .AddHttpMessageHandler(sp =>
                new EndpointRateLimitingHandler(
                    sp.GetRequiredService<IReadOnlyDictionary<string, RateLimiter>>(),
                    sp.GetRequiredService<ILogger<EndpointRateLimitingHandler>>()))
            .AddHttpMessageHandler(sp =>
                new OAuthSigningHandler(
                    sp.GetRequiredService<ISessionTokenProvider>(),
                    credentials.ConsumerKey,
                    credentials.AccessToken,
                    sp.GetRequiredService<ISessionManager>()));
    }
}
