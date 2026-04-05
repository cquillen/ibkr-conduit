using IbkrConduit.Diagnostics;
using IbkrConduit.Errors;
using IbkrConduit.EventContracts;
using IbkrConduit.Session;

namespace IbkrConduit.Client;

/// <summary>
/// Event contract (ForecastEx) operations that delegate to the underlying Refit API.
/// </summary>
public class EventContractOperations : IEventContractOperations
{
    private readonly IIbkrEventContractApi _api;
    private readonly IbkrClientOptions _options;

    /// <summary>
    /// Creates a new <see cref="EventContractOperations"/> instance.
    /// </summary>
    /// <param name="api">The Refit event contract API client.</param>
    /// <param name="options">Client options.</param>
    public EventContractOperations(IIbkrEventContractApi api, IbkrClientOptions options)
    {
        _api = api;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<Result<EventContractCategoryTreeResponse>> GetCategoryTreeAsync(
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.EventContracts.GetCategoryTree");
        var response = await _api.GetCategoryTreeAsync(cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<EventContractMarketResponse>> GetMarketAsync(int underlyingConid,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.EventContracts.GetMarket");
        activity?.SetTag("underlyingConid", underlyingConid);
        var response = await _api.GetMarketAsync(underlyingConid, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<EventContractRulesResponse>> GetContractRulesAsync(int conid,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.EventContracts.GetContractRules");
        activity?.SetTag("conid", conid);
        var response = await _api.GetContractRulesAsync(conid, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<EventContractDetailsResponse>> GetContractDetailsAsync(int conid,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.EventContracts.GetContractDetails");
        activity?.SetTag("conid", conid);
        var response = await _api.GetContractDetailsAsync(conid, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<EventContractSchedulesResponse>> GetContractSchedulesAsync(int conid,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.EventContracts.GetContractSchedules");
        activity?.SetTag("conid", conid);
        var response = await _api.GetContractSchedulesAsync(conid, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }
}
