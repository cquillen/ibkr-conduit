using IbkrConduit.Client;
using IbkrConduit.MarketData;
using IbkrConduit.Streaming;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace IbkrConduit.Examples.MarketDataStream;

/// <summary>
/// Coordinates the streaming subscriptions and the Spectre.Console Live render loop.
/// </summary>
internal static class StreamHost
{
    // CA1848: static class cannot host [LoggerMessage] partial methods (no instance logger field).
    // LoggerMessage.Define satisfies the analyzer while keeping the delegates at class scope.
    private static readonly Action<ILogger, string, int, string, Exception?> _logSubscriptionFailed =
        LoggerMessage.Define<string, int, string>(
            LogLevel.Warning,
            new EventId(1, nameof(RunAsync)),
            "Subscription failed for {Symbol} (conid {Conid}): {Message}. Continuing with the remaining symbols.");

    private static readonly Action<ILogger, string, string, Exception?> _logStreamError =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(2, nameof(RunAsync)),
            "Market data stream error for {Symbol}: {Message}");

    private static readonly Action<ILogger, string, Exception?> _logSubscriptionDisposeFailed =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(3, nameof(RunAsync)),
            "Subscription disposal threw on shutdown: {Message}");

    private static readonly string[] _fields =
    {
        MarketDataFields.LastPrice,
        MarketDataFields.BidPrice,
        MarketDataFields.AskPrice,
        MarketDataFields.Volume,
        MarketDataFields.ChangePercent,
    };

    /// <summary>
    /// Resolves symbols, subscribes to each, renders the live table until cancelled,
    /// and disposes subscriptions on shutdown. Returns the total number of ticks observed.
    /// </summary>
    public static async Task<int> RunAsync(
        IIbkrClient client,
        IReadOnlyList<string> symbols,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var resolved = await SymbolResolver.ResolveAsync(client, symbols, logger, cancellationToken);
        var table = new LiveTickTable(resolved);
        var totalTicks = 0;

        var subscriptions = new List<IDisposable>(resolved.Count);
        try
        {
            foreach (var entry in resolved)
            {
                try
                {
                    var observable = await client.Streaming.MarketDataAsync(
                        entry.Conid, _fields, cancellationToken);

                    subscriptions.Add(observable.Subscribe(new ActionObserver<MarketDataTick>(
                        tick =>
                        {
                            table.UpdateTick(tick);
                            Interlocked.Increment(ref totalTicks);
                        },
                        logger,
                        entry.Symbol)));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logSubscriptionFailed(logger, entry.Symbol, entry.Conid, ex.Message, ex);
                }
            }

            if (subscriptions.Count == 0)
            {
                throw new InvalidOperationException(
                    "No subscriptions succeeded; nothing to render.");
            }

            await RenderLoopAsync(table, cancellationToken);
        }
        finally
        {
            foreach (var sub in subscriptions)
            {
                try
                {
                    sub.Dispose();
                }
                catch (Exception ex)
                {
                    _logSubscriptionDisposeFailed(logger, ex.Message, ex);
                }
            }
        }

        return totalTicks;
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

        public void OnError(Exception error) =>
            _logStreamError(_logger, _label, error.Message, error);

        public void OnCompleted() { /* end-of-stream is normal; no-op */ }
    }

    /// <summary>
    /// Spectre.Console Live render loop. Refreshes the table and the status header
    /// every 250ms until <paramref name="ct"/> is cancelled.
    /// </summary>
    private static async Task RenderLoopAsync(LiveTickTable table, CancellationToken ct)
    {
        var layout = new Rows(
            new Markup("[grey]● initializing…[/]"),
            table.Table);

        await AnsiConsole.Live(layout)
            .StartAsync(async ctx =>
            {
                while (!ct.IsCancellationRequested)
                {
                    var now = TimeProvider.System.GetUtcNow();
                    table.RefreshDisplay(now);

                    var status = table.AnyTickWithin(TimeSpan.FromSeconds(30), now)
                        ? "[green]● Live[/]"
                        : "[yellow]● Stale[/]";

                    var refreshed = new Rows(new Markup(status), table.Table);
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
}
