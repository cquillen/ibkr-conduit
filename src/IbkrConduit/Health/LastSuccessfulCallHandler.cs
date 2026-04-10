namespace IbkrConduit.Health;

/// <summary>
/// DelegatingHandler that tracks the timestamp of the last successful (2xx) HTTP response.
/// </summary>
internal sealed class LastSuccessfulCallHandler : DelegatingHandler
{
    /// <summary>
    /// Gets the timestamp of the last successful API call, or null if none has been recorded.
    /// </summary>
    public DateTimeOffset? LastSuccessfulCall { get; private set; }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            LastSuccessfulCall = DateTimeOffset.UtcNow;
        }

        return response;
    }
}
