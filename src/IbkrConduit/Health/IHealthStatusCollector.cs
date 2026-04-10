namespace IbkrConduit.Health;

/// <summary>
/// Internal interface for collecting aggregated IBKR health status.
/// </summary>
internal interface IHealthStatusCollector
{
    /// <summary>
    /// Collects current health status from all signal sources.
    /// </summary>
    /// <param name="activeProbe">When true, makes a live API call to check session status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregated health status snapshot.</returns>
    Task<IbkrHealthStatus> GetHealthStatusAsync(
        bool activeProbe = false, CancellationToken cancellationToken = default);
}
