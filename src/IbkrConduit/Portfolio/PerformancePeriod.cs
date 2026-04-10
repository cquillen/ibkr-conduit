using System.Runtime.Serialization;

namespace IbkrConduit.Portfolio;

/// <summary>
/// Time period for portfolio performance queries.
/// </summary>
public enum PerformancePeriod
{
    /// <summary>One day.</summary>
    [EnumMember(Value = "1D")]
    OneDay,

    /// <summary>One week.</summary>
    [EnumMember(Value = "1W")]
    OneWeek,

    /// <summary>One month.</summary>
    [EnumMember(Value = "1M")]
    OneMonth,

    /// <summary>Three months.</summary>
    [EnumMember(Value = "3M")]
    ThreeMonths,

    /// <summary>Six months.</summary>
    [EnumMember(Value = "6M")]
    SixMonths,

    /// <summary>One year.</summary>
    [EnumMember(Value = "1Y")]
    OneYear,

    /// <summary>Month to date.</summary>
    [EnumMember(Value = "MTD")]
    MonthToDate,

    /// <summary>Year to date.</summary>
    [EnumMember(Value = "YTD")]
    YearToDate,
}
