using IbkrConduit.Errors;
using IbkrConduit.Flex;

namespace IbkrConduit.Client;

/// <summary>
/// Operations for the IBKR Flex Web Service, providing access to Flex Query reports.
/// </summary>
public interface IFlexOperations
{
    /// <summary>
    /// Executes a Flex Query and returns the parsed result.
    /// Handles the two-step request/poll flow internally.
    /// </summary>
    /// <param name="queryId">The Flex Query template ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Result{T}"/> wrapping the parsed Flex Query result on success,
    /// or an <see cref="IbkrError"/> subtype on failure.</returns>
    Task<Result<FlexQueryResult>> ExecuteQueryAsync(
        string queryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a Flex Query with a date range override.
    /// Date format: yyyyMMdd.
    /// </summary>
    /// <param name="queryId">The Flex Query template ID.</param>
    /// <param name="fromDate">Start date in yyyyMMdd format.</param>
    /// <param name="toDate">End date in yyyyMMdd format.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Result{T}"/> wrapping the parsed Flex Query result on success,
    /// or an <see cref="IbkrError"/> subtype on failure.</returns>
    Task<Result<FlexQueryResult>> ExecuteQueryAsync(
        string queryId, string fromDate, string toDate,
        CancellationToken cancellationToken = default);
}
