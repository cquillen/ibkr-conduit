using System.Text.Json;
using System.Threading;
using IbkrConduit.Streaming;

namespace IbkrConduit.Client;

/// <summary>
/// Implementation of <see cref="IStreamingOperations"/> that builds topic subscribe messages,
/// maps JSON to typed models, and returns <see cref="IObservable{T}"/> streams via
/// <see cref="ChannelObservable{T}"/>.
/// </summary>
internal sealed class StreamingOperations : IStreamingOperations
{
    private readonly IIbkrWebSocketClient _webSocketClient;
    private readonly Lazy<IObservable<SessionStatusEvent>> _sessionStatus;

    /// <summary>
    /// Creates a new <see cref="StreamingOperations"/>.
    /// </summary>
    /// <param name="webSocketClient">The underlying WebSocket client.</param>
    public StreamingOperations(IIbkrWebSocketClient webSocketClient)
    {
        _webSocketClient = webSocketClient;
        _sessionStatus = new Lazy<IObservable<SessionStatusEvent>>(
            () => CreateUnsolicitedObservable("sts", MapSessionStatus),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public IObservable<SessionStatusEvent> SessionStatus => _sessionStatus.Value;

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken cancellationToken = default) =>
        _webSocketClient.ConnectAsync(cancellationToken);

    /// <inheritdoc />
    public bool IsConnected => _webSocketClient.IsConnected;

    /// <inheritdoc />
    public DateTimeOffset? LastMessageReceivedAt => _webSocketClient.LastMessageReceivedAt;

    /// <inheritdoc />
    public async Task<IObservable<MarketDataTick>> MarketDataAsync(int conid, string[] fields, CancellationToken cancellationToken = default)
    {
        var fieldsJson = string.Join(",", fields.Select(f => $"\"{f}\""));
        var subscribeMessage = $"smd+{conid}+{{\"fields\":[{fieldsJson}]}}";

        var (reader, _) = await _webSocketClient.SubscribeTopicAsync(subscribeMessage, "smd", cancellationToken);

        return new ChannelObservable<MarketDataTick>(reader, MapMarketDataTick);
    }

    /// <inheritdoc />
    public async Task<IObservable<OrderUpdate>> OrderUpdatesAsync(int? days = null, CancellationToken cancellationToken = default)
    {
        var subscribeMessage = days.HasValue
            ? $"sor+{{\"days\":{days.Value}}}"
            : "sor+{}";

        var (reader, _) = await _webSocketClient.SubscribeTopicAsync(subscribeMessage, "sor", cancellationToken);

        return new ChannelObservable<OrderUpdate>(reader, MapOrderUpdate);
    }

    /// <inheritdoc />
    public async Task<IObservable<PnlUpdate>> ProfitAndLossAsync(CancellationToken cancellationToken = default)
    {
        var (reader, _) = await _webSocketClient.SubscribeTopicAsync("spl+{}", "spl", cancellationToken);

        return new ChannelObservable<PnlUpdate>(reader, MapPnlUpdate);
    }

    /// <inheritdoc />
    public async Task<IObservable<AccountSummaryUpdate>> AccountSummaryAsync(CancellationToken cancellationToken = default)
    {
        var (reader, _) = await _webSocketClient.SubscribeTopicAsync("ssd+{}", "ssd", cancellationToken);

        return new ChannelObservable<AccountSummaryUpdate>(reader, MapAccountSummaryUpdate);
    }

    /// <inheritdoc />
    public async Task<IObservable<AccountLedgerUpdate>> AccountLedgerAsync(CancellationToken cancellationToken = default)
    {
        var (reader, _) = await _webSocketClient.SubscribeTopicAsync("sld+{}", "sld", cancellationToken);

        return new ChannelObservable<AccountLedgerUpdate>(reader, MapAccountLedgerUpdate);
    }

    private ChannelObservable<T> CreateUnsolicitedObservable<T>(string topicPrefix, Func<JsonElement, T> mapper)
    {
        var (reader, _) = _webSocketClient.RegisterUnsolicitedTopic(topicPrefix);
        return new ChannelObservable<T>(reader, mapper);
    }

    private static SessionStatusEvent MapSessionStatus(JsonElement element)
    {
        if (element.TryGetProperty("args", out var args)
            && args.TryGetProperty("authenticated", out var authProp))
        {
            return new SessionStatusEvent { Authenticated = authProp.GetBoolean() };
        }
        return new SessionStatusEvent();
    }

    private static MarketDataTick MapMarketDataTick(JsonElement element)
    {
        var conid = 0;
        long? updated = null;
        var fields = new Dictionary<string, string>();

        // Extract conid from the topic string: "smd+265598" -> 265598
        if (element.TryGetProperty("topic", out var topicProp))
        {
            var topic = topicProp.GetString();
            if (topic != null)
            {
                var plusIndex = topic.IndexOf('+');
                if (plusIndex >= 0 && int.TryParse(topic[(plusIndex + 1)..], out var parsedConid))
                {
                    conid = parsedConid;
                }
            }
        }

        // Also try conid property directly
        if (conid == 0 && element.TryGetProperty("conid", out var conidProp))
        {
            conid = conidProp.GetInt32();
        }

        if (element.TryGetProperty("_updated", out var updatedProp))
        {
            updated = updatedProp.GetInt64();
        }

        // Extract numeric field keys into the Fields dictionary
        foreach (var prop in element.EnumerateObject())
        {
            if (prop.Name == "topic" || prop.Name == "conid" || prop.Name == "_updated")
            {
                continue;
            }

            // Numeric keys are market data field IDs
            if (int.TryParse(prop.Name, out _))
            {
                fields[prop.Name] = prop.Value.ToString();
            }
        }

        return new MarketDataTick
        {
            Conid = conid,
            Updated = updated,
            Fields = fields.Count > 0 ? fields : null,
        };
    }

    private static OrderUpdate MapOrderUpdate(JsonElement element) =>
        JsonSerializer.Deserialize<OrderUpdate>(element.GetRawText()) ?? new OrderUpdate();

    private static PnlUpdate MapPnlUpdate(JsonElement element) =>
        JsonSerializer.Deserialize<PnlUpdate>(element.GetRawText()) ?? new PnlUpdate();

    private static AccountSummaryUpdate MapAccountSummaryUpdate(JsonElement element) =>
        JsonSerializer.Deserialize<AccountSummaryUpdate>(element.GetRawText()) ?? new AccountSummaryUpdate();

    private static AccountLedgerUpdate MapAccountLedgerUpdate(JsonElement element) =>
        JsonSerializer.Deserialize<AccountLedgerUpdate>(element.GetRawText()) ?? new AccountLedgerUpdate();
}
