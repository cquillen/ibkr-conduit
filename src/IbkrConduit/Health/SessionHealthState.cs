namespace IbkrConduit.Health;

/// <summary>
/// Holds the last known session health state, updated by the tickle timer
/// and session manager. Registered as a singleton and read by the health
/// status collector for passive (non-probing) health checks.
/// All access is synchronized via a lock for thread safety.
/// </summary>
internal sealed class SessionHealthState
{
    private readonly object _lock = new();
    private bool _authenticated;
    private bool _connected;
    private bool _competing;
    private bool _established;
    private string? _failReason;

    /// <summary>Whether the session is currently authenticated.</summary>
    public bool Authenticated
    {
        get { lock (_lock) { return _authenticated; } }
    }

    /// <summary>Whether the session is connected to the backend.</summary>
    public bool Connected
    {
        get { lock (_lock) { return _connected; } }
    }

    /// <summary>Whether this session is competing with another.</summary>
    public bool Competing
    {
        get { lock (_lock) { return _competing; } }
    }

    /// <summary>Whether the session has been fully established.</summary>
    public bool Established
    {
        get { lock (_lock) { return _established; } }
    }

    /// <summary>Failure reason from the last status check, if any.</summary>
    public string? FailReason
    {
        get { lock (_lock) { return _failReason; } }
    }

    /// <summary>
    /// Atomically updates all session health fields under a single lock acquisition.
    /// </summary>
    /// <param name="authenticated">Whether the session is authenticated.</param>
    /// <param name="connected">Whether the session is connected.</param>
    /// <param name="competing">Whether this session is competing with another.</param>
    /// <param name="established">Whether the session has been fully established.</param>
    /// <param name="failReason">Failure reason, if any.</param>
    public void Update(bool authenticated, bool connected, bool competing, bool established, string? failReason = null)
    {
        lock (_lock)
        {
            _authenticated = authenticated;
            _connected = connected;
            _competing = competing;
            _established = established;
            _failReason = failReason;
        }
    }

    /// <summary>
    /// Atomically marks the session as failed by clearing authentication and setting a failure reason.
    /// </summary>
    /// <param name="reason">Description of the failure.</param>
    public void SetFailed(string reason)
    {
        lock (_lock)
        {
            _authenticated = false;
            _failReason = reason;
        }
    }

    /// <summary>
    /// Returns an immutable snapshot of the current session health state,
    /// captured atomically under a single lock acquisition.
    /// </summary>
    /// <returns>An immutable <see cref="SessionHealthSnapshot"/> of the current state.</returns>
    public SessionHealthSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            return new SessionHealthSnapshot(_authenticated, _connected, _competing, _established, _failReason);
        }
    }
}

/// <summary>
/// Immutable snapshot of session health state at a point in time.
/// </summary>
/// <param name="Authenticated">Whether the session is authenticated.</param>
/// <param name="Connected">Whether the session is connected.</param>
/// <param name="Competing">Whether this session is competing with another.</param>
/// <param name="Established">Whether the session has been fully established.</param>
/// <param name="FailReason">Failure reason, if any.</param>
internal sealed record SessionHealthSnapshot(
    bool Authenticated,
    bool Connected,
    bool Competing,
    bool Established,
    string? FailReason);
