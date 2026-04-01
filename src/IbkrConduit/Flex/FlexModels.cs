using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

namespace IbkrConduit.Flex;

/// <summary>
/// Represents a trade record parsed from a Flex Query response.
/// </summary>
[ExcludeFromCodeCoverage]
public record FlexTrade
{
    /// <summary>The account ID associated with the trade.</summary>
    public string AccountId { get; init; } = string.Empty;

    /// <summary>The traded symbol.</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>The IBKR contract ID, if present.</summary>
    public int? Conid { get; init; }

    /// <summary>A description of the traded instrument.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>The side of the trade (BUY or SELL).</summary>
    public string Side { get; init; } = string.Empty;

    /// <summary>The quantity traded.</summary>
    public decimal Quantity { get; init; }

    /// <summary>The trade price.</summary>
    public decimal Price { get; init; }

    /// <summary>The total proceeds from the trade.</summary>
    public decimal Proceeds { get; init; }

    /// <summary>The commission charged for the trade.</summary>
    public decimal Commission { get; init; }

    /// <summary>The currency of the trade.</summary>
    public string Currency { get; init; } = string.Empty;

    /// <summary>The trade date.</summary>
    public string TradeDate { get; init; } = string.Empty;

    /// <summary>The trade time.</summary>
    public string TradeTime { get; init; } = string.Empty;

    /// <summary>The order type used for the trade.</summary>
    public string OrderType { get; init; } = string.Empty;

    /// <summary>The exchange where the trade was executed.</summary>
    public string Exchange { get; init; } = string.Empty;

    /// <summary>The order ID associated with the trade.</summary>
    public string OrderId { get; init; } = string.Empty;

    /// <summary>The execution ID for the trade.</summary>
    public string ExecId { get; init; } = string.Empty;

    /// <summary>Raw XML element for accessing any additional attributes.</summary>
    public XElement? RawElement { get; init; }
}

/// <summary>
/// Represents an open position record parsed from a Flex Query response.
/// </summary>
[ExcludeFromCodeCoverage]
public record FlexPosition
{
    /// <summary>The account ID associated with the position.</summary>
    public string AccountId { get; init; } = string.Empty;

    /// <summary>The symbol of the position.</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>The IBKR contract ID, if present.</summary>
    public int? Conid { get; init; }

    /// <summary>A description of the held instrument.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>The position size (positive for long, negative for short).</summary>
    public decimal Position { get; init; }

    /// <summary>The current mark price.</summary>
    public decimal MarkPrice { get; init; }

    /// <summary>The total position value at mark.</summary>
    public decimal PositionValue { get; init; }

    /// <summary>The cost basis of the position.</summary>
    public decimal CostBasis { get; init; }

    /// <summary>The unrealized profit and loss.</summary>
    public decimal UnrealizedPnl { get; init; }

    /// <summary>The currency of the position.</summary>
    public string Currency { get; init; } = string.Empty;

    /// <summary>The asset class of the instrument.</summary>
    public string AssetClass { get; init; } = string.Empty;

    /// <summary>Raw XML element for accessing any additional attributes.</summary>
    public XElement? RawElement { get; init; }
}
