using IbkrConduit.Errors;
using IbkrConduit.Flex;

namespace IbkrConduit.Client;

/// <summary>
/// Operations for the IBKR Flex Web Service, providing access to Flex Query reports.
/// </summary>
public interface IFlexOperations
{
    /// <summary>
    /// Fetches cash transactions using the Cash Transactions query template configured
    /// in <c>IbkrClientOptions.FlexQueries</c>. The query's configured period
    /// (set in the IBKR portal) determines the date range — runtime date overrides are
    /// not supported because Activity Flex queries can hang server-side on multi-day
    /// range overrides.
    /// </summary>
    /// <remarks>
    /// <para>The query template in the IBKR portal should have "Breakout by Day" set to
    /// <b>No</b> for best results. When enabled, IBKR generates a separate
    /// <c>&lt;FlexStatement&gt;</c> per trading day (128+ empty wrappers for a 365-day
    /// query), producing a much larger response. The parser handles both shapes —
    /// transactions are flattened across all statement wrappers regardless of the
    /// breakout setting — but the consolidated shape is 10x smaller and faster.</para>
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Result{T}"/> wrapping a typed
    /// <see cref="CashTransactionsFlexResult"/> on success, or an <see cref="IbkrError"/>
    /// subtype on failure.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <c>IbkrClientOptions.FlexQueries.CashTransactionsQueryId</c> is not set,
    /// or if no Flex token is configured.
    /// </exception>
    Task<Result<CashTransactionsFlexResult>> GetCashTransactionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches trade confirmations for the given date range using the Trade Confirmations
    /// query template configured in <c>IbkrClientOptions.FlexQueries</c>.
    /// </summary>
    /// <param name="fromDate">Start date (inclusive).</param>
    /// <param name="toDate">End date (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Result{T}"/> wrapping a typed
    /// <see cref="TradeConfirmationsFlexResult"/> on success, or an <see cref="IbkrError"/>
    /// subtype on failure.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <c>IbkrClientOptions.FlexQueries.TradeConfirmationsQueryId</c> is not set,
    /// or if no Flex token is configured.
    /// </exception>
    Task<Result<TradeConfirmationsFlexResult>> GetTradeConfirmationsAsync(
        DateOnly fromDate, DateOnly toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an arbitrary Flex query by ID and returns a generic result with envelope
    /// metadata. Use this for query types without dedicated typed methods.
    /// </summary>
    /// <param name="queryId">The Flex Query template ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Result{T}"/> wrapping a <see cref="FlexGenericResult"/> on
    /// success, or an <see cref="IbkrError"/> subtype on failure.</returns>
    Task<Result<FlexGenericResult>> ExecuteQueryAsync(
        string queryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an arbitrary Flex query with runtime date range override.
    /// Note: multi-day runtime overrides can hang on Activity Flex (<c>AF</c>) query types —
    /// in those cases, configure the period in the query template instead.
    /// Trade Confirmation Flex (<c>TCF</c>) queries handle runtime overrides reliably.
    /// Date format: <c>yyyyMMdd</c>.
    /// </summary>
    /// <param name="queryId">The Flex Query template ID.</param>
    /// <param name="fromDate">Start date in <c>yyyyMMdd</c> format.</param>
    /// <param name="toDate">End date in <c>yyyyMMdd</c> format.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Result{T}"/> wrapping a <see cref="FlexGenericResult"/> on
    /// success, or an <see cref="IbkrError"/> subtype on failure.</returns>
    Task<Result<FlexGenericResult>> ExecuteQueryAsync(
        string queryId, string fromDate, string toDate,
        CancellationToken cancellationToken = default);
}
