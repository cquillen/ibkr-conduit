using System.Globalization;
using IbkrConduit.Streaming;
using Spectre.Console;

namespace IbkrConduit.Examples.OrderMonitor;

/// <summary>
/// Spectre.Console table state for the trade-execution (<c>str</c>) stream. Executions
/// are an append-only log: deduped on <see cref="TradeExecution.ExecutionId"/> (IBKR
/// replays history on subscribe and after reconnect), newest-first, capped to the most
/// recent <c>capacity</c> rows. <see cref="TotalSeen"/> counts all distinct executions.
/// </summary>
internal sealed class LiveExecutionTable
{
    private const int _defaultCapacity = 15;

    private readonly int _capacity;
    private readonly object _gate = new();
    private readonly LinkedList<TradeExecution> _window = new(); // newest at front
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private int _totalSeen;

    /// <summary>Creates the table state and its Spectre.Console table.</summary>
    /// <param name="capacity">Maximum number of rows retained for display.</param>
    public LiveExecutionTable(int capacity = _defaultCapacity)
    {
        _capacity = capacity;
        Table = new Table()
            .AddColumn("[bold]Time[/]")
            .AddColumn("[bold]Symbol[/]")
            .AddColumn("[bold]Side[/]")
            .AddColumn(new TableColumn("[bold]Qty[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Price[/]").RightAligned())
            .AddColumn("[bold]Exch[/]")
            .AddColumn("[bold]OrderRef[/]")
            .Border(TableBorder.Rounded)
            .Title("[bold]Executions[/]");
    }

    /// <summary>The Spectre.Console table to render. Call <see cref="RefreshDisplay"/> first.</summary>
    public Table Table { get; }

    /// <summary>Total distinct executions observed this session (ignores capacity trimming).</summary>
    public int TotalSeen
    {
        get { lock (_gate) { return _totalSeen; } }
    }

    /// <summary>Adds an execution unless its id was already seen (dedupe).</summary>
    public void Add(TradeExecution execution)
    {
        lock (_gate)
        {
            if (!_seen.Add(execution.ExecutionId))
            {
                return;
            }

            _totalSeen++;
            _window.AddFirst(execution);
            while (_window.Count > _capacity)
            {
                _window.RemoveLast();
            }
        }
    }

    /// <summary>Point-in-time snapshot of the retained window, newest first.</summary>
    public IReadOnlyList<TradeExecution> Snapshot()
    {
        lock (_gate)
        {
            return _window.ToArray();
        }
    }

    /// <summary>Rebuilds the table rows from the current window.</summary>
    public void RefreshDisplay()
    {
        var rows = Snapshot();
        Table.Rows.Clear();
        foreach (var e in rows)
        {
            Table.AddRow(
                Markup.Escape(FormatTime(e)),
                Markup.Escape(e.Symbol),
                Markup.Escape(e.Side),
                Markup.Escape(e.Size.ToString(CultureInfo.InvariantCulture)),
                Markup.Escape(e.Price.ToString(CultureInfo.InvariantCulture)),
                Markup.Escape(e.Exchange ?? "-"),
                Markup.Escape(e.OrderRef ?? "-"));
        }
    }

    private static string FormatTime(TradeExecution e)
    {
        if (e.TradeTimeR is { } epochMs)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(epochMs)
                .ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }

        return e.TradeTime ?? "-";
    }
}
