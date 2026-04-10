using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

namespace IbkrConduit.Flex;

/// <summary>
/// A cash transaction row parsed from a Cash Transactions Flex query response.
/// </summary>
[ExcludeFromCodeCoverage]
public record FlexCashTransaction
{
    /// <summary>Account ID for the transaction.</summary>
    public string AccountId { get; init; } = string.Empty;

    /// <summary>Transaction currency.</summary>
    public string Currency { get; init; } = string.Empty;

    /// <summary>FX rate to base currency.</summary>
    public decimal FxRateToBase { get; init; }

    /// <summary>Asset category.</summary>
    public string AssetCategory { get; init; } = string.Empty;

    /// <summary>Instrument symbol (if applicable).</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Human-readable description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>IBKR contract ID (if applicable).</summary>
    public int? Conid { get; init; }

    /// <summary>Transaction timestamp.</summary>
    public DateTimeOffset? DateTime { get; init; }

    /// <summary>Settlement date.</summary>
    public DateOnly? SettleDate { get; init; }

    /// <summary>Report date.</summary>
    public DateOnly? ReportDate { get; init; }

    /// <summary>Transaction amount.</summary>
    public decimal Amount { get; init; }

    /// <summary>Transaction type (e.g. "Deposits/Withdrawals", "Broker Interest Received").</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>IBKR transaction ID.</summary>
    public string TransactionId { get; init; } = string.Empty;

    /// <summary>Transaction code flags.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Level of detail (e.g. "DETAIL").</summary>
    public string LevelOfDetail { get; init; } = string.Empty;

    /// <summary>Raw XML element for access to attributes not surfaced on this DTO.</summary>
    public XElement? RawElement { get; init; }
}
