using IbkrConduit.Allocation;

namespace IbkrConduit.Client;

/// <summary>
/// FA allocation operations on the IBKR API.
/// </summary>
public interface IAllocationOperations
{
    /// <summary>
    /// Retrieves a list of all allocatable sub-accounts with balance data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AllocationAccountsResponse> GetAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all allocation groups with summary information.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AllocationGroupListResponse> GetGroupsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new allocation group.
    /// </summary>
    /// <param name="request">The group definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AllocationSuccessResponse> AddGroupAsync(AllocationGroupRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the configuration of a single allocation group by name.
    /// </summary>
    /// <param name="name">The allocation group name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AllocationGroupDetail> GetGroupAsync(string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an allocation group by name.
    /// </summary>
    /// <param name="name">The allocation group name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AllocationSuccessResponse> DeleteGroupAsync(string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Modifies an existing allocation group.
    /// </summary>
    /// <param name="request">The group definition with updated values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AllocationSuccessResponse> ModifyGroupAsync(AllocationGroupRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the allocation preset configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AllocationPresetsResponse> GetPresetsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the allocation preset configuration.
    /// </summary>
    /// <param name="request">The preset configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AllocationSuccessResponse> SetPresetsAsync(AllocationPresetsRequest request,
        CancellationToken cancellationToken = default);
}
