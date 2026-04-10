using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

namespace IbkrConduit.Flex;

/// <summary>
/// A symbol-level aggregate row from a Trade Confirmations Flex query.
/// </summary>
[ExcludeFromCodeCoverage]
public record FlexSymbolSummary
{
    /// <summary>Account ID.</summary>
    public string AccountId { get; init; } = string.Empty;

    /// <summary>Currency.</summary>
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

    /// <summary>Listing exchange.</summary>
    public string ListingExchange { get; init; } = string.Empty;

    /// <summary>Trade date.</summary>
    public DateOnly? TradeDate { get; init; }

    /// <summary>Settle date.</summary>
    public DateOnly? SettleDate { get; init; }

    /// <summary>Report date.</summary>
    public DateOnly? ReportDate { get; init; }

    /// <summary>Buy/Sell side.</summary>
    public string BuySell { get; init; } = string.Empty;

    /// <summary>Total quantity across all fills for this symbol.</summary>
    public decimal Quantity { get; init; }

    /// <summary>Average price across all fills for this symbol.</summary>
    public decimal Price { get; init; }

    /// <summary>Total amount.</summary>
    public decimal Amount { get; init; }

    /// <summary>Total proceeds.</summary>
    public decimal Proceeds { get; init; }

    /// <summary>Net cash impact.</summary>
    public decimal NetCash { get; init; }

    /// <summary>Total commission.</summary>
    public decimal Commission { get; init; }

    /// <summary>Level of detail (e.g. "SYMBOL_SUMMARY").</summary>
    public string LevelOfDetail { get; init; } = string.Empty;

    /// <summary>Raw XML element for access to attributes not surfaced on this DTO.</summary>
    public XElement? RawElement { get; init; }
}
