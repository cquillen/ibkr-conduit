namespace IbkrConduit.MarketData;

/// <summary>
/// Represents a bar width for historical data requests (e.g., "5min", "1h", "1d").
/// Combines an integer value with a <see cref="BarUnit"/> and serializes to the
/// IBKR wire format via <see cref="ToString"/>.
/// </summary>
/// <remarks>
/// Valid units for bars are: Seconds, Minutes, Hours, Days, Weeks, Months.
/// Years are NOT supported for bar sizes — use <see cref="HistoryPeriod"/> for multi-year spans.
/// </remarks>
/// <param name="Value">The numeric width (e.g., 5 for "5 minutes").</param>
/// <param name="Unit">The time unit.</param>
public readonly record struct BarSize(int Value, BarUnit Unit)
{
    /// <summary>Serializes to IBKR wire format (e.g., "5min", "1h", "1d").</summary>
    public override string ToString() => $"{Value}{Unit switch
    {
        BarUnit.Seconds => "S",
        BarUnit.Minutes => "min",
        BarUnit.Hours => "h",
        BarUnit.Days => "d",
        BarUnit.Weeks => "w",
        BarUnit.Months => "m",
        _ => throw new ArgumentOutOfRangeException(nameof(Unit)),
    }}";

    /// <summary>Creates a bar size of N seconds.</summary>
    public static BarSize Seconds(int n) => new(n, BarUnit.Seconds);

    /// <summary>Creates a bar size of N minutes.</summary>
    public static BarSize Minutes(int n) => new(n, BarUnit.Minutes);

    /// <summary>Creates a bar size of N hours.</summary>
    public static BarSize Hours(int n) => new(n, BarUnit.Hours);

    /// <summary>Creates a bar size of N days.</summary>
    public static BarSize Days(int n) => new(n, BarUnit.Days);

    /// <summary>Creates a bar size of N weeks.</summary>
    public static BarSize Weeks(int n) => new(n, BarUnit.Weeks);

    /// <summary>Creates a bar size of N months.</summary>
    public static BarSize Months(int n) => new(n, BarUnit.Months);
}

/// <summary>
/// Time units valid for <see cref="BarSize"/>.
/// Does NOT include Years — use <see cref="HistoryPeriod"/> for multi-year spans.
/// </summary>
public enum BarUnit
{
    /// <summary>Seconds ("S"). Only valid for bar sizes, not periods.</summary>
    Seconds,

    /// <summary>Minutes ("min").</summary>
    Minutes,

    /// <summary>Hours ("h").</summary>
    Hours,

    /// <summary>Days ("d").</summary>
    Days,

    /// <summary>Weeks ("w").</summary>
    Weeks,

    /// <summary>Months ("m").</summary>
    Months,
}
