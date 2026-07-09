using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using IbkrConduit.Auth;
using IbkrConduit.Diagnostics;
using IbkrConduit.Errors;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging;
using Refit;

namespace IbkrConduit.Streaming;

/// <summary>
/// Internal WebSocket client that manages the connection to the IBKR WebSocket API,
/// heartbeat, message pump, and topic-based routing to subscribers.
/// </summary>
internal sealed partial class IbkrWebSocketClient : IIbkrWebSocketClient
{
    private const string _webSocketBaseUrl = "wss://api.ibkr.com/v1/api/ws";
    private const int _reconnectDelayMs = 1000;
    private const int _receiveBufferSize = 8192;

    private static readonly Counter<long> _messagesReceived =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.websocket.messages.received");

    private static readonly Counter<long> _messagesSent =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.websocket.messages.sent");

    private static readonly Counter<long> _unsubscribeCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.websocket.unsubscribe.count");

    private static readonly Counter<long> _reconnectCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.websocket.reconnect.count");

    private static readonly Counter<long> _heartbeatCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.websocket.heartbeat.count");

    private readonly IIbkrSessionApi _sessionApi;
    private readonly ISessionManager _sessionManager;
    private readonly IbkrOAuthCredentials _credentials;
    private readonly ISessionLifecycleNotifier _notifier;
    private readonly ILogger<IbkrWebSocketClient> _logger;
    private readonly TenantContext _tenant;
    private readonly StreamingMetrics _metrics;
    private readonly Dictionary<string, object> _logScope;
    private readonly Func<IWebSocketAdapter> _webSocketFactory;
    private readonly int _heartbeatIntervalSeconds;
    private readonly int _streamingBufferSize;
    private readonly TimeProvider _timeProvider;
    private readonly string _resolvedWebSocketBaseUrl;
    private readonly ConcurrentDictionary<string, List<ChannelWriter<JsonElement>>> _subscribers = new();
    private readonly List<TopicSubscription> _subscriptions = [];
    private readonly List<ChannelWriter<ConnectionEvent>> _connectionEventWriters = [];
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly object _subscriptionLock = new();
    private readonly object _connectionEventLock = new();

    // Log-throttle state for buffer-overflow drops: the first eviction on a topic per connection
    // logs a Warning; subsequent evictions on that topic are counted (not logged) until the next
    // reconnect clears the set — preventing a log flood under a stalled consumer (ADR-0002).
    private readonly HashSet<string> _loggedOverflowTopics = [];
    private readonly object _overflowLogLock = new();

    // Log-throttle state for unmatched target-qualified drops (ADR-0005): the first unmatched frame
    // on a topic prefix per connection logs a Warning; subsequent unmatched frames on that prefix are
    // counted (not logged) until the next reconnect clears the set — the same anti-flood discipline
    // the overflow throttle uses, so a server streaming a stale per-target topic cannot flood the log.
    private readonly HashSet<string> _loggedUnmatchedTopics = [];
    private readonly object _unmatchedLogLock = new();

    // Per-tenant Meter that owns the connection_state gauge. It shares the public meter name so
    // consumers subscribing to "IbkrConduit" still see the gauge, but is instance-scoped and
    // disposed with this client — so add/remove churn accumulates no stale gauges pinning disposed
    // clients alive and reporting 0 forever (VCR-09 / FIL-7). No static mutable state.
    private readonly Meter _connectionStateMeter;

    private IWebSocketAdapter? _webSocket;
    private CancellationTokenSource? _heartbeatCts;
    private CancellationTokenSource? _messagePumpCts;
    private IDisposable? _notifierSubscription;
    private IDisposable? _tickleWatchdogSubscription;
    private readonly CancellationTokenSource _disposeCts = new();

    /// <summary>
    /// Monotonic connection generation, incremented under <see cref="_connectLock"/> each time a new
    /// socket is established in <see cref="ConnectCoreAsync"/>. A reconnect trigger captures the
    /// generation it observed; a stale trigger whose generation the current connection has advanced
    /// past no-ops under the lock rather than tearing down a healthy newer connection (STR-5).
    /// </summary>
    private long _connectionEpoch;

    /// <summary>
    /// Stores the ticks value of the last received message timestamp, or 0 if none.
    /// Uses Interlocked for thread-safe access since DateTimeOffset? cannot be volatile.
    /// </summary>
    private long _lastMessageReceivedAtTicks;

    private volatile bool _disposed;

    /// <summary>
    /// Idempotency gate for <see cref="DisposeAsync"/>: the first caller runs teardown, later
    /// callers no-op. Distinct from <see cref="_disposed"/>, which is the observable state flag set
    /// under <see cref="_connectLock"/> during teardown (STR-3).
    /// </summary>
    private int _disposeStarted;

