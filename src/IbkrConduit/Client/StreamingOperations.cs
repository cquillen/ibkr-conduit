using System.Text.Json;
using System.Threading;
using IbkrConduit.Streaming;
using IbkrConduit.Streaming.Mappers;

namespace IbkrConduit.Client;

/// <summary>
/// Implementation of <see cref="IStreamingOperations"/> that builds topic subscribe messages,
/// delegates JSON-to-DTO transformations to the per-topic mappers in
/// <c>IbkrConduit.Streaming.Mappers</c>, and returns <see cref="IObservable{T}"/> streams via
/// <see cref="ChannelObservable{T}"/>.
/// </summary>
internal sealed class StreamingOperations : IStreamingOperations
{
    private readonly IIbkrWebSocketClient _webSocketClient;
    private readonly Lazy<IObservable<SessionStatusEvent>> _sessionStatus;
    private readonly Lazy<IObservable<BulletinEvent>> _bulletins;
    private readonly Lazy<IObservable<NotificationEvent>> _tradingNotifications;
    private readonly Lazy<IObservable<SystemEvent>> _systemEvents;
    private readonly Lazy<IObservable<AccountStatusEvent>> _accountStatus;

    /// <summary>
    /// Creates a new <see cref="StreamingOperations"/>.
    /// </summary>
    /// <param name="webSocketClient">The underlying WebSocket client.</param>
    public StreamingOperations(IIbkrWebSocketClient webSocketClient)
    {
        _webSocketClient = webSocketClient;
        _sessionStatus = new Lazy<IObservable<SessionStatusEvent>>(
            () => CreateUnsolicitedObservable("sts", SessionStatusMapper.Map),
            LazyThreadSafetyMode.ExecutionAndPublication);
        _bulletins = new Lazy<IObservable<BulletinEvent>>(
            () => CreateUnsolicitedObservable("blt", BulletinMapper.Map),
            LazyThreadSafetyMode.ExecutionAndPublication);
        _tradingNotifications = new Lazy<IObservable<NotificationEvent>>(
            () => CreateUnsolicitedObservable("ntf", NotificationMapper.Map),
            LazyThreadSafetyMode.ExecutionAndPublication);
        _systemEvents = new Lazy<IObservable<SystemEvent>>(
            () => CreateUnsolicitedObservable("system", SystemEventMapper.Map),
            LazyThreadSafetyMode.ExecutionAndPublication);
        _accountStatus = new Lazy<IObservable<AccountStatusEvent>>(
            () => CreateUnsolicitedObservable("act", AccountStatusMapper.Map),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public IObservable<SessionStatusEvent> SessionStatus => _sessionStatus.Value;

    /// <inheritdoc />
    public IObservable<BulletinEvent> Bulletins => _bulletins.Value;

    /// <inheritdoc />
    public IObservable<NotificationEvent> TradingNotifications => _tradingNotifications.Value;

    /// <inheritdoc />
    public IObservable<SystemEvent> SystemEvents => _systemEvents.Value;

    /// <inheritdoc />
    public IObservable<AccountStatusEvent> AccountStatus => _accountStatus.Value;

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

        return new ChannelObservable<MarketDataTick>(reader, MarketDataTickMapper.Map);
    }

    /// <inheritdoc />
    public async Task<IObservable<OrderUpdate>> OrderUpdatesAsync(int? days = null, CancellationToken cancellationToken = default)
    {
        var subscribeMessage = days.HasValue
            ? $"sor+{{\"days\":{days.Value}}}"
            : "sor+{}";

        var (reader, _) = await _webSocketClient.SubscribeTopicAsync(subscribeMessage, "sor", cancellationToken);

        return new ChannelObservable<OrderUpdate>(reader, OrderUpdateMapper.Map);
    }

    /// <inheritdoc />
    public async Task<IObservable<PnlUpdate>> ProfitAndLossAsync(CancellationToken cancellationToken = default)
    {
        var (reader, _) = await _webSocketClient.SubscribeTopicAsync("spl+{}", "spl", cancellationToken);

        return new ChannelObservable<PnlUpdate>(reader, PnlUpdateMapper.Map);
    }

    /// <inheritdoc />
    public async Task<IObservable<AccountSummaryUpdate>> AccountSummaryAsync(CancellationToken cancellationToken = default)
    {
        var (reader, _) = await _webSocketClient.SubscribeTopicAsync("ssd+{}", "ssd", cancellationToken);

        return new ChannelObservable<AccountSummaryUpdate>(reader, AccountSummaryUpdateMapper.Map);
    }

    /// <inheritdoc />
    public async Task<IObservable<AccountLedgerUpdate>> AccountLedgerAsync(CancellationToken cancellationToken = default)
    {
        var (reader, _) = await _webSocketClient.SubscribeTopicAsync("sld+{}", "sld", cancellationToken);

        return new ChannelObservable<AccountLedgerUpdate>(reader, AccountLedgerUpdateMapper.Map);
    }

    private ChannelObservable<T> CreateUnsolicitedObservable<T>(string topicPrefix, Func<JsonElement, T> mapper)
    {
        var (reader, _) = _webSocketClient.RegisterUnsolicitedTopic(topicPrefix);
        return new ChannelObservable<T>(reader, mapper);
    }
}
