namespace IbkrConduit.Health;

/// <summary>
/// Thread-safe tracker for the timestamp of the last successful API call.
/// Registered as a singleton and shared across all HTTP pipeline handler instances.
/// </summary>
internal sealed class LastSuccessfulCallTracker
{
    /// <summary>
    /// Gets the timestamp of the last successful API call, or null if none has been recorded.
    /// </summary>
    public DateTimeOffset? LastSuccessfulCall { get; private set; }

    /// <summary>
    /// Records the current time as the last successful call timestamp.
    /// </summary>
    public void RecordSuccess() => LastSuccessfulCall = DateTimeOffset.UtcNow;
}

/// <summary>
/// DelegatingHandler that tracks the timestamp of the last successful (2xx) HTTP response
/// by writing to a shared <see cref="LastSuccessfulCallTracker"/>.
/// </summary>
internal sealed class LastSuccessfulCallHandler : DelegatingHandler
{
    private readonly LastSuccessfulCallTracker _tracker;

    /// <summary>
    /// Creates a new <see cref="LastSuccessfulCallHandler"/>.
    /// </summary>
    /// <param name="tracker">Shared tracker for recording successful calls.</param>
    public LastSuccessfulCallHandler(LastSuccessfulCallTracker tracker)
    {
        _tracker = tracker;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            _tracker.RecordSuccess();
        }

        return response;
    }
}
