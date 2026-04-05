using Refit;

namespace IbkrConduit.EventContracts;

/// <summary>
/// Refit interface for IBKR ForecastEx event contract endpoints.
/// </summary>
public interface IIbkrEventContractApi
{
    /// <summary>
    /// Retrieves the full category tree for event contracts.
    /// </summary>
    [Get("/v1/api/forecast/category/tree")]
    Task<IApiResponse<EventContractCategoryTreeResponse>> GetCategoryTreeAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all contracts in a market by underlying conid.
    /// </summary>
    [Get("/v1/api/forecast/contract/market")]
    Task<IApiResponse<EventContractMarketResponse>> GetMarketAsync(
        [Query] int underlyingConid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the rules for a specific event contract.
    /// </summary>
    [Get("/v1/api/forecast/contract/rules")]
    Task<IApiResponse<EventContractRulesResponse>> GetContractRulesAsync(
        [Query] int conid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves details for a specific event contract.
    /// </summary>
    [Get("/v1/api/forecast/contract/details")]
    Task<IApiResponse<EventContractDetailsResponse>> GetContractDetailsAsync(
        [Query] int conid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves trading schedules for a specific event contract.
    /// </summary>
    [Get("/v1/api/forecast/contract/schedules")]
    Task<IApiResponse<EventContractSchedulesResponse>> GetContractSchedulesAsync(
        [Query] int conid, CancellationToken cancellationToken = default);
}
