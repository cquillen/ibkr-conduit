using System;
using System.Net;
using System.Net.Http;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Flex;
using IbkrConduit.Session;
using IbkrConduit.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Http;

/// <summary>
/// Registers WebSocket streaming services and Flex Web Service clients.
/// </summary>
internal static class StreamingAndFlexRegistration
{
    /// <summary>
    /// Registers the WebSocket client, streaming operations, Flex HTTP client, and Flex operations.
    /// </summary>
    public static void Register(
        IServiceCollection services,
        IbkrOAuthCredentials credentials,
        IbkrClientOptions clientOptions,
        string baseUrl)
    {
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
                    sp.GetRequiredService<ILogger<FlexClient>>(),
                    baseUrl: null,
                    pollTimeout: clientOptions.FlexPollTimeout));
            services.AddSingleton<IFlexOperations>(sp =>
                new FlexOperations(sp.GetRequiredService<FlexClient>()));
        }
        else
        {
            services.AddSingleton<IFlexOperations>(_ => new FlexOperations(null));
        }
    }
}
