using System.Globalization;
using IbkrConduit.Orders;
using IbkrConduit.Streaming;
using Spectre.Console;

namespace IbkrConduit.Examples.OrderMonitor;

/// <summary>
/// Spectre.Console table state for the order-status (<c>sor</c>) stream. Orders are an
/// update-in-place set keyed by the IBKR order id: a new order inserts a row; subsequent
/// updates for the same id merge into it. Rows are populated two ways — <see cref="Seed"/>
/// loads existing orders from the REST snapshot on startup (the <c>sor</c> stream's initial
/// frames are id-only), and <see cref="Upsert"/> applies live <c>sor</c> updates. Because
/// <c>sor</c> deltas are sparse — a later frame carries only the id plus whatever changed —
/// the merge only overwrites a field when the incoming value carries real data (non-empty
/// string, non-zero quantity, non-null price), so a sparse frame never blanks a column an
/// earlier frame (or the seed) populated. Rows render sorted by OrderId.
/// </summary>
internal sealed class LiveOrderTable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, RowState> _rows = new(StringComparer.Ordinal);

    /// <summary>Creates the table state and its Spectre.Console table.</summary>
    public LiveOrderTable()
    {
        Table = new Table()
            .AddColumn("[bold]Order[/]")
            .AddColumn("[bold]Symbol[/]")
            .AddColumn("[bold]Side[/]")
            .AddColumn(new TableColumn("[bold]Qty[/]").RightAligned())
            .AddColumn("[bold]Type[/]")
            .AddColumn(new TableColumn("[bold]Price[/]").RightAligned())
            .AddColumn("[bold]Status[/]")
            .AddColumn(new TableColumn("[bold]Filled[/]").RightAligned())
            .AddColumn("[bold]OrderRef[/]")
            .AddColumn(new TableColumn("[bold]Age[/]").RightAligned())
            .Border(TableBorder.Rounded)
            .Title("[bold]Orders[/]");
    }

    /// <summary>The Spectre.Console table to render. Call <see cref="RefreshDisplay"/> first.</summary>
    public Table Table { get; }

    /// <summary>Number of distinct orders currently tracked.</summary>
    public int Count
    {
        get { lock (_gate) { return _rows.Count; } }
    }

    /// <summary>Inserts a new order row or merges the update into the existing row.</summary>
    public void Upsert(OrderUpdate update)
    {
        lock (_gate)
        {
            if (!_rows.TryGetValue(update.OrderId, out var row))
            {
                row = new RowState { OrderId = update.OrderId };
                _rows[update.OrderId] = row;
            }

            // sor status deltas are sparse: a later frame carries only the id plus whatever
            // changed, so every other field arrives at its default (empty string / 0 / null).
            // Merge rather than last-write-wins so a sparse delta never blanks a column an
            // earlier, fuller frame populated — only overwrite when the incoming value carries
            // real data.
            if (!string.IsNullOrEmpty(update.Symbol))
            {
                row.Symbol = update.Symbol;
            }

            if (!string.IsNullOrEmpty(update.Side))
            {
                row.Side = update.Side;
            }

            if (update.Size != 0)
            {
                row.Qty = update.Size;
            }

            if (!string.IsNullOrEmpty(update.OrderType))
            {
                row.Type = update.OrderType;
            }

            if (update.Price is not null)
            {
                row.Price = update.Price;
            }

            if (!string.IsNullOrEmpty(update.Status))
            {
                row.Status = update.Status;
            }

            if (update.FilledQuantity != 0)
            {
                row.Filled = update.FilledQuantity;
            }

            if (!string.IsNullOrEmpty(update.OrderRef))
            {
                row.OrderRef = update.OrderRef;
            }

            row.LastUpdateAt = TimeProvider.System.GetUtcNow();
        }
    }

    /// <summary>
    /// Seeds a row from a REST <see cref="LiveOrder"/> snapshot
    /// (<c>GetLiveOrdersAsync</c>). The <c>sor</c> stream's initial frames are id-only, so
    /// existing orders would otherwise render blank until they next change; seeding from
    /// REST populates them up front, and later sparse <c>sor</c> frames merge in via
    /// <see cref="Upsert"/> without blanking these columns. Keyed by the IBKR order id, so
    /// the seeded row and its subsequent <c>sor</c> updates share one row.
    /// </summary>
    public void Seed(LiveOrder order)
    {
        lock (_gate)
        {
            var id = order.OrderId.ToString(CultureInfo.InvariantCulture);
            if (!_rows.TryGetValue(id, out var row))
            {
                row = new RowState { OrderId = id };
                _rows[id] = row;
            }

            // The REST snapshot is the authoritative full picture of the order.
            row.Symbol = order.Ticker ?? string.Empty;
            row.Side = order.Side;
            row.Qty = order.TotalSize;
            row.Type = order.OrderType ?? string.Empty;
            row.Price = order.Price; // null for market orders
            row.Status = order.Status;
            row.Filled = order.FilledQuantity;
            if (!string.IsNullOrEmpty(order.OrderRef))
            {
                row.OrderRef = order.OrderRef;
            }

            row.LastUpdateAt = TimeProvider.System.GetUtcNow();
        }
    }

    /// <summary>Point-in-time snapshot of tracked orders, sorted by OrderId.</summary>
    public IReadOnlyList<OrderRow> Snapshot()
    {
        lock (_gate)
        {
            return _rows.Values
                .OrderBy(r => r.OrderId, StringComparer.Ordinal)
                .Select(r => new OrderRow(
                    r.OrderId, r.Symbol, r.Side, r.Qty, r.Type, r.Price,
                    r.Status, r.Filled, r.OrderRef, r.LastUpdateAt))
                .ToArray();
        }
    }

    /// <summary>Rebuilds the table rows from current state, coloring Age by staleness.</summary>
    public void RefreshDisplay(DateTimeOffset now)
    {
        var rows = Snapshot();
        Table.Rows.Clear();
        foreach (var r in rows)
        {
            Table.AddRow(
                new Markup(Markup.Escape(r.OrderId)),
                new Markup(Markup.Escape(r.Symbol)),
                new Markup(Markup.Escape(r.Side)),
                new Markup(Markup.Escape(r.Qty.ToString(CultureInfo.InvariantCulture))),
                new Markup(Markup.Escape(r.Type)),
                new Markup(Markup.Escape(r.Price?.ToString(CultureInfo.InvariantCulture) ?? "-")),
                new Markup(Markup.Escape(r.Status)),
                new Markup(Markup.Escape(r.Filled.ToString(CultureInfo.InvariantCulture))),
                new Markup(Markup.Escape(r.OrderRef ?? "-")),
                FormatAge(r.LastUpdateAt, now));
        }
    }

    private static Markup FormatAge(DateTimeOffset? lastUpdateAt, DateTimeOffset now)
    {
        if (lastUpdateAt is null)
        {
            return new Markup("-");
        }

        var age = now - lastUpdateAt.Value;
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

    /// <summary>Immutable view of a tracked order for rendering and testing.</summary>
    internal sealed record OrderRow(
        string OrderId, string Symbol, string Side, decimal Qty, string Type,
        decimal? Price, string Status, decimal Filled, string? OrderRef,
        DateTimeOffset? LastUpdateAt);

    private sealed class RowState
    {
        public string OrderId { get; init; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public string Type { get; set; } = string.Empty;
        public decimal? Price { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal Filled { get; set; }
        public string? OrderRef { get; set; }
        public DateTimeOffset? LastUpdateAt { get; set; }
    }
}
