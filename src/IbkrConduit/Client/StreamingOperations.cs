using System.Text.Json;
using IbkrConduit.Streaming;
using IbkrConduit.Streaming.Mappers;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Client;

/// <summary>
/// Implementation of <see cref="IStreamingOperations"/> that builds topic subscribe messages,
/// delegates JSON-to-DTO transformations to the per-topic mappers in
/// <c>IbkrConduit.Streaming.Mappers</c>, and returns <see cref="IIbkrSubscription{T}"/> handles that
/// wrap a <see cref="ChannelObservable{T}"/> stream and the topic's unsubscribe delegate.
/// </summary>
internal sealed class StreamingOperations : IStreamingOperations
{
    private readonly IIbkrWebSocketClient _webSocketClient;
    private readonly ILoggerFactory _loggerFactory;
    private readonly StreamingMetrics _metrics;

    /// <summary>
    /// Creates a new <see cref="StreamingOperations"/>.
    /// </summary>
    /// <param name="webSocketClient">The underlying WebSocket client.</param>
    /// <param name="loggerFactory">Factory used to create a per-topic logger for each subscription's observable, so a dropped-frame warning can be traced back to its topic.</param>
    /// <param name="metrics">Reporter that counts every dropped frame (mapper/observer failures in the observables) so no streaming loss is silent.</param>
    public StreamingOperations(IIbkrWebSocketClient webSocketClient, ILoggerFactory loggerFactory, StreamingMetrics metrics)
    {
        _webSocketClient = webSocketClient;
        _loggerFactory = loggerFactory;
        _metrics = metrics;
    }

    /// <inheritdoc />
    public IIbkrSubscription<ConnectionEvent> SubscribeConnectionEvents()
    {
        var (reader, unsubscribe) = _webSocketClient.RegisterConnectionEvents();
        return new IbkrSubscription<ConnectionEvent>(new ConnectionEventObservable(reader), unsubscribe);
    }

    /// <inheritdoc />
    public IIbkrSubscription<SessionStatusEvent> SubscribeSessionStatus() =>
        CreateUnsolicitedSubscription("sts", SessionStatusMapper.Map);

    /// <inheritdoc />
    public IIbkrSubscription<BulletinEvent> SubscribeBulletins() =>
        CreateUnsolicitedSubscription("blt", BulletinMapper.Map);

    /// <inheritdoc />
    public IIbkrSubscription<NotificationEvent> SubscribeTradingNotifications() =>
        CreateUnsolicitedSubscription("ntf", NotificationMapper.Map);

    /// <inheritdoc />
    public IIbkrSubscription<SystemEvent> SubscribeSystemEvents() =>
        CreateUnsolicitedSubscription("system", SystemEventMapper.Map);

