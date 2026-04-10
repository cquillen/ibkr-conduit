using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

namespace IbkrConduit.Flex;

/// <summary>
/// Generic result for arbitrary Flex queries without a dedicated typed method.
/// </summary>
/// <param name="QueryName">Name of the Flex query template as configured in the IBKR portal.</param>
/// <param name="QueryType">Query type code from the FlexQueryResponse element (e.g. "AF", "TCF").</param>
/// <param name="GeneratedAt">Timestamp when the report was generated.</param>
/// <param name="Statements">Per-statement metadata for drilling into individual FlexStatement sections.</param>
/// <param name="RawXml">Raw response document.</param>
[ExcludeFromCodeCoverage]
public record FlexGenericResult(
    string QueryName,
    string QueryType,
    DateTimeOffset? GeneratedAt,
    IReadOnlyList<FlexStatementInfo> Statements,
    XDocument RawXml);
