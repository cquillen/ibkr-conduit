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
