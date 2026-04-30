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
            () => CreateUnsolicitedObservable("sts", MapSessionStatus),
            LazyThreadSafetyMode.ExecutionAndPublication);
        _bulletins = new Lazy<IObservable<BulletinEvent>>(
            () => CreateUnsolicitedObservable("blt", MapBulletin),
            LazyThreadSafetyMode.ExecutionAndPublication);
        _tradingNotifications = new Lazy<IObservable<NotificationEvent>>(
            () => CreateUnsolicitedObservable("ntf", MapNotification),
            LazyThreadSafetyMode.ExecutionAndPublication);
        _systemEvents = new Lazy<IObservable<SystemEvent>>(
            () => CreateUnsolicitedObservable("system", MapSystemEvent),
            LazyThreadSafetyMode.ExecutionAndPublication);
        _accountStatus = new Lazy<IObservable<AccountStatusEvent>>(
            () => CreateUnsolicitedObservable("act", MapAccountStatus),
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

    private static BulletinEvent MapBulletin(JsonElement element)
    {
        if (!element.TryGetProperty("args", out var args))
        {
            return new BulletinEvent();
        }
        return new BulletinEvent
        {
            Id = args.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? string.Empty : string.Empty,
            Message = args.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? string.Empty : string.Empty,
        };
    }

    private static NotificationEvent MapNotification(JsonElement element)
    {
        if (!element.TryGetProperty("args", out var args))
        {
            return new NotificationEvent();
        }
        return new NotificationEvent
        {
            Id = args.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? string.Empty : string.Empty,
            Title = args.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? string.Empty : string.Empty,
            Text = args.TryGetProperty("text", out var textProp) ? textProp.GetString() ?? string.Empty : string.Empty,
            Url = args.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null,
        };
    }

    private static SystemEvent MapSystemEvent(JsonElement element)
    {
        var username = element.TryGetProperty("success", out var successProp)
            ? successProp.GetString()
            : null;

        long? heartbeatMs = element.TryGetProperty("hb", out var hbProp) && hbProp.ValueKind == JsonValueKind.Number
            ? hbProp.GetInt64()
            : null;

        return new SystemEvent
        {
            Username = username,
            HeartbeatMs = heartbeatMs,
        };
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

    private static AccountStatusEvent MapAccountStatus(JsonElement element)
    {
        if (!element.TryGetProperty("args", out var args))
        {
            return new AccountStatusEvent();
        }

        return new AccountStatusEvent
        {
            Accounts = ReadStringList(args, "accounts"),
            AcctProps = ReadAcctProps(args),
            Aliases = ReadStringDict(args, "aliases"),
            AllowFeatures = args.TryGetProperty("allowFeatures", out var feats)
                ? MapAccountFeatures(feats)
                : null,
            ChartPeriods = ReadChartPeriods(args),
            Groups = ReadStringList(args, "groups"),
            Profiles = ReadStringList(args, "profiles"),
            SelectedAccount = args.TryGetProperty("selectedAccount", out var sa) ? sa.GetString() ?? string.Empty : string.Empty,
            ServerInfo = args.TryGetProperty("serverInfo", out var si) ? MapServerInfo(si) : null,
            SessionId = args.TryGetProperty("sessionId", out var sid) ? sid.GetString() ?? string.Empty : string.Empty,
            IsFT = args.TryGetProperty("isFT", out var ft) && ft.ValueKind == JsonValueKind.True,
            IsPaper = args.TryGetProperty("isPaper", out var ip) && ip.ValueKind == JsonValueKind.True,
        };
    }

    private static IReadOnlyList<string> ReadStringList(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }
        var list = new List<string>(arr.GetArrayLength());
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                list.Add(item.GetString()!);
            }
        }
        return list;
    }

    private static Dictionary<string, string> ReadStringDict(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var obj) || obj.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>();
        }
        var dict = new Dictionary<string, string>();
        foreach (var prop in obj.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.String)
            {
                dict[prop.Name] = prop.Value.GetString()!;
            }
        }
        return dict;
    }

    private static Dictionary<string, AccountProperties> ReadAcctProps(JsonElement parent)
    {
        if (!parent.TryGetProperty("acctProps", out var obj) || obj.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, AccountProperties>();
        }
        var dict = new Dictionary<string, AccountProperties>();
        foreach (var prop in obj.EnumerateObject())
        {
            dict[prop.Name] = MapAccountProperties(prop.Value);
        }
        return dict;
    }

    private static AccountProperties MapAccountProperties(JsonElement el) =>
        new()
        {
            HasChildAccounts = el.TryGetProperty("hasChildAccounts", out var h) && h.ValueKind == JsonValueKind.True,
            SupportsCashQty = el.TryGetProperty("supportsCashQty", out var sc) && sc.ValueKind == JsonValueKind.True,
            NoFXConv = el.TryGetProperty("noFXConv", out var nf) && nf.ValueKind == JsonValueKind.True,
            IsProp = el.TryGetProperty("isProp", out var ip) && ip.ValueKind == JsonValueKind.True,
            SupportsFractions = el.TryGetProperty("supportsFractions", out var sf) && sf.ValueKind == JsonValueKind.True,
            AllowCustomerTime = el.TryGetProperty("allowCustomerTime", out var ac) && ac.ValueKind == JsonValueKind.True,
        };

    private static AccountFeatures MapAccountFeatures(JsonElement el) =>
        new()
        {
            ShowGFIS = el.TryGetProperty("showGFIS", out var v1) && v1.ValueKind == JsonValueKind.True,
            ShowEUCostReport = el.TryGetProperty("showEUCostReport", out var v2) && v2.ValueKind == JsonValueKind.True,
            AllowEventContract = el.TryGetProperty("allowEventContract", out var v3) && v3.ValueKind == JsonValueKind.True,
            AllowFXConv = el.TryGetProperty("allowFXConv", out var v4) && v4.ValueKind == JsonValueKind.True,
            AllowFinancialLens = el.TryGetProperty("allowFinancialLens", out var v5) && v5.ValueKind == JsonValueKind.True,
            AllowMTA = el.TryGetProperty("allowMTA", out var v6) && v6.ValueKind == JsonValueKind.True,
            AllowTypeAhead = el.TryGetProperty("allowTypeAhead", out var v7) && v7.ValueKind == JsonValueKind.True,
            AllowEventTrading = el.TryGetProperty("allowEventTrading", out var v8) && v8.ValueKind == JsonValueKind.True,
            SnapshotRefreshTimeout = el.TryGetProperty("snapshotRefreshTimeout", out var srt) && srt.ValueKind == JsonValueKind.Number ? srt.GetInt32() : null,
            LiteUser = el.TryGetProperty("liteUser", out var v10) && v10.ValueKind == JsonValueKind.True,
            ShowWebNews = el.TryGetProperty("showWebNews", out var v11) && v11.ValueKind == JsonValueKind.True,
            Research = el.TryGetProperty("research", out var v12) && v12.ValueKind == JsonValueKind.True,
            DebugPnl = el.TryGetProperty("debugPnl", out var v13) && v13.ValueKind == JsonValueKind.True,
            ShowTaxOpt = el.TryGetProperty("showTaxOpt", out var v14) && v14.ValueKind == JsonValueKind.True,
            ShowImpactDashboard = el.TryGetProperty("showImpactDashboard", out var v15) && v15.ValueKind == JsonValueKind.True,
            AllowDynAccount = el.TryGetProperty("allowDynAccount", out var v16) && v16.ValueKind == JsonValueKind.True,
            AllowCrypto = el.TryGetProperty("allowCrypto", out var v17) && v17.ValueKind == JsonValueKind.True,
            AllowedAssetTypes = el.TryGetProperty("allowedAssetTypes", out var aat) ? aat.GetString() : null,
        };

    private static Dictionary<string, IReadOnlyList<string>> ReadChartPeriods(JsonElement parent)
    {
        if (!parent.TryGetProperty("chartPeriods", out var obj) || obj.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, IReadOnlyList<string>>();
        }
        var dict = new Dictionary<string, IReadOnlyList<string>>();
        foreach (var prop in obj.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }
            var list = new List<string>(prop.Value.GetArrayLength());
            foreach (var item in prop.Value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    list.Add(item.GetString()!);
                }
            }
            dict[prop.Name] = list;
        }
        return dict;
    }

    private static StreamingServerInfo MapServerInfo(JsonElement el) =>
        new()
        {
            ServerName = el.TryGetProperty("serverName", out var sn) ? sn.GetString() ?? string.Empty : string.Empty,
            ServerVersion = el.TryGetProperty("serverVersion", out var sv) ? sv.GetString() ?? string.Empty : string.Empty,
        };
}