    /// <inheritdoc />
    public IIbkrSubscription<AccountStatusEvent> SubscribeAccountStatus() =>
        CreateUnsolicitedSubscription("act", AccountStatusMapper.Map);

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken cancellationToken = default) =>
        _webSocketClient.ConnectAsync(cancellationToken);

    /// <inheritdoc />
    public bool IsConnected => _webSocketClient.IsConnected;

    /// <inheritdoc />
    public DateTimeOffset? LastMessageReceivedAt => _webSocketClient.LastMessageReceivedAt;

    /// <inheritdoc />
    public async Task<IIbkrSubscription<MarketDataTick>> MarketDataAsync(int conid, string[] fields, CancellationToken cancellationToken = default)
    {
        var fieldsJson = string.Join(",", fields.Select(f => $"\"{f}\""));
        var subscribeMessage = $"smd+{conid}+{{\"fields\":[{fieldsJson}]}}";
        var cancelMessage = $"umd+{conid}+{{}}";

        var (reader, unsubscribe) = await _webSocketClient.SubscribeTopicAsync(subscribeMessage, "smd", cancelMessage, cancellationToken);

        return new IbkrSubscription<MarketDataTick>(new ChannelObservable<MarketDataTick>(reader, MarketDataTickMapper.Map, CreateTopicLogger("smd"), _metrics, "smd"), unsubscribe);
    }

    /// <inheritdoc />
    public async Task<IIbkrSubscription<OrderUpdate>> OrderUpdatesAsync(int? days = null, CancellationToken cancellationToken = default)
    {
        var subscribeMessage = days.HasValue
            ? $"sor+{{\"days\":{days.Value}}}"
            : "sor+{}";
        var cancelMessage = "uor+{}";

        var (reader, unsubscribe) = await _webSocketClient.SubscribeTopicAsync(subscribeMessage, "sor", cancelMessage, cancellationToken);

        return new IbkrSubscription<OrderUpdate>(new FanOutChannelObservable<OrderUpdate>(reader, OrderUpdateMapper.MapMany, CreateTopicLogger("sor"), _metrics, "sor"), unsubscribe);
    }

    /// <inheritdoc />
    public async Task<IIbkrSubscription<TradeExecution>> TradeExecutionsAsync(
        bool? realtimeUpdatesOnly = null,
        int? days = null,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>();
        if (realtimeUpdatesOnly.HasValue)
        {
            parts.Add($"\"realtimeUpdatesOnly\":{(realtimeUpdatesOnly.Value ? "true" : "false")}");
        }
        if (days.HasValue)
        {
            parts.Add($"\"days\":{days.Value}");
        }
        var subscribeMessage = $"str+{{{string.Join(",", parts)}}}";

        var (reader, unsubscribe) = await _webSocketClient.SubscribeTopicAsync(subscribeMessage, "str", "utr", cancellationToken);

        return new IbkrSubscription<TradeExecution>(new FanOutChannelObservable<TradeExecution>(reader, TradeExecutionMapper.MapMany, CreateTopicLogger("str"), _metrics, "str"), unsubscribe);
    }

    /// <inheritdoc />
    public async Task<IIbkrSubscription<PnlUpdate>> ProfitAndLossAsync(CancellationToken cancellationToken = default)
    {
        var (reader, unsubscribe) = await _webSocketClient.SubscribeTopicAsync("spl+{}", "spl", "upl+{}", cancellationToken);

        return new IbkrSubscription<PnlUpdate>(new FanOutChannelObservable<PnlUpdate>(reader, PnlUpdateMapper.MapMany, CreateTopicLogger("spl"), _metrics, "spl"), unsubscribe);
    }

    /// <inheritdoc />
    public async Task<IIbkrSubscription<AccountSummaryUpdate>> AccountSummaryAsync(
        string accountId,
        string[]? keys = null,
        string[]? fields = null,
        CancellationToken cancellationToken = default)
    {
        var subscribeMessage = $"ssd+{accountId}+{BuildKeysFieldsArgs(keys, fields)}";
        var cancelMessage = $"usd+{accountId}+{{}}";

        var (reader, unsubscribe) = await _webSocketClient.SubscribeTopicAsync(subscribeMessage, "ssd", cancelMessage, cancellationToken);

        return new IbkrSubscription<AccountSummaryUpdate>(new ChannelObservable<AccountSummaryUpdate>(reader, AccountSummaryUpdateMapper.Map, CreateTopicLogger("ssd"), _metrics, "ssd"), unsubscribe);
    }

    /// <inheritdoc />
    public async Task<IIbkrSubscription<AccountLedgerUpdate>> AccountLedgerAsync(
        string accountId,
        string[]? keys = null,
        string[]? fields = null,
        CancellationToken cancellationToken = default)
    {
        var subscribeMessage = $"sld+{accountId}+{BuildKeysFieldsArgs(keys, fields)}";
        var cancelMessage = $"uld+{accountId}+{{}}";

        var (reader, unsubscribe) = await _webSocketClient.SubscribeTopicAsync(subscribeMessage, "sld", cancelMessage, cancellationToken);

        return new IbkrSubscription<AccountLedgerUpdate>(new ChannelObservable<AccountLedgerUpdate>(reader, AccountLedgerUpdateMapper.Map, CreateTopicLogger("sld"), _metrics, "sld"), unsubscribe);
    }

    private static string BuildKeysFieldsArgs(string[]? keys, string[]? fields)
    {
        var parts = new List<string>();
        if (keys is { Length: > 0 })
        {
            parts.Add($"\"keys\":[{string.Join(",", keys.Select(k => $"\"{k}\""))}]");
        }
        if (fields is { Length: > 0 })
        {
            parts.Add($"\"fields\":[{string.Join(",", fields.Select(f => $"\"{f}\""))}]");
        }
        return $"{{{string.Join(",", parts)}}}";
    }

    private IbkrSubscription<T> CreateUnsolicitedSubscription<T>(string topicPrefix, Func<JsonElement, T> mapper)
    {
        var (reader, unsubscribe) = _webSocketClient.RegisterUnsolicitedTopic(topicPrefix);
        return new IbkrSubscription<T>(new ChannelObservable<T>(reader, mapper, CreateTopicLogger(topicPrefix), _metrics, topicPrefix), unsubscribe);
    }

    /// <summary>Creates a logger scoped to a topic, used to trace dropped-frame warnings back to their subscription.</summary>
    private ILogger CreateTopicLogger(string topicPrefix) =>
        _loggerFactory.CreateLogger($"IbkrConduit.Streaming.{topicPrefix}");
}
