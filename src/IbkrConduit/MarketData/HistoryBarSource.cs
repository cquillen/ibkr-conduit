using System.Runtime.Serialization;

namespace IbkrConduit.MarketData;

/// <summary>
/// Source of data for historical bars.
/// </summary>
public enum HistoryBarSource
{
    /// <summary>OHLC of the last trade values.</summary>
    [EnumMember(Value = "Last")]
    Last,

    /// <summary>OHLC of the bid/ask midpoint.</summary>
    [EnumMember(Value = "Midpoint")]
    Midpoint,

    /// <summary>OHLC bid/ask values.</summary>
    [EnumMember(Value = "Bid_Ask")]
    BidAsk,
}
