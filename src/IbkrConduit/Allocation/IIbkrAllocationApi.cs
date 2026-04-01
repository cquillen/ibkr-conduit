using Refit;

namespace IbkrConduit.Allocation;

/// <summary>
/// Refit interface for IBKR FA (Financial Advisor) allocation endpoints.
/// </summary>
public interface IIbkrAllocationApi
{
    /// <summary>
    /// Retrieves a list of all allocatable sub-accounts with balance data.
    /// </summary>
    [Get("/v1/api/iserver/account/allocation/accounts")]
    Task<AllocationAccountsResponse> GetAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all allocation groups with summary information.
    /// </summary>
    [Get("/v1/api/iserver/account/allocation/group")]
    Task<AllocationGroupListResponse> GetGroupsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new allocation group.
    /// </summary>
    [Post("/v1/api/iserver/account/allocation/group")]
    Task<AllocationSuccessResponse> AddGroupAsync(
        [Body] AllocationGroupRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the configuration of a single allocation group.
    /// </summary>
    [Post("/v1/api/iserver/account/allocation/group/single")]
    Task<AllocationGroupDetail> GetGroupAsync(
        [Body] AllocationGroupNameRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an allocation group.
    /// </summary>
    [Post("/v1/api/iserver/account/allocation/group/delete")]
    Task<AllocationSuccessResponse> DeleteGroupAsync(
        [Body] AllocationGroupNameRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Modifies an existing allocation group.
    /// </summary>
    [Put("/v1/api/iserver/account/allocation/group")]
    Task<AllocationSuccessResponse> ModifyGroupAsync(
        [Body] AllocationGroupRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the allocation preset configuration.
    /// </summary>
    [Get("/v1/api/iserver/account/allocation/presets")]
    Task<AllocationPresetsResponse> GetPresetsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the allocation preset configuration.
    /// </summary>
    [Post("/v1/api/iserver/account/allocation/presets")]
    Task<AllocationSuccessResponse> SetPresetsAsync(
        [Body] AllocationPresetsRequest request, CancellationToken cancellationToken = default);
}
