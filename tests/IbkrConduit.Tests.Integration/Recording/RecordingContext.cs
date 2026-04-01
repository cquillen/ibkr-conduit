using System.Threading;

namespace IbkrConduit.Tests.Integration.Recording;

/// <summary>
/// Thread-safe context for tracking the current recording scenario and step counter.
/// </summary>
public sealed class RecordingContext
{
    private int _stepCounter;

    /// <summary>
    /// The name of the currently active recording scenario, or null if not recording.
    /// </summary>
    public string? ScenarioName { get; set; }

    /// <summary>
    /// True when a recording scenario is active.
    /// </summary>
    public bool IsActive => ScenarioName is not null;

    /// <summary>
    /// Returns the next step number (1-based, thread-safe).
    /// </summary>
    public int NextStep() => Interlocked.Increment(ref _stepCounter);

    /// <summary>
    /// Resets the counter and sets a new scenario name.
    /// </summary>
    public void Reset(string scenarioName)
    {
        ScenarioName = scenarioName;
        Interlocked.Exchange(ref _stepCounter, 0);
    }
}
