using IbkrConduit.Errors;
using IbkrConduit.EventContracts;

namespace IbkrConduit.Client;

/// <summary>
/// Event contract (ForecastEx) operations on the IBKR API.
/// </summary>
public interface IEventContractOperations
{
    /// <summary>
    /// Retrieves the full category tree for event contracts.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<EventContractCategoryTreeResponse>> GetCategoryTreeAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all contracts in a market by underlying conid.
    /// </summary>
    /// <param name="underlyingConid">The underlying contract identifier for the market.</param>
    /// <param name="exchange">Optional exchange filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<EventContractMarketResponse>> GetMarketAsync(int underlyingConid,
        string? exchange = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the rules for a specific event contract.
    /// </summary>
    /// <param name="conid">The contract identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<EventContractRulesResponse>> GetContractRulesAsync(int conid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves details for a specific event contract.
    /// </summary>
    /// <param name="conid">The contract identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<EventContractDetailsResponse>> GetContractDetailsAsync(int conid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves trading schedules for a specific event contract.
    /// </summary>
    /// <param name="conid">The contract identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<EventContractSchedulesResponse>> GetContractSchedulesAsync(int conid,
        CancellationToken cancellationToken = default);
}