    /// <summary>
    /// Creates a new <see cref="IbkrWebSocketClient"/>.
    /// </summary>
    /// <param name="sessionApi">Session API for tickle calls.</param>
    /// <param name="sessionManager">Brokerage session manager. Its <see cref="ISessionManager.EnsureInitializedAsync"/> is invoked before the WebSocket opens so iserver-dependent topics are not rejected with "Missing iserver bridge".</param>
    /// <param name="credentials">OAuth credentials containing the access token.</param>
    /// <param name="notifier">Session lifecycle notifier for reconnect on re-auth.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="webSocketFactory">Factory for creating WebSocket adapter instances.</param>
    /// <param name="heartbeatIntervalSeconds">Seconds between "tic" ping messages used to keep the WebSocket session alive. IBKR requires at least one per minute.</param>
    /// <param name="streamingBufferSize">Per-subscriber channel capacity. When full, the oldest message is dropped to make room for the newest.</param>
    /// <param name="tenant">Per-provider tenant identity used to tag telemetry.</param>
    /// <param name="metrics">Reporter that counts every dropped frame (overflow evictions here) so no streaming loss is silent.</param>
    /// <param name="timeProvider">Time provider for delays; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="webSocketBaseUrl">Override for the WebSocket base URL. When null, defaults to <c>wss://api.ibkr.com/v1/api/ws</c>.</param>
    public IbkrWebSocketClient(
        IIbkrSessionApi sessionApi,
        ISessionManager sessionManager,
        IbkrOAuthCredentials credentials,
        ISessionLifecycleNotifier notifier,
        ILogger<IbkrWebSocketClient> logger,
        Func<IWebSocketAdapter> webSocketFactory,
        int heartbeatIntervalSeconds,
        int streamingBufferSize,
        TenantContext tenant,
        StreamingMetrics metrics,
        TimeProvider? timeProvider = null,
        string? webSocketBaseUrl = null)
    {
        _sessionApi = sessionApi;
        _sessionManager = sessionManager;
        _credentials = credentials;
        _notifier = notifier;
        _logger = logger;
        _tenant = tenant;
        _metrics = metrics;
        _logScope = new Dictionary<string, object> { [LogFields.TenantId] = _tenant.TenantId };
        _webSocketFactory = webSocketFactory;
        _heartbeatIntervalSeconds = heartbeatIntervalSeconds;
        _streamingBufferSize = streamingBufferSize;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _resolvedWebSocketBaseUrl = webSocketBaseUrl ?? _webSocketBaseUrl;

        _connectionStateMeter = new Meter(IbkrConduitDiagnostics.MeterName);
        _connectionStateMeter.CreateObservableGauge(
            "ibkr.conduit.websocket.connection_state",
            ObserveConnectionState);

        _notifierSubscription = _notifier.Subscribe(OnSessionRefreshedAsync);
        _tickleWatchdogSubscription = _notifier.SubscribeTickleSucceeded(OnTickleSucceededAsync);
    }

    /// <inheritdoc />
    public bool IsConnected
    {
        get
        {
            var ws = _webSocket;
            return ws is { State: WebSocketState.Open };
        }
    }

    /// <inheritdoc />
    public int ActiveSubscriptionCount
    {
        get
        {
            lock (_subscriptionLock)
            {
                return _subscriptions.Count;
            }
        }
    }

