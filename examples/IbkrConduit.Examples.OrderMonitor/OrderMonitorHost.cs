using IbkrConduit.Client;
using IbkrConduit.Streaming;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace IbkrConduit.Examples.OrderMonitor;

/// <summary>
/// Coordinates the order-status and trade-execution subscriptions and the
/// Spectre.Console Live render loop.
/// </summary>
internal static class OrderMonitorHost
{
    private static readonly Action<ILogger, string, string, Exception?> _logSubscriptionFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(1, nameof(RunAsync)),
            "Subscription failed for {Stream}: {Message}. Continuing with the remaining stream.");

    private static readonly Action<ILogger, string, string, Exception?> _logStreamError =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(2, nameof(RunAsync)),
            "{Stream} stream error: {Message}");

    private static readonly Action<ILogger, string, Exception?> _logDisposeFailed =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(3, nameof(RunAsync)),
            "Subscription disposal threw on shutdown: {Message}");

    private static readonly Action<ILogger, int, Exception?> _logSeeded =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(4, nameof(RunAsync)),
            "Seeded {Count} existing order(s) from the REST snapshot.");

    private static readonly Action<ILogger, string, Exception?> _logSeedFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(5, nameof(RunAsync)),
            "Order snapshot seed failed: {Message}. Existing orders will populate as they next update.");

    /// <summary>
    /// Subscribes to both streams, connects, renders until cancelled, and disposes.
    /// </summary>
    public static async Task RunAsync(
        IIbkrClient client,
        bool realtimeOnly,
        int days,
        ILogger logger,
        PanelLogBuffer panelBuffer,
        CancellationToken cancellationToken)
    {
        var orderTable = new LiveOrderTable();
        var execTable = new LiveExecutionTable();

        var subscriptions = new List<IDisposable>(2);
        var handles = new List<IAsyncDisposable>(2);
        try
        {
            // The sor stream's initial frames are id-only, so existing orders would render
            // blank until they next change. Seed them from the REST snapshot first (this also
            // primes the brokerage session). Skipped for realtime-only, which shows only
            // post-launch activity.
            if (!realtimeOnly)
            {
                await SeedOrdersAsync(client, orderTable, logger, cancellationToken);
            }

            // Orders: pass days for history unless realtime-only was requested.
            try
            {
                var orders = await client.Streaming.OrderUpdatesAsync(
                    realtimeOnly ? null : days, cancellationToken);
                handles.Add(orders);
                subscriptions.Add(orders.Stream.Subscribe(
                    new ActionObserver<OrderUpdate>(orderTable.Upsert, logger, "orders")));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logSubscriptionFailed(logger, "orders", ex.Message, ex);
            }

            try
            {
                var execs = await client.Streaming.TradeExecutionsAsync(
                    realtimeOnly ? true : null, realtimeOnly ? null : days, cancellationToken);
                handles.Add(execs);
                subscriptions.Add(execs.Stream.Subscribe(
                    new ActionObserver<TradeExecution>(execTable.Add, logger, "executions")));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logSubscriptionFailed(logger, "executions", ex.Message, ex);
            }

            if (subscriptions.Count == 0)
            {
                throw new InvalidOperationException(
                    "No subscriptions succeeded; nothing to render.");
            }

            await client.Streaming.ConnectAsync(cancellationToken);

            await RenderLoopAsync(orderTable, execTable, client.Streaming, panelBuffer, cancellationToken);
        }
        finally
        {
            foreach (var sub in subscriptions)
            {
                try { sub.Dispose(); }
                catch (Exception ex) { _logDisposeFailed(logger, ex.Message, ex); }
            }

            foreach (var handle in handles)
            {
                try { await handle.DisposeAsync(); }
                catch (Exception ex) { _logDisposeFailed(logger, ex.Message, ex); }
            }
        }
    }

    /// <summary>
    /// Seeds the Orders table from the REST order snapshot. IBKR's
    /// <c>/iserver/account/orders</c> primes on the first call and returns the full set on
    /// the next, so it is called twice and the seed only trusts a primed snapshot
    /// (<c>IsSnapshot == true</c>, design doc §10.6). Failures are logged and swallowed — seeding
    /// is a best-effort convenience and the live streams still run without it.
    /// </summary>
    private static async Task SeedOrdersAsync(
        IIbkrClient client, LiveOrderTable orderTable, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            await client.Orders.GetLiveOrdersAsync(cancellationToken: cancellationToken);
            var result = await client.Orders.GetLiveOrdersAsync(cancellationToken: cancellationToken);
            if (!result.IsSuccess)
            {
                _logSeedFailed(logger, result.Error.Message ?? "unknown error", null);
                return;
            }

            if (!result.Value.IsSnapshot)
            {
                // Unprimed read — an empty list here is not authoritative; skip rather than seed a
                // false "no orders" state. The live sor stream will still populate the table.
                _logSeedFailed(logger, "live-orders read was not primed (IsSnapshot=false)", null);
                return;
            }

            foreach (var order in result.Value.Orders)
            {
                orderTable.Seed(order);
            }

            _logSeeded(logger, result.Value.Orders.Count, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logSeedFailed(logger, ex.Message, ex);
        }
    }

    /// <summary>
    /// Adapts an <see cref="Action{T}"/> to the <see cref="IObserver{T}"/> interface.
    /// Stream errors are logged at <see cref="LogLevel.Warning"/> using the supplied logger
    /// and label. End-of-stream completions are a no-op.
    /// </summary>
    private sealed class ActionObserver<T> : IObserver<T>
    {
        private readonly Action<T> _onNext;
        private readonly ILogger _logger;
        private readonly string _label;

        public ActionObserver(Action<T> onNext, ILogger logger, string label)
        {
            _onNext = onNext;
            _logger = logger;
            _label = label;
        }

        public void OnNext(T value) => _onNext(value);

        public void OnError(Exception error) => _logStreamError(_logger, _label, error.Message, error);

        public void OnCompleted() { /* end-of-stream is normal; no-op */ }
    }

    /// <summary>
    /// Spectre.Console Live render loop. Refreshes the Orders table, the Executions table,
    /// the status header, and the Logs panel every 250ms until <paramref name="ct"/> is
    /// cancelled. The header reflects the real WebSocket connection state from
    /// <see cref="IStreamingOperations.IsConnected"/> and the freshness of the last
    /// received message from <see cref="IStreamingOperations.LastMessageReceivedAt"/>.
    /// The Logs panel snapshots <paramref name="panelBuffer"/> on every tick.
    /// </summary>
    private static async Task RenderLoopAsync(
        LiveOrderTable orderTable,
        LiveExecutionTable execTable,
        IStreamingOperations streaming,
        PanelLogBuffer panelBuffer,
        CancellationToken ct)
    {
        var initial = new Rows(
            new Markup("[grey]● initializing…[/]"),
            orderTable.Table,
            execTable.Table,
            BuildLogPanel(panelBuffer));

        await AnsiConsole.Live(initial)
            .StartAsync(async ctx =>
            {
                while (!ct.IsCancellationRequested)
                {
                    var now = TimeProvider.System.GetUtcNow();
                    orderTable.RefreshDisplay(now);
                    execTable.RefreshDisplay();

                    var refreshed = new Rows(
                        BuildStatus(streaming, orderTable, execTable, now),
                        orderTable.Table,
                        execTable.Table,
                        BuildLogPanel(panelBuffer));
                    ctx.UpdateTarget(refreshed);
                    ctx.Refresh();

                    try
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            });
    }

    /// <summary>
    /// Builds the status header reflecting connection state, message freshness, and the
    /// current order/execution counts from the two tables.
    /// </summary>
    private static Markup BuildStatus(
        IStreamingOperations streaming, LiveOrderTable orders, LiveExecutionTable execs, DateTimeOffset now)
    {
        var connection = streaming.IsConnected
            ? "[green]● Connected[/]"
            : "[red]● Disconnected[/]";

        var lastMsg = streaming.LastMessageReceivedAt;
        var freshness = lastMsg is null
            ? "(no messages yet)"
            : $"(last msg {(int)(now - lastMsg.Value).TotalSeconds}s ago)";

        return new Markup($"{connection} {freshness}  [grey]{orders.Count} orders · {execs.TotalSeen} executions[/]");
    }

    /// <summary>
    /// Builds the Logs panel for the current tick. Always renders exactly 8 rows
    /// (padded with blank rows when fewer entries exist) so the layout does not
    /// shift when the first warning arrives. Each entry is a single line:
    /// <c>HH:mm:ss [level] {message}</c>, truncated with an ellipsis if it
    /// overflows the rendered width.
    /// </summary>
    private static Panel BuildLogPanel(PanelLogBuffer buffer)
    {
        var entries = buffer.Snapshot();
        var lines = new List<IRenderable>(8);

        foreach (var entry in entries)
        {
            var timestamp = entry.Timestamp.ToLocalTime().ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            var levelToken = entry.Level switch
            {
                LogLevel.Critical or LogLevel.Error => "[red]error[/]",
                LogLevel.Warning => "[yellow]warn[/]",
                LogLevel.Information => "[grey]info[/]",
                LogLevel.Debug or LogLevel.Trace => "[dim]debug[/]",
                _ => "[dim]?[/]",
            };
            var msg = Markup.Escape(entry.Message);
            lines.Add(new Markup($"{timestamp} {levelToken} {msg}").Overflow(Overflow.Ellipsis));
        }

        while (lines.Count < 8)
        {
            lines.Add(new Markup(string.Empty));
        }

        return new Panel(new Rows(lines)).Header("Logs").Expand();
    }
}
