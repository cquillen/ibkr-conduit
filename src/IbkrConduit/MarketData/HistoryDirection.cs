using System.Runtime.Serialization;

namespace IbkrConduit.MarketData;

/// <summary>
/// Direction for historical data relative to the start time.
/// </summary>
public enum HistoryDirection
{
    /// <summary>Data begins away from start time, ending at start time or current time.</summary>
    [EnumMember(Value = "-1")]
    Backward = -1,

    /// <summary>Data begins at start time, extending forward.</summary>
    [EnumMember(Value = "1")]
    Forward = 1,
}
