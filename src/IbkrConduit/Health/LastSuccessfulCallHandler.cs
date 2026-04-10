namespace IbkrConduit.Health;

/// <summary>
/// Thread-safe tracker for the timestamp of the last successful API call.
/// Registered as a singleton and shared across all HTTP pipeline handler instances.
/// Uses <see cref="Interlocked"/> operations for lock-free thread safety.
/// </summary>
internal sealed class LastSuccessfulCallTracker
{
    private long _lastSuccessfulCallTicks;

    /// <summary>
    /// Gets the timestamp of the last successful API call, or null if none has been recorded.
    /// </summary>
    public DateTimeOffset? LastSuccessfulCall
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastSuccessfulCallTicks);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    /// <summary>
    /// Records the current time as the last successful call timestamp.
    /// </summary>
    public void RecordSuccess() =>
        Interlocked.Exchange(ref _lastSuccessfulCallTicks, DateTimeOffset.UtcNow.Ticks);
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
