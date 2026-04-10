using System.Diagnostics.CodeAnalysis;

namespace IbkrConduit.Flex;

/// <summary>
/// Query IDs for strongly-typed Flex Web Service operations.
/// </summary>
[ExcludeFromCodeCoverage]
public class FlexQueryOptions
{
    /// <summary>
    /// Query ID for the Cash Transactions Flex query template.
    /// Required by <c>IFlexOperations.GetCashTransactionsAsync</c>.
    /// Configure in IBKR portal: Reports → Flex Queries → create an Activity Flex
    /// query with the Cash Transactions section enabled, then copy the numeric ID here.
    /// </summary>
    public string? CashTransactionsQueryId { get; set; }

    /// <summary>
    /// Query ID for the Trade Confirmations Flex query template.
    /// Required by <c>IFlexOperations.GetTradeConfirmationsAsync</c>.
    /// Configure in IBKR portal: Reports → Flex Queries → create a Trade Confirmation
    /// Flex query with Trade Confirms / Symbol Summary / Orders sections enabled,
    /// then copy the numeric ID here.
    /// </summary>
    public string? TradeConfirmationsQueryId { get; set; }
}
