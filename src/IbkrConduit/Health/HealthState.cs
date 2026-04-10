namespace IbkrConduit.Health;

/// <summary>
/// Overall health assessment of the IBKR connection.
/// </summary>
public enum HealthState
{
    /// <summary>All signals nominal.</summary>
    Healthy,

    /// <summary>Functional but one or more signals indicate potential issues.</summary>
    Degraded,

    /// <summary>Connection is not functional.</summary>
    Unhealthy,
}
