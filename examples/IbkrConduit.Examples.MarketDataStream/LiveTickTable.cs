using System.Collections.Concurrent;
using System.Globalization;
using IbkrConduit.MarketData;
using IbkrConduit.Streaming;
using Spectre.Console;

namespace IbkrConduit.Examples.MarketDataStream;

/// <summary>
/// Spectre.Console table state for live market-data ticks. One row per
/// resolved symbol, indexed by conid. Tick updates merge field changes
/// into per-row state; the table is rendered by <see cref="StreamHost"/>.
/// </summary>
internal sealed class LiveTickTable
{
    private readonly ConcurrentDictionary<int, RowState> _rows = new();

    /// <summary>
    /// Initializes a new <see cref="LiveTickTable"/> with the given resolved symbols.
    /// </summary>
    public LiveTickTable(IReadOnlyList<SymbolResolver.ResolvedSymbol> symbols)
    {
        Table = new Table()
            .AddColumn("[bold]Symbol[/]")
            .AddColumn(new TableColumn("[bold]Last[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Bid[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Ask[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Volume[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]% Chg[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Age[/]").RightAligned())
            .Border(TableBorder.Rounded);

        foreach (var symbol in symbols.OrderBy(s => s.Conid))
        {
            _rows[symbol.Conid] = new RowState(symbol);
            Table.AddRow(symbol.Symbol, "-", "-", "-", "-", "-", "-");
        }
    }

    /// <summary>The Spectre.Console table to render. Call <see cref="RefreshDisplay"/> before reading.</summary>
    public Table Table { get; }

    /// <summary>Merges fields from a tick into the row's state.</summary>
    public void UpdateTick(MarketDataTick tick)
    {
        if (!_rows.TryGetValue(tick.Conid, out var row))
        {
            return;
        }

        if (tick.Fields is null)
        {
            return;
        }

        if (tick.Fields.TryGetValue(MarketDataFields.LastPrice, out var last)) { row.Last = last; }
        if (tick.Fields.TryGetValue(MarketDataFields.BidPrice, out var bid)) { row.Bid = bid; }
        if (tick.Fields.TryGetValue(MarketDataFields.AskPrice, out var ask)) { row.Ask = ask; }
        if (tick.Fields.TryGetValue(MarketDataFields.Volume, out var vol)) { row.Volume = vol; }
        if (tick.Fields.TryGetValue(MarketDataFields.ChangePercent, out var chg)) { row.PercentChange = chg; }

        row.LastTickAt = TimeProvider.System.GetUtcNow();
    }

    /// <summary>Re-renders all table cells from current row state. Call from the render-loop tick.</summary>
    public void RefreshDisplay(DateTimeOffset now)
    {
        var i = 0;
        foreach (var conid in _rows.Keys.OrderBy(k => k))
        {
            var row = _rows[conid];
            Table.UpdateCell(i, 0, new Markup(Markup.Escape(row.Symbol.Symbol)));
            Table.UpdateCell(i, 1, new Markup(Markup.Escape(row.Last ?? "-")));
            Table.UpdateCell(i, 2, new Markup(Markup.Escape(row.Bid ?? "-")));
            Table.UpdateCell(i, 3, new Markup(Markup.Escape(row.Ask ?? "-")));
            Table.UpdateCell(i, 4, new Markup(Markup.Escape(row.Volume ?? "-")));
            Table.UpdateCell(i, 5, FormatPercentChange(row.PercentChange));
            Table.UpdateCell(i, 6, FormatAge(row.LastTickAt, now));
            i++;
        }
    }

    /// <summary>Whether any row received a tick within <paramref name="window"/>.</summary>
    public bool AnyTickWithin(TimeSpan window, DateTimeOffset now)
    {
        foreach (var row in _rows.Values)
        {
            if (row.LastTickAt is { } when_ && now - when_ <= window)
            {
                return true;
            }
        }

        return false;
    }

    private static Markup FormatPercentChange(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return new Markup("-");
        }

        var trimmed = value.Trim();
        var color = trimmed.StartsWith('-') ? "red" : "green";
        return new Markup($"[{color}]{Markup.Escape(trimmed)}[/]");
    }

    private static Markup FormatAge(DateTimeOffset? lastTickAt, DateTimeOffset now)
    {
        if (lastTickAt is null)
        {
            return new Markup("-");
        }

        var age = now - lastTickAt.Value;
        var seconds = (int)age.TotalSeconds;
        var text = seconds < 60
            ? $"{seconds}s"
            : age.ToString(@"m\m\ s\s", CultureInfo.InvariantCulture);

        var color = age > TimeSpan.FromSeconds(30) ? "red"
            : age > TimeSpan.FromSeconds(5) ? "yellow"
            : "default";

        return color == "default"
            ? new Markup(Markup.Escape(text))
            : new Markup($"[{color}]{Markup.Escape(text)}[/]");
    }

    private sealed class RowState
    {
        public RowState(SymbolResolver.ResolvedSymbol symbol)
        {
            Symbol = symbol;
        }

        public SymbolResolver.ResolvedSymbol Symbol { get; }
        public string? Last { get; set; }
        public string? Bid { get; set; }
        public string? Ask { get; set; }
        public string? Volume { get; set; }
        public string? PercentChange { get; set; }
        // Note: LastTickAt is read by RefreshDisplay (250ms render thread) and written by
        // UpdateTick (subscriber thread). DateTimeOffset? is not atomic on most platforms;
        // a torn read could show a momentary stale age. Acceptable for a UI demo.
        public DateTimeOffset? LastTickAt { get; set; }
    }
}
