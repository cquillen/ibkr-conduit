using System.Text.Json;
using IbkrConduit.Health;
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
    private readonly SessionHealthState _sessionHealthState;
    private readonly StreamingMetrics _metrics;

    /// <summary>
    /// Creates a new <see cref="StreamingOperations"/>.
    /// </summary>
    /// <param name="webSocketClient">The underlying WebSocket client.</param>
    /// <param name="loggerFactory">Factory used to create a per-topic logger for each subscription's observable, so a dropped-frame warning can be traced back to its topic.</param>
    /// <param name="sessionHealthState">Shared session-health state that a competing <c>sts</c> frame feeds (ADR-0004).</param>
    /// <param name="metrics">Reporter that counts every dropped frame (mapper/observer failures in the observables) so no streaming loss is silent.</param>
    public StreamingOperations(
        IIbkrWebSocketClient webSocketClient,
        ILoggerFactory loggerFactory,
        SessionHealthState sessionHealthState,
        StreamingMetrics metrics)
    {
        _webSocketClient = webSocketClient;
        _loggerFactory = loggerFactory;
        _sessionHealthState = sessionHealthState;
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
        CreateUnsolicitedSubscription("sts", MapSessionStatusAndFeedHealth);

    /// <summary>
    /// Maps an <c>sts</c> frame to a <see cref="SessionStatusEvent"/> and, when the frame reports a
    /// competing session, feeds that verdict into the passive session-health snapshot so a competing
    /// takeover is observable through health as well as the push event (ADR-0004 / GAP3-3).
    /// </summary>
    private SessionStatusEvent MapSessionStatusAndFeedHealth(JsonElement element)
    {
        var evt = SessionStatusMapper.Map(element);
        if (evt.Competing == true)
        {
            _sessionHealthState.MarkCompeting(evt.FailReason);
        }
        return evt;
    }

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
        ValidateConid(conid);
        var subscribeMessage = $"smd+{conid}+{BuildFieldsArgs(fields)}";
        var cancelMessage = $"umd+{conid}+{{}}";

        // ADR-0005: register under the full wire-topic identity so this subscription receives only
        // conid's own ticks, never another conid's (PRB-1.1/1.2).
        var routingKey = $"smd+{conid}";
        var (reader, unsubscribe) = await _webSocketClient.SubscribeTopicAsync(subscribeMessage, routingKey, cancelMessage, cancellationToken);

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

        // Per-element mapper isolation (WIR-1): a single malformed order is skipped and counted+logged
        // through the drop taxonomy, so the frame's remaining orders are still delivered. A
        // status-bearing sor frame that omits a required money field also raises the WIR-5 census
        // signal through the same reporter (separate counter — the order is delivered, not dropped).
        var sorLogger = CreateTopicLogger("sor");
        var mapOrders = (JsonElement frame) =>
            OrderUpdateMapper.MapMany(
                frame,
                ex => RecordMapperDrop(sorLogger, "sor", ex),
                field => RecordMissingMoneyField(sorLogger, "sor", field));

        return new IbkrSubscription<OrderUpdate>(new FanOutChannelObservable<OrderUpdate>(reader, mapOrders, sorLogger, _metrics, "sor"), unsubscribe);
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

        // Per-element mapper isolation (FIL-2): a single malformed execution is skipped and
        // counted+logged through the same drop taxonomy the observable uses for whole-frame drops,
        // so the frame's remaining fills are still delivered rather than discarded as one bad frame.
        var strLogger = CreateTopicLogger("str");
        var mapExecutions = (JsonElement frame) =>
            TradeExecutionMapper.MapMany(
                frame,
                ex => RecordMapperDrop(strLogger, "str", ex),
                field => RecordMissingMoneyField(strLogger, "str", field));

        return new IbkrSubscription<TradeExecution>(new FanOutChannelObservable<TradeExecution>(reader, mapExecutions, strLogger, _metrics, "str"), unsubscribe);
    }

    /// <inheritdoc />
    public async Task<IIbkrSubscription<PnlUpdate>> ProfitAndLossAsync(CancellationToken cancellationToken = default)
    {
        var (reader, unsubscribe) = await _webSocketClient.SubscribeTopicAsync("spl+{}", "spl", "upl+{}", cancellationToken);

        // Per-element mapper isolation (PRB-3.2): a single malformed per-account entry is skipped and
        // counted+logged through the drop taxonomy, so the frame's other accounts are still delivered.
        var splLogger = CreateTopicLogger("spl");
        var mapPnl = (JsonElement frame) =>
            PnlUpdateMapper.MapMany(frame, ex => RecordMapperDrop(splLogger, "spl", ex));

        return new IbkrSubscription<PnlUpdate>(new FanOutChannelObservable<PnlUpdate>(reader, mapPnl, splLogger, _metrics, "spl"), unsubscribe);
    }

    /// <inheritdoc />
    public async Task<IIbkrSubscription<AccountSummaryUpdate>> AccountSummaryAsync(
        string accountId,
        string[]? keys = null,
        string[]? fields = null,
        CancellationToken cancellationToken = default)
    {
        ValidateTargetSegment(accountId, nameof(accountId));
        var subscribeMessage = $"ssd+{accountId}+{BuildKeysFieldsArgs(keys, fields)}";
        var cancelMessage = $"usd+{accountId}+{{}}";

        // ADR-0005: register under the full wire-topic identity so this subscription receives only
        // accountId's own summary rows, never another account's (PRB-3.1).
        var routingKey = $"ssd+{accountId}";
        var (reader, unsubscribe) = await _webSocketClient.SubscribeTopicAsync(subscribeMessage, routingKey, cancelMessage, cancellationToken);

        // Per-row mapper isolation (WIR-1): a single malformed summary row is skipped and counted+logged
        // through the drop taxonomy, so the frame's other rows still deliver. A monetary row that omits
        // monetaryValue also raises the WIR-5 census signal (separate counter — the frame is delivered).
        var ssdLogger = CreateTopicLogger("ssd");
        var mapSummary = (JsonElement frame) =>
            AccountSummaryUpdateMapper.Map(
                frame,
                ex => RecordMapperDrop(ssdLogger, "ssd", ex),
                field => RecordMissingMoneyField(ssdLogger, "ssd", field));

        return new IbkrSubscription<AccountSummaryUpdate>(new ChannelObservable<AccountSummaryUpdate>(reader, mapSummary, ssdLogger, _metrics, "ssd"), unsubscribe);
    }

    /// <inheritdoc />
    public async Task<IIbkrSubscription<AccountLedgerUpdate>> AccountLedgerAsync(
        string accountId,
        string[]? keys = null,
        string[]? fields = null,
        CancellationToken cancellationToken = default)
    {
        ValidateTargetSegment(accountId, nameof(accountId));
        var subscribeMessage = $"sld+{accountId}+{BuildKeysFieldsArgs(keys, fields)}";
        var cancelMessage = $"uld+{accountId}+{{}}";

        // ADR-0005: register under the full wire-topic identity so this subscription receives only
        // accountId's own ledger rows, never another account's (PRB-3.1).
        var routingKey = $"sld+{accountId}";
        var (reader, unsubscribe) = await _webSocketClient.SubscribeTopicAsync(subscribeMessage, routingKey, cancelMessage, cancellationToken);

        // Per-row mapper isolation (WIR-1): a single malformed ledger row is skipped and counted+logged
        // through the drop taxonomy, so the frame's other currencies still deliver. A substantive row
        // that omits netLiquidationValue also raises the WIR-5 census signal (separate counter — the
        // frame is delivered).
        var sldLogger = CreateTopicLogger("sld");
        var mapLedger = (JsonElement frame) =>
            AccountLedgerUpdateMapper.Map(
                frame,
                ex => RecordMapperDrop(sldLogger, "sld", ex),
                field => RecordMissingMoneyField(sldLogger, "sld", field));

        return new IbkrSubscription<AccountLedgerUpdate>(new ChannelObservable<AccountLedgerUpdate>(reader, mapLedger, sldLogger, _metrics, "sld"), unsubscribe);
    }

    private static string BuildKeysFieldsArgs(string[]? keys, string[]? fields)
    {
        // Serializer-escape each array so consumer-supplied key/field strings never reach the wire
        // raw-interpolated (PRB-1.3); a value containing '"' or '\' produces valid JSON, not a broken
        // subscribe message.
        var parts = new List<string>();
        if (keys is { Length: > 0 })
        {
            parts.Add($"\"keys\":{JsonSerializer.Serialize(keys)}");
        }
        if (fields is { Length: > 0 })
        {
            parts.Add($"\"fields\":{JsonSerializer.Serialize(fields)}");
        }
        return $"{{{string.Join(",", parts)}}}";
    }

    /// <summary>
    /// Builds a market-data subscribe args object (<c>{"fields":[…]}</c>) with the field array
    /// serializer-escaped so consumer-supplied field strings never reach the wire raw-interpolated
    /// (PRB-1.3).
    /// </summary>
    private static string BuildFieldsArgs(string[] fields) =>
        $"{{\"fields\":{JsonSerializer.Serialize(fields)}}}";

    /// <summary>
    /// Guards a numeric contract-id target segment: a conid must be a positive integer. A non-positive
    /// value is rejected at the facade before any subscribe reaches the wire (PRB-1.3).
    /// </summary>
    private static void ValidateConid(int conid)
    {
        if (conid <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(conid), conid, "Contract id (conid) must be a positive integer.");
        }
    }

    /// <summary>
    /// Guards a consumer-supplied topic-target segment (an account id): it must be non-empty and must
    /// not contain <c>+</c> (the wire topic segment separator) or any whitespace, so a malformed target
    /// cannot corrupt the subscribe topic or bleed into another topic's routing (PRB-1.3 / ADR-0005).
    /// Validation runs before any subscribe message is built or sent.
    /// </summary>
    private static void ValidateTargetSegment(string value, string paramName)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("Streaming topic target segment must not be empty.", paramName);
        }
        if (value.Contains('+', StringComparison.Ordinal))
        {
            throw new ArgumentException("Streaming topic target segment must not contain '+'.", paramName);
        }
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                throw new ArgumentException("Streaming topic target segment must not contain whitespace.", paramName);
            }
        }
    }

    private IbkrSubscription<T> CreateUnsolicitedSubscription<T>(string topicPrefix, Func<JsonElement, T> mapper)
    {
        var (reader, unsubscribe) = _webSocketClient.RegisterUnsolicitedTopic(topicPrefix);
        return new IbkrSubscription<T>(new ChannelObservable<T>(reader, mapper, CreateTopicLogger(topicPrefix), _metrics, topicPrefix), unsubscribe);
    }

    /// <summary>Creates a logger scoped to a topic, used to trace dropped-frame warnings back to their subscription.</summary>
    private ILogger CreateTopicLogger(string topicPrefix) =>
        _loggerFactory.CreateLogger($"IbkrConduit.Streaming.{topicPrefix}");

    /// <summary>
    /// Records a per-element mapper drop through the same VCR-02 drop taxonomy the observables use
    /// for whole-frame drops: increments <c>ibkr.conduit.streaming.frames.dropped</c> with
    /// <see cref="StreamingMetrics.MapperCause"/> and logs it against the wire topic. Wired into
    /// <see cref="TradeExecutionMapper.MapMany"/>'s per-element isolation (FIL-2) so a single
    /// malformed execution is counted and logged, never silently swallowed, while the frame's
    /// remaining fills are still delivered.
    /// </summary>
    private void RecordMapperDrop(ILogger logger, string topic, Exception exception)
    {
        _metrics.RecordDrop(topic, StreamingMetrics.MapperCause);
        logger.LogDroppedFrame(topic, exception.Message, exception);
    }

    /// <summary>
    /// Records the WIR-5 required-money-field census signal: a delivered <paramref name="topic"/>
    /// frame omitted a required money field. Increments the dedicated
    /// <c>ibkr.conduit.streaming.money_field.absent</c> counter (kept out of the VCR-02 drop
    /// taxonomy — the frame is delivered, not dropped) and logs it against the wire topic so an IBKR
    /// wire-shape drift on a money field is observable rather than silent.
    /// </summary>
    private void RecordMissingMoneyField(ILogger logger, string topic, string field)
    {
        _metrics.RecordMissingMoneyField(topic, field);
        logger.LogMissingMoneyField(topic, field);
    }
}
