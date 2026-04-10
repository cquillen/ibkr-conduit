using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

namespace IbkrConduit.Flex;

/// <summary>
/// Strongly-typed result for a Cash Transactions Flex query.
/// </summary>
/// <param name="QueryName">Name of the Flex query template as configured in the IBKR portal.</param>
/// <param name="GeneratedAt">Timestamp when the report was generated (from the first statement).</param>
/// <param name="FromDate">Minimum fromDate across all FlexStatement elements.</param>
/// <param name="ToDate">Maximum toDate across all FlexStatement elements.</param>
/// <param name="CashTransactions">Flattened list of cash transactions across all statements.</param>
/// <param name="RawXml">Raw response document for access to fields not surfaced on the DTO.</param>
[ExcludeFromCodeCoverage]
public record CashTransactionsFlexResult(
    string QueryName,
    DateTimeOffset? GeneratedAt,
    DateOnly? FromDate,
    DateOnly? ToDate,
    IReadOnlyList<FlexCashTransaction> CashTransactions,
    XDocument RawXml);
