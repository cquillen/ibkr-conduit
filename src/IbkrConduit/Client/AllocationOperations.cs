using IbkrConduit.Allocation;
using IbkrConduit.Diagnostics;

namespace IbkrConduit.Client;

/// <summary>
/// FA allocation operations that delegate to the underlying Refit API.
/// </summary>
public class AllocationOperations : IAllocationOperations
{
    private readonly IIbkrAllocationApi _api;

    /// <summary>
    /// Creates a new <see cref="AllocationOperations"/> instance.
    /// </summary>
    /// <param name="api">The Refit allocation API client.</param>
    public AllocationOperations(IIbkrAllocationApi api) => _api = api;

    /// <inheritdoc />
    public async Task<AllocationAccountsResponse> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Allocation.GetAccounts");
        return await _api.GetAccountsAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AllocationGroupListResponse> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Allocation.GetGroups");
        return await _api.GetGroupsAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AllocationSuccessResponse> AddGroupAsync(AllocationGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Allocation.AddGroup");
        activity?.SetTag("group.name", request.Name);
        return await _api.AddGroupAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AllocationGroupDetail> GetGroupAsync(string name,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Allocation.GetGroup");
        activity?.SetTag("group.name", name);
        return await _api.GetGroupAsync(new AllocationGroupNameRequest(name), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AllocationSuccessResponse> DeleteGroupAsync(string name,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Allocation.DeleteGroup");
        activity?.SetTag("group.name", name);
        return await _api.DeleteGroupAsync(new AllocationGroupNameRequest(name), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AllocationSuccessResponse> ModifyGroupAsync(AllocationGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Allocation.ModifyGroup");
        activity?.SetTag("group.name", request.Name);
        return await _api.ModifyGroupAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AllocationPresetsResponse> GetPresetsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Allocation.GetPresets");
        return await _api.GetPresetsAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AllocationSuccessResponse> SetPresetsAsync(AllocationPresetsRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Allocation.SetPresets");
        return await _api.SetPresetsAsync(request, cancellationToken);
    }
}
