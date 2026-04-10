using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

namespace IbkrConduit.Flex;

/// <summary>
/// Per-statement metadata exposed on <see cref="FlexGenericResult"/>.
/// </summary>
/// <param name="AccountId">Account ID the statement belongs to.</param>
/// <param name="FromDate">Statement start date.</param>
/// <param name="ToDate">Statement end date.</param>
/// <param name="Period">Period name (e.g. "Last365CalendarDays", "Today"). Empty when overridden.</param>
/// <param name="WhenGenerated">Timestamp when the statement was generated.</param>
/// <param name="RawElement">Raw FlexStatement element for drilling into sections the generic result does not parse.</param>
[ExcludeFromCodeCoverage]
public record FlexStatementInfo(
    string AccountId,
    DateOnly? FromDate,
    DateOnly? ToDate,
    string Period,
    DateTimeOffset? WhenGenerated,
    XElement RawElement);
