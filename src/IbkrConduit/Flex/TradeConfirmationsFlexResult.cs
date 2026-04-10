using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

namespace IbkrConduit.Flex;

/// <summary>
/// Strongly-typed result for a Trade Confirmations Flex query.
/// </summary>
/// <param name="QueryName">Name of the Flex query template as configured in the IBKR portal.</param>
/// <param name="GeneratedAt">Timestamp when the report was generated.</param>
/// <param name="FromDate">Minimum fromDate across all FlexStatement elements.</param>
/// <param name="ToDate">Maximum toDate across all FlexStatement elements.</param>
/// <param name="TradeConfirmations">Individual execution rows.</param>
/// <param name="SymbolSummaries">Per-symbol aggregate rows.</param>
/// <param name="Orders">Per-order aggregate rows.</param>
/// <param name="RawXml">Raw response document for access to fields not surfaced on the DTO.</param>
[ExcludeFromCodeCoverage]
public record TradeConfirmationsFlexResult(
    string QueryName,
    DateTimeOffset? GeneratedAt,
    DateOnly? FromDate,
    DateOnly? ToDate,
    IReadOnlyList<FlexTradeConfirmation> TradeConfirmations,
    IReadOnlyList<FlexSymbolSummary> SymbolSummaries,
    IReadOnlyList<FlexOrder> Orders,
    XDocument RawXml);
