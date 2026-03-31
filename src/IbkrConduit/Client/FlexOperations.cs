using IbkrConduit.Flex;

namespace IbkrConduit.Client;

/// <summary>
/// Default implementation of <see cref="IFlexOperations"/> that validates
/// the Flex token and delegates to <see cref="FlexClient"/>.
/// </summary>
internal sealed class FlexOperations : IFlexOperations
{
    private readonly FlexClient? _flexClient;

    /// <summary>
    /// Creates a new <see cref="FlexOperations"/> instance.
    /// </summary>
    /// <param name="flexClient">The Flex client, or null if no Flex token is configured.</param>
    public FlexOperations(FlexClient? flexClient)
    {
        _flexClient = flexClient;
    }

    /// <inheritdoc />
    public async Task<FlexQueryResult> ExecuteQueryAsync(
        string queryId, CancellationToken cancellationToken = default)
    {
        EnsureFlexConfigured();
        var doc = await _flexClient!.ExecuteQueryAsync(queryId, null, null, cancellationToken);
        return new FlexQueryResult(doc);
    }

    /// <inheritdoc />
    public async Task<FlexQueryResult> ExecuteQueryAsync(
        string queryId, string fromDate, string toDate,
        CancellationToken cancellationToken = default)
    {
        EnsureFlexConfigured();
        var doc = await _flexClient!.ExecuteQueryAsync(queryId, fromDate, toDate, cancellationToken);
        return new FlexQueryResult(doc);
    }

    private void EnsureFlexConfigured()
    {
        if (_flexClient == null)
        {
            throw new InvalidOperationException(
                "Flex operations require a FlexToken to be configured in IbkrClientOptions. " +
                "Set IbkrClientOptions.FlexToken when calling AddIbkrClient().");
        }
    }
}
