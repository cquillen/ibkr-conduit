namespace IbkrConduit.MarketData;

/// <summary>
/// Represents a time period for historical data requests (e.g., "6d", "1w", "3m").
/// Combines an integer value with a <see cref="PeriodUnit"/> and serializes to the
/// IBKR wire format via <see cref="ToString"/>.
/// </summary>
/// <remarks>
/// Valid units for periods are: Minutes, Hours, Days, Weeks, Months, Years.
/// Seconds are NOT supported for periods — use <see cref="BarSize"/> for sub-minute granularity.
/// </remarks>
/// <param name="Value">The numeric duration (e.g., 6 for "6 days").</param>
/// <param name="Unit">The time unit.</param>
public readonly record struct HistoryPeriod(int Value, PeriodUnit Unit)
{
    /// <summary>Serializes to IBKR wire format (e.g., "6d", "1w", "3m").</summary>
    public override string ToString() => $"{Value}{Unit switch
    {
        PeriodUnit.Minutes => "min",
        PeriodUnit.Hours => "h",
        PeriodUnit.Days => "d",
        PeriodUnit.Weeks => "w",
        PeriodUnit.Months => "m",
        PeriodUnit.Years => "y",
        _ => throw new ArgumentOutOfRangeException(nameof(Unit)),
    }}";

    /// <summary>Creates a period of N minutes.</summary>
    public static HistoryPeriod Minutes(int n) => new(n, PeriodUnit.Minutes);

    /// <summary>Creates a period of N hours.</summary>
    public static HistoryPeriod Hours(int n) => new(n, PeriodUnit.Hours);

    /// <summary>Creates a period of N days.</summary>
    public static HistoryPeriod Days(int n) => new(n, PeriodUnit.Days);

    /// <summary>Creates a period of N weeks.</summary>
    public static HistoryPeriod Weeks(int n) => new(n, PeriodUnit.Weeks);

    /// <summary>Creates a period of N months.</summary>
    public static HistoryPeriod Months(int n) => new(n, PeriodUnit.Months);

    /// <summary>Creates a period of N years.</summary>
    public static HistoryPeriod Years(int n) => new(n, PeriodUnit.Years);
}

/// <summary>
/// Time units valid for <see cref="HistoryPeriod"/>.
/// Does NOT include Seconds — use <see cref="BarSize"/> for sub-minute granularity.
/// </summary>
public enum PeriodUnit
{
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

    /// <summary>Years ("y").</summary>
    Years,
}
