using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

namespace IbkrConduit.Flex;

/// <summary>
/// A single trade execution parsed from a Trade Confirmations Flex query response.
/// </summary>
[ExcludeFromCodeCoverage]
public record FlexTradeConfirmation
{
    /// <summary>Account ID.</summary>
    public string AccountId { get; init; } = string.Empty;

    /// <summary>Trade currency.</summary>
    public string Currency { get; init; } = string.Empty;

    /// <summary>Asset category.</summary>
    public string AssetCategory { get; init; } = string.Empty;

    /// <summary>Asset sub-category.</summary>
    public string SubCategory { get; init; } = string.Empty;

    /// <summary>Instrument symbol.</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Instrument description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>IBKR contract ID.</summary>
    public int? Conid { get; init; }

    /// <summary>IBKR trade ID.</summary>
    public string TradeId { get; init; } = string.Empty;

    /// <summary>Order ID.</summary>
    public string OrderId { get; init; } = string.Empty;

    /// <summary>Execution ID.</summary>
    public string ExecId { get; init; } = string.Empty;

    /// <summary>Trade date.</summary>
    public DateOnly? TradeDate { get; init; }

    /// <summary>Settlement date.</summary>
    public DateOnly? SettleDate { get; init; }

    /// <summary>Report date.</summary>
    public DateOnly? ReportDate { get; init; }

    /// <summary>Order placement timestamp.</summary>
    public DateTimeOffset? OrderTime { get; init; }

    /// <summary>Execution timestamp.</summary>
    public DateTimeOffset? DateTime { get; init; }

    /// <summary>Execution venue.</summary>
    public string Exchange { get; init; } = string.Empty;

    /// <summary>Buy/Sell side.</summary>
    public string BuySell { get; init; } = string.Empty;

    /// <summary>Executed quantity.</summary>
    public decimal Quantity { get; init; }

    /// <summary>Execution price.</summary>
    public decimal Price { get; init; }

    /// <summary>Trade amount.</summary>
    public decimal Amount { get; init; }

    /// <summary>Trade proceeds.</summary>
    public decimal Proceeds { get; init; }

    /// <summary>Net cash impact.</summary>
    public decimal NetCash { get; init; }

    /// <summary>Commission amount.</summary>
    public decimal Commission { get; init; }

    /// <summary>Commission currency.</summary>
    public string CommissionCurrency { get; init; } = string.Empty;

    /// <summary>Order type.</summary>
    public string OrderType { get; init; } = string.Empty;

    /// <summary>Level of detail (e.g. "EXECUTION").</summary>
    public string LevelOfDetail { get; init; } = string.Empty;

    /// <summary>Raw XML element for access to attributes not surfaced on this DTO.</summary>
    public XElement? RawElement { get; init; }
}
