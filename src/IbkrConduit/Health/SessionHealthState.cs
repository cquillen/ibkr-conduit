using System.Diagnostics.CodeAnalysis;

namespace IbkrConduit.Health;

/// <summary>
/// Holds the last known session health state, updated by the tickle timer
/// and session manager. Registered as a singleton and read by the health
/// status collector for passive (non-probing) health checks.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class SessionHealthState
{
    /// <summary>Whether the session is currently authenticated.</summary>
    public bool Authenticated { get; set; }

    /// <summary>Whether the session is connected to the backend.</summary>
    public bool Connected { get; set; }

    /// <summary>Whether this session is competing with another.</summary>
    public bool Competing { get; set; }

    /// <summary>Whether the session has been fully established.</summary>
    public bool Established { get; set; }

    /// <summary>Failure reason from the last status check, if any.</summary>
    public string? FailReason { get; set; }
}