    /// <inheritdoc />
    public DateTimeOffset? LastMessageReceivedAt
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastMessageReceivedAtTicks);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    /// <summary>
    /// The current connection generation (0 before the first successful connect). Exposed for tests
    /// asserting that a stale reconnect trigger did not advance the generation (STR-5).
    /// </summary>
    internal long ConnectionEpoch => Volatile.Read(ref _connectionEpoch);

    /// <summary>
    /// Test seam (FO-4): the number of distinct routing keys currently mapped in
    /// <see cref="_subscribers"/>. Asserts empty writer-lists are reaped once their last writer
    /// unsubscribes rather than left mapped as an unbounded per-key leak.
    /// </summary>
    internal int SubscriberKeyCount => _subscribers.Count;

    /// <summary>
    /// Test seam (FO-4): whether <see cref="_subscribers"/> currently maps <paramref name="routingKey"/>.
    /// </summary>
    /// <param name="routingKey">The full-topic-identity routing key to probe.</param>
    internal bool HasSubscriberKey(string routingKey) => _subscribers.ContainsKey(routingKey);

    /// <summary>
    /// Test-only hook (FO-4): invoked inside <see cref="SubscribeTopicAsync"/> immediately after the
    /// <c>GetOrAdd</c> that captures the subscriber list, but before the registration locks are taken —
    /// the exact window in which a concurrent last-writer unsubscribe can reap the captured list. A
    /// test sets this to deterministically drive that reap into the race window; <see langword="null"/>
    /// in production (no-op).
    /// </summary>
    internal Action? OnSubscribeListCaptured { get; set; }

    /// <summary>
    /// Connects to the IBKR WebSocket API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Ensure the brokerage session (ssodh/init) is established before opening the
        // WebSocket, so iserver-dependent subscriptions (sor/str/smd/spl/ssd/sld) are not
        // rejected by IBKR with "Missing iserver bridge". Idempotent: a no-op once the
        // session is already initialized.
        //
        // Deliberately done here rather than in ConnectCoreAsync: ConnectCoreAsync is also
        // the internal reconnect path, which runs inside ReauthenticateAsync while it holds
        // the session lock. Re-entering session initialization there would deadlock. On
        // reconnect the session is already established (by tickle keepalive or the reauth
        // itself), so no re-initialization is needed.
        await _sessionManager.EnsureInitializedAsync(cancellationToken);

        await _connectLock.WaitAsync(cancellationToken);
        try
        {
            await ConnectCoreAsync(cancellationToken);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <summary>
    /// Subscribes to a WebSocket topic and returns a <see cref="ChannelReader{T}"/> for receiving messages,
    /// plus an asynchronous unsubscribe delegate.
    /// </summary>
    /// <param name="subscribeMessage">The subscribe message to send on the WebSocket.</param>
    /// <param name="routingKey">
    /// The routing key this subscription registers and dispatches under (ADR-0005): the full
    /// wire-topic identity for a target-qualified topic (e.g. <c>smd+265598</c>, <c>ssd+DU1234567</c>)
    /// so a frame reaches only its own target's subscribers, or the bare prefix for a target-less
    /// topic (e.g. <c>sor</c>, <c>spl</c>). The bare prefix is derived from it for metrics.
    /// </param>
    /// <param name="cancelMessage">
    /// The IBKR unsubscribe message to send when the last subscription for this cancel
    /// message is torn down, or <see langword="null"/> for local-teardown-only topics.
    /// Subscriptions sharing the same cancel message are refcounted; the cancel is only
    /// sent once the final subscriber referencing it unsubscribes.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple of the channel reader and an asynchronous unsubscribe delegate.</returns>
    /// <remarks>
    /// If the WebSocket is not yet connected, the subscription is queued in memory
    /// and replayed automatically when <see cref="ConnectAsync"/> is called. No wire
    /// message is sent until the connection is open. The returned channel reader is
    /// usable immediately; messages will start flowing once <see cref="ConnectAsync"/>
    /// completes.
    /// <para>
    /// <b>May block on an in-flight reconnect:</b> registration and send happen under
    /// <c>_connectLock</c> (CON-3) so a subscribe cannot race a reconnect's replay — this
    /// serializes a subscribe call behind any in-progress reconnect, which can take tens of
    /// seconds under a degraded network. Callers needing a bound should pass a
    /// <paramref name="cancellationToken"/> with a deadline.
    /// </para>
    /// </remarks>
    public async Task<(ChannelReader<JsonElement> Reader, Func<CancellationToken, ValueTask> Unsubscribe)> SubscribeTopicAsync(
        string subscribeMessage,
        string routingKey,
        string? cancelMessage,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // ADR-0005: a target-qualified subscription registers under its full wire-topic identity
        // (routingKey, e.g. smd+265598) so ProcessMessage delivers a frame only to its own target's
        // subscribers. The bare prefix is derived for metrics/connection-event topic lists, which must
        // stay bounded (one entry per topic type, not per conid/account).
        var topicPrefix = ExtractTopicPrefix(routingKey);

        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.WebSocket.Subscribe");
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
        activity?.SetTag(LogFields.Topic, routingKey);

        var channel = Channel.CreateBounded<JsonElement>(
            new BoundedChannelOptions(_streamingBufferSize)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
            },
            itemDropped: _ => OnFrameDropped(topicPrefix));

        var entry = new TopicSubscription(routingKey, topicPrefix, subscribeMessage, cancelMessage, channel.Writer);
        var writers = _subscribers.GetOrAdd(routingKey, _ => []);

        // FO-4 test seam: the reap-vs-subscribe race window opens here, after the list is captured but
        // before the registration locks are taken. No-op in production.
        OnSubscribeListCaptured?.Invoke();

        // CON-3: register and send under _connectLock so a subscribe cannot race a reconnect's
        // replay — the replay snapshot (taken under this lock in ConnectCoreAsync) and this direct
        // send are serialized, so each subscribe message goes out at most once per connection rather
        // than being both replayed and direct-sent. Lock order _connectLock -> _subscriptionLock
        // matches ConnectCoreAsync and DisposeAsync.
        try
        {
            await _connectLock.WaitAsync(cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            // _connectLock was disposed by a concurrent DisposeAsync — the client is gone.
            channel.Writer.TryComplete();
            throw new ObjectDisposedException(GetType().FullName);
        }

        try
        {
            lock (_subscriptionLock)
            {
                lock (writers)
                {
                    writers.Add(channel.Writer);
                }
                _subscriptions.Add(entry);

                // CON-2: a DisposeAsync that set _disposed and swept the registries in the window
                // between its own _connectLock release and semaphore disposal could leave us here
                // holding the lock with _disposed already true. Re-check under the same lock
                // DisposeAsync's sweep takes; if disposed, roll the registration back and fail rather
                // than leak a never-completed writer.
                if (_disposed)
                {
                    _subscriptions.Remove(entry);
                    lock (writers)
                    {
                        writers.Remove(channel.Writer);
                    }
                    channel.Writer.TryComplete();
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }

            // Only send the subscribe message immediately if the WebSocket is already open.
            // Otherwise leave it in the registry and let ConnectCoreAsync's replay path send it after
            // the WebSocket opens. Holding _connectLock keeps _webSocket stable across the check and
            // the send.
            if (_webSocket?.State == WebSocketState.Open)
            {
                try
                {
                    await SendTextAsync(subscribeMessage, cancellationToken);
                }
                catch
                {
                    // STR-4: the registration is already committed; a send that throws (socket racing
                    // Open->Aborted, or the caller's token firing mid-send) would orphan the entry —
                    // replayed on every reconnect into a channel nobody reads, its CancelMessage
                    // suppressing a sibling's wire cancel via the refcount check. Roll the
                    // registration back and complete the channel before rethrowing so a failed
                    // subscribe leaves no registered state.
                    lock (_subscriptionLock)
                    {
                        _subscriptions.Remove(entry);
                        lock (writers)
                        {
                            writers.Remove(channel.Writer);
                        }
                    }
                    channel.Writer.TryComplete();
                    throw;
                }
            }
        }
        finally
        {
            try
            {
                _connectLock.Release();
            }
            catch (ObjectDisposedException)
            {
                // Semaphore disposed by a dispose that raced our critical section — safe to ignore.
            }
        }

        return (channel.Reader, ct => UnsubscribeSolicitedAsync(entry, ct));
    }

    /// <inheritdoc />
    public (ChannelReader<JsonElement> Reader, Func<CancellationToken, ValueTask> Unsubscribe) RegisterUnsolicitedTopic(string topicPrefix)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var channel = Channel.CreateBounded<JsonElement>(
            new BoundedChannelOptions(_streamingBufferSize)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
            },
            itemDropped: _ => OnFrameDropped(topicPrefix));

        var writers = _subscribers.GetOrAdd(topicPrefix, _ => []);
        lock (_subscriptionLock)
        {
            lock (writers)
            {
                writers.Add(channel.Writer);
            }

            // CON-2: mirror SubscribeTopicAsync — roll back and fail if a concurrent dispose swept
            // the registries after the entry guard, rather than leak a never-completed writer.
            if (_disposed)
            {
                lock (writers)
                {
                    writers.Remove(channel.Writer);
                }
                channel.Writer.TryComplete();
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        return (channel.Reader, _ =>
        {
            if (_subscribers.TryGetValue(topicPrefix, out var existingWriters))
            {
                lock (existingWriters)
                {
                    existingWriters.Remove(channel.Writer);
                }
            }
            channel.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
        );
    }

    /// <inheritdoc />
    public (ChannelReader<ConnectionEvent> Reader, Func<CancellationToken, ValueTask> Unsubscribe) RegisterConnectionEvents()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Unbounded: connection-lifecycle events are low-frequency (one pair per reconnect) and are
        // the consumer's reconciliation trigger — they must never be dropped under overflow.
        var channel = Channel.CreateUnbounded<ConnectionEvent>(new UnboundedChannelOptions { SingleReader = true });

        lock (_connectionEventLock)
        {
            _connectionEventWriters.Add(channel.Writer);

            // CON-2: roll back and fail if a concurrent dispose swept the connection-event writers
            // after the entry guard, rather than leak a never-completed writer.
            if (_disposed)
            {
                _connectionEventWriters.Remove(channel.Writer);
                channel.Writer.TryComplete();
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        return (channel.Reader, _ =>
        {
            lock (_connectionEventLock)
            {
                _connectionEventWriters.Remove(channel.Writer);
            }
            channel.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
        );
    }

    /// <summary>
    /// Records a dropped frame on the given wire topic under buffer overflow: increments the
    /// dropped-frames counter (<c>cause=overflow</c>) and logs a Warning on the first eviction for
    /// this topic since the current connection was established. No overflow eviction is silent
    /// (finding FIL-1).
    /// </summary>
    private void OnFrameDropped(string topic)
    {
        _metrics.RecordDrop(topic, StreamingMetrics.OverflowCause);

        bool firstForTopic;
        lock (_overflowLogLock)
        {
            firstForTopic = _loggedOverflowTopics.Add(topic);
        }
        if (firstForTopic)
        {
            LogFrameOverflowDropped(topic);
        }
    }

    /// <summary>
    /// Observes the connection_state gauge: 1 when the socket is open, 0 otherwise, stamped with
    /// the tenant id. After disposal it yields no measurement — the gauge falls silent rather than
    /// reporting 0 forever for a disposed client (FIL-7).
    /// </summary>
    private IEnumerable<Measurement<int>> ObserveConnectionState()
    {
        if (_disposed)
        {
            yield break;
        }

        yield return new Measurement<int>(
            _webSocket is { State: WebSocketState.Open } ? 1 : 0,
            new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId));
    }

    /// <summary>Broadcasts a connection-lifecycle event to every registered connection-event subscriber.</summary>
    private void PublishConnectionEvent(ConnectionEvent connectionEvent)
    {
        lock (_connectionEventLock)
        {
            foreach (var writer in _connectionEventWriters)
            {
                writer.TryWrite(connectionEvent);
            }
        }
    }

    /// <summary>The distinct wire topic prefixes that are replayed on a reconnect (solicited subscriptions only).</summary>
    private string[] GetReplayedTopics()
    {
        lock (_subscriptionLock)
        {
            return _subscriptions.Select(s => s.TopicPrefix).Distinct().ToArray();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Idempotent: only the first caller runs teardown.
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
        {
            return;
        }

        _notifierSubscription?.Dispose();
        _notifierSubscription = null;

        _tickleWatchdogSubscription?.Dispose();
        _tickleWatchdogSubscription = null;

        // Signal in-flight connect/reconnect work (linked to _disposeCts regardless of the caller's
        // token) to abort, so the current holder of _connectLock releases promptly and the
        // untokened WaitAsync below cannot deadlock (STR-3).
        await _disposeCts.CancelAsync();

        // Serialize teardown with any in-flight connect/reconnect by acquiring _connectLock (without
        // the now-cancelled dispose token). This guarantees we never dispose the semaphore under a
        // live reconnect, never run DisconnectAsync concurrently with the reconnect's own, and — by
        // setting _disposed and clearing _subscriptions here — that a straggler reconnect that
        // slips in later finds the client disposed and nothing to replay (STR-3).
        await _connectLock.WaitAsync();
        try
        {
            _disposed = true;
            await DisconnectAsync();

            lock (_subscriptionLock)
            {
                _subscriptions.Clear();
            }
        }
        finally
        {
            _connectLock.Release();
        }

        // Complete all channel writers. Under _subscriptionLock so a subscribe racing this sweep
        // (CON-2) is serialized: it either added its writer before this sweep (completed here) or
        // observes _disposed under the lock and rolls back — never a live writer left behind.
        lock (_subscriptionLock)
        {
            foreach (var kvp in _subscribers)
            {
                lock (kvp.Value)
                {
                    foreach (var writer in kvp.Value)
                    {
                        writer.TryComplete();
                    }
                }
            }

            _subscribers.Clear();
        }

        // Complete connection-event subscriber channels so their observables complete.
        lock (_connectionEventLock)
        {
            foreach (var writer in _connectionEventWriters)
            {
                writer.TryComplete();
            }
            _connectionEventWriters.Clear();
        }

        _connectLock.Dispose();
        _disposeCts.Dispose();

        // Dispose the per-tenant Meter last so the connection_state gauge is removed with the
        // client — its callback is already disposal-gated above (FIL-7).
        _connectionStateMeter.Dispose();
    }

    private async Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        // Re-check under _connectLock (held by the caller): never open a socket on a disposed
        // client, even if a straggler reconnect reached here after teardown began (STR-3).
        if (_disposed)
        {
            return;
        }

        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            return;
        }

        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.WebSocket.Connect");
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
        activity?.SetTag("url", _resolvedWebSocketBaseUrl);
        using var _ = _logger.BeginScope(_logScope);

        LogConnecting();

        TickleResponse tickleResponse;
        try
        {
            tickleResponse = await _sessionApi.TickleAsync(cancellationToken);
        }
        catch (ApiRequestException ex)
        {
            // Refit 11 wraps the original SendAsync exception; re-throw it so callers observe the same
            // exception type (transport/timeout/cancellation) they did under Refit 10.
            ex.RethrowOriginal();
            throw; // unreachable unless InnerException is null
        }

        var uri = new Uri($"{_resolvedWebSocketBaseUrl}?oauth_token={_credentials.AccessToken}");
        var ws = _webSocketFactory();
        try
        {
            ws.SetRequestHeader("Cookie", $"api={tickleResponse.Session}");
            ws.SetRequestHeader("User-Agent", "ClientPortalGW/1");

            // Use system proxy if configured (e.g., HTTPS_PROXY environment variable)
            ws.Proxy = System.Net.WebRequest.DefaultWebProxy;

            await ws.ConnectAsync(uri, cancellationToken);
        }
        catch
        {
            // STR-6: the factory-created adapter is only owned by _webSocket once assigned below;
            // if header/proxy setup or the connect throws (or cancels), dispose it here so a
            // WS-only outage retrying per tickle does not drip undisposed adapters.
            await ws.DisposeAsync();
            throw;
        }

        _webSocket = ws;

        // A new socket is a new connection generation. Bump under _connectLock (held here) so a
        // reconnect trigger raised against an older generation is stale and no-ops under the lock
        // (STR-5). The heartbeat and message pump for this connection capture this generation and
        // stamp it on any reconnect they trigger.
        var epoch = Interlocked.Increment(ref _connectionEpoch);

        LogConnected();

        // A new socket is a new connection: reset the per-connection log throttles so the first
        // overflow eviction and first unmatched drop on each topic log again after a reconnect.
        lock (_overflowLogLock)
        {
            _loggedOverflowTopics.Clear();
        }
        lock (_unmatchedLogLock)
        {
            _loggedUnmatchedTopics.Clear();
        }

        StartHeartbeat(epoch);
        StartMessagePump(epoch);
        await ReplayActiveSubscriptionsAsync(cancellationToken);
    }

    private async Task DisconnectAsync()
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.WebSocket.Disconnect");
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);

        _heartbeatCts?.Cancel();
        _messagePumpCts?.Cancel();

        var ws = _webSocket;
        _webSocket = null;

        if (ws != null && ws.State == WebSocketState.Open)
        {
            try
            {
                await ws.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Closing",
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                LogDisconnectError(ex);
            }
        }

        if (ws != null)
        {
            await ws.DisposeAsync();
        }

        _heartbeatCts?.Dispose();
        _heartbeatCts = null;
        _messagePumpCts?.Dispose();
        _messagePumpCts = null;
    }

    private void StartHeartbeat(long epoch)
    {
        _heartbeatCts?.Cancel();
        _heartbeatCts?.Dispose();
        _heartbeatCts = new CancellationTokenSource();
        var ct = _heartbeatCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_heartbeatIntervalSeconds), _timeProvider, ct);
                    try
                    {
                        await SendTextAsync("tic", ct);
                        _heartbeatCount.Add(1,
                            new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId));
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        LogHeartbeatError(ex);
                        _ = Task.Run(() => ReconnectAsync("heartbeat_failure", epoch, _disposeCts.Token));
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
        }, ct);
    }

    private void StartMessagePump(long epoch)
    {
        _messagePumpCts?.Cancel();
        _messagePumpCts?.Dispose();
        _messagePumpCts = new CancellationTokenSource();
        var ct = _messagePumpCts.Token;

        _ = Task.Run(async () =>
        {
            var buffer = new byte[_receiveBufferSize];
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var ws = _webSocket;
                    if (ws == null || ws.State != WebSocketState.Open)
                    {
                        break;
                    }

                    try
                    {
                        using var ms = new MemoryStream();
                        ValueWebSocketReceiveResult result;
                        do
                        {
                            result = await ws.ReceiveAsync(
                                buffer.AsMemory(), ct);
                            ms.Write(buffer, 0, result.Count);
                        }
                        while (!result.EndOfMessage);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            LogWebSocketClosed();
                            _ = Task.Run(() => ReconnectAsync("server_close", epoch, _disposeCts.Token));
                            break;
                        }

                        Interlocked.Exchange(ref _lastMessageReceivedAtTicks, _timeProvider.GetUtcNow().UtcTicks);

                        var text = Encoding.UTF8.GetString(ms.ToArray());
                        if (_logger.IsEnabled(LogLevel.Trace))
                        {
                            LogIncomingMessage(text);
                        }
                        ProcessMessage(text);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (WebSocketException ex)
                    {
                        LogWebSocketError(ex);
                        _ = Task.Run(() => ReconnectAsync("receive_error", epoch, _disposeCts.Token));
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
        }, ct);
    }

    private void ProcessMessage(string text)
    {
        JsonElement root;
        try
        {
            root = JsonDocument.Parse(text).RootElement;
        }
        catch (JsonException ex)
        {
            LogMessageParseError(ex);
            return;
        }

        if (!root.TryGetProperty("topic", out var topicElement))
        {
            return;
        }

        var topic = topicElement.GetString();
        if (topic == null)
        {
            return;
        }

        // Drop heartbeat echoes; sts and system are now surfaced via the
        // unsolicited-topic dispatch path below.
        if (topic == "tic")
        {
            return;
        }

        // Extract prefix: "smd+265598" -> "smd", "sor" -> "sor". A '+' marks a target-qualified topic.
        var plusIndex = topic.IndexOf('+');
        var prefix = plusIndex >= 0 ? topic[..plusIndex] : topic;
        var isTargetQualified = plusIndex >= 0;

        _messagesReceived.Add(1,
            new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId),
            new KeyValuePair<string, object?>(LogFields.Topic, prefix));

        // ADR-0005 full-topic-identity routing: dispatch by the frame's full wire topic first. A
        // target-qualified subscription (smd+265598, ssd+DU…) registers under its full identity, so a
        // frame reaches exactly its own target's subscribers — never another target sharing the prefix.
        if (_subscribers.TryGetValue(topic, out var exactWriters) && TryDispatch(exactWriters, root))
        {
            return;
        }

        // Prefix fallback for target-less/unsolicited topics (sor, spl, str, sts, system, act, …),
        // which register under the bare prefix and never carry a target segment. Guarded by
        // isTargetQualified because a target-less frame's topic already equals its prefix (handled
        // above): this only serves the case where a target-qualified frame's prefix was itself
        // registered as a prefix-scoped subscription. Target-qualified topics (smd/ssd/sld) never
        // register a bare prefix, so this never cross-delivers between two targets of the same prefix.
        if (isTargetQualified && _subscribers.TryGetValue(prefix, out var prefixWriters) && TryDispatch(prefixWriters, root))
        {
            return;
        }

        // ADR-0005 §4: a target-qualified frame that matched no live subscription (a late frame after
        // unsubscribe, or a server-initiated per-target stream) is dropped observably rather than
        // cross-delivered or silently discarded. A target-less frame with no subscriber stays a silent
        // no-op — an unsolicited topic the consumer simply never registered for.
        if (isTargetQualified)
        {
            OnUnmatchedFrame(prefix, topic);
        }
    }

    /// <summary>
    /// Writes <paramref name="root"/> to every writer registered under a routing key. Returns
    /// <see langword="true"/> only when at least one writer was present: an empty list — every
    /// subscriber gone but the key not yet reaped — counts as no match, so a target-qualified frame
    /// arriving after the last unsubscribe is dropped observably rather than treated as delivered.
    /// </summary>
    private static bool TryDispatch(List<ChannelWriter<JsonElement>> writers, JsonElement root)
    {
        lock (writers)
        {
            if (writers.Count == 0)
            {
                return false;
            }

            foreach (var writer in writers)
            {
                writer.TryWrite(root);
            }

            return true;
        }
    }

    /// <summary>
    /// Records a target-qualified frame whose full wire-topic identity matched no live subscription
    /// (ADR-0005 §4): increments the drop counter under <see cref="StreamingMetrics.UnmatchedCause"/>
    /// tagged with the topic prefix, and logs a Warning on the first unmatched frame for this prefix
    /// since the current connection was established. Never cross-delivered, never silent.
    /// </summary>
    private void OnUnmatchedFrame(string prefix, string topic)
    {
        _metrics.RecordDrop(prefix, StreamingMetrics.UnmatchedCause);

        bool firstForPrefix;
        lock (_unmatchedLogLock)
        {
            firstForPrefix = _loggedUnmatchedTopics.Add(prefix);
        }
        if (firstForPrefix)
        {
            LogUnmatchedFrameDropped(prefix, topic);
        }
    }

    /// <summary>
    /// Derives the bare topic prefix from a routing key: everything up to the first <c>+</c>
    /// (<c>smd+265598</c> → <c>smd</c>, <c>sor</c> → <c>sor</c>). The prefix — not the full,
    /// unbounded-cardinality routing key — tags metrics and populates connection-event topic lists.
    /// </summary>
    private static string ExtractTopicPrefix(string routingKey)
    {
        var plusIndex = routingKey.IndexOf('+');
        return plusIndex >= 0 ? routingKey[..plusIndex] : routingKey;
    }

    /// <summary>
    /// Tears down the current socket and re-establishes it, replaying active subscriptions.
    /// </summary>
    /// <param name="reason">Trigger label for telemetry.</param>
    /// <param name="observedEpoch">
    /// The connection generation the trigger observed, or <see langword="null"/> for an unconditional
    /// reconnect (session refresh — must reconnect to pick up new credentials regardless). When
    /// non-null, the cycle is skipped under the lock if the current generation has advanced past it:
    /// a stale trigger must not tear down a healthy connection established after it was raised (STR-5).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task ReconnectAsync(string reason, long? observedEpoch, CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        // Link every reconnect to _disposeCts regardless of the caller's token (notifier callbacks
        // pass external tokens): DisposeAsync's cancel then aborts an in-flight reconnect no matter
        // who triggered it, so a straggler cannot resubscribe after dispose (STR-3).
        CancellationTokenSource linkedCts;
        try
        {
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        }
        catch (ObjectDisposedException)
        {
            return; // client already disposed; _disposeCts gone
        }

        try
        {
            var linkedToken = linkedCts.Token;

            try
            {
                await _connectLock.WaitAsync(linkedToken);
            }
            catch (OperationCanceledException)
            {
                return; // disposing or caller cancelled before we acquired the lock
            }
            catch (ObjectDisposedException)
            {
                return; // semaphore disposed by DisposeAsync — the client is gone
            }

            try
            {
                // Re-check under the lock: a straggler that queued on the lock while DisposeAsync
                // held it finds the client disposed and does nothing (STR-3).
                if (_disposed)
                {
                    return;
                }

                // STR-5: skip a stale trigger under the lock. If the connection generation has
                // advanced past the one this trigger observed, a newer connect already replaced the
                // socket the trigger fired against — tearing it down would double the coverage gap and
                // emit a redundant Disconnected/Reconnected pair, driving redundant consumer
                // reconciliation exactly when the session is struggling. session_refresh passes null
                // and is never skipped (it must reconnect to pick up new credentials).
                if (observedEpoch is { } expectedEpoch && Volatile.Read(ref _connectionEpoch) != expectedEpoch)
                {
                    return;
                }

                using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.WebSocket.Reconnect");
                activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
                activity?.SetTag(LogFields.Trigger, reason);

                _reconnectCount.Add(1,
                    new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId),
                    new KeyValuePair<string, object?>(LogFields.Trigger, reason));
                LogReconnecting();

                // Mark the start of the coverage gap so consumers can begin REST reconciliation
                // immediately instead of inferring an outage from staleness (finding FIL-4).
                PublishConnectionEvent(new ConnectionDisconnected(_timeProvider.GetUtcNow(), reason));

                await DisconnectAsync();

                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(_reconnectDelayMs), _timeProvider, linkedToken);
                    await ConnectCoreAsync(linkedToken);

                    // Mark the end of the gap, listing the topics whose subscriptions were replayed.
                    PublishConnectionEvent(new ConnectionReconnected(_timeProvider.GetUtcNow(), GetReplayedTopics()));
                }
                catch (OperationCanceledException)
                {
                    // Cancelled by dispose (or the caller) mid-reconnect — not an error.
                }
                catch (Exception ex)
                {
                    LogReconnectError(ex);
                }
            }
            finally
            {
                try
                {
                    _connectLock.Release();
                }
                catch (ObjectDisposedException)
                {
                    // Semaphore disposed by a dispose that raced our critical section — safe to ignore.
                }
            }
        }
        finally
        {
            linkedCts.Dispose();
        }
    }

    private async Task ReplayActiveSubscriptionsAsync(CancellationToken cancellationToken)
    {
        string[] subscriptions;
        lock (_subscriptionLock)
        {
            subscriptions = _subscriptions.Select(s => s.SubscribeMessage).Distinct().ToArray();
        }

        foreach (var sub in subscriptions)
        {
            await SendTextAsync(sub, cancellationToken);
        }
    }

    private async Task OnSessionRefreshedAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        LogSessionRefreshed();

        // Unconditional (null epoch): a re-auth changed the session/token, so the socket must be
        // re-established regardless of its current health to reconnect with the fresh credentials.
        await ReconnectAsync("session_refresh", observedEpoch: null, cancellationToken);
    }

    private async Task OnTickleSucceededAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        // Capture the generation before the open-state check: a concurrent reconnect bumping the
        // epoch between the two would otherwise let this trigger observe the already-current
        // (post-recovery) epoch, so the STR-5 stale-trigger guard wouldn't fire and a
        // freshly-recovered connection could be torn down.
        var observedEpoch = Volatile.Read(ref _connectionEpoch);

        if (_webSocket is { State: WebSocketState.Open })
        {
            return;
        }

        LogTickleWatchdogTriggeringReconnect();
        try
        {
            await ReconnectAsync("tickle_watchdog", observedEpoch, cancellationToken);
        }
        catch (Exception ex)
        {
            LogTickleWatchdogReconnectFailed(ex);
        }
    }

    private async Task SendTextAsync(string message, CancellationToken cancellationToken)
    {
        var ws = _webSocket;
        if (ws == null || ws.State != WebSocketState.Open)
        {
            return;
        }

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            LogOutgoingMessage(message);
        }

        var bytes = Encoding.UTF8.GetBytes(message);
        await ws.SendAsync(
            bytes.AsMemory(),
            WebSocketMessageType.Text,
            true,
            cancellationToken);

        _messagesSent.Add(1,
            new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId));
    }

    private async ValueTask UnsubscribeSolicitedAsync(TopicSubscription entry, CancellationToken cancellationToken)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.WebSocket.Unsubscribe");
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
        activity?.SetTag(LogFields.Topic, entry.TopicPrefix);

        string? cancelToSend = null;
        lock (_subscriptionLock)
        {
            _subscriptions.Remove(entry);

            if (_subscribers.TryGetValue(entry.RoutingKey, out var writers))
            {
                lock (writers)
                {
                    writers.Remove(entry.Writer);
                }
            }
            entry.Writer.TryComplete();

            var stillReferenced = entry.CancelMessage is not null
                && _subscriptions.Exists(s => s.CancelMessage == entry.CancelMessage);
            if (entry.CancelMessage is not null && !stillReferenced && _webSocket?.State == WebSocketState.Open)
            {
                cancelToSend = entry.CancelMessage;
            }
        }

        _unsubscribeCount.Add(1,
            new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId),
            new KeyValuePair<string, object?>(LogFields.Topic, entry.TopicPrefix));

        if (cancelToSend is not null)
        {
            try
            {
                await SendTextAsync(cancelToSend, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogUnsubscribeSendError(ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Connecting to IBKR WebSocket")]
    private partial void LogConnecting();

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to IBKR WebSocket")]
    private partial void LogConnected();

    [LoggerMessage(Level = LogLevel.Warning, Message = "WebSocket disconnect error")]
    private partial void LogDisconnectError(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Heartbeat send failed")]
    private partial void LogHeartbeatError(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "WebSocket connection closed by server")]
    private partial void LogWebSocketClosed();

    [LoggerMessage(Level = LogLevel.Warning, Message = "WebSocket receive error")]
    private partial void LogWebSocketError(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to parse WebSocket message")]
    private partial void LogMessageParseError(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reconnecting to IBKR WebSocket")]
    private partial void LogReconnecting();

    [LoggerMessage(Level = LogLevel.Warning, Message = "WebSocket reconnect failed")]
    private partial void LogReconnectError(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Session refreshed, triggering WebSocket reconnect")]
    private partial void LogSessionRefreshed();

    [LoggerMessage(Level = LogLevel.Trace, Message = "WebSocket send: {Message}")]
    private partial void LogOutgoingMessage(string message);

    [LoggerMessage(Level = LogLevel.Trace, Message = "WebSocket receive: {Message}")]
    private partial void LogIncomingMessage(string message);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tickle watchdog detected dead WebSocket — triggering reconnect")]
    private partial void LogTickleWatchdogTriggeringReconnect();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tickle watchdog reconnect attempt failed")]
    private partial void LogTickleWatchdogReconnectFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "WebSocket unsubscribe send failed")]
    private partial void LogUnsubscribeSendError(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dropping oldest {Topic} frame under buffer overflow (a stalled consumer); further {Topic} overflow drops are counted, not logged, until reconnect")]
    private partial void LogFrameOverflowDropped(string topic);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dropping unmatched {Prefix} frame (topic {Topic}) — no live subscription for its full topic identity; further unmatched {Prefix} frames are counted, not logged, until reconnect")]
    private partial void LogUnmatchedFrameDropped(string prefix, string topic);

    private sealed record TopicSubscription(
        string RoutingKey,
        string TopicPrefix,
        string SubscribeMessage,
        string? CancelMessage,
        ChannelWriter<JsonElement> Writer);
}
