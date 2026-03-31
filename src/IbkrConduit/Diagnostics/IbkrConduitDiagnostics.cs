using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace IbkrConduit.Diagnostics;

/// <summary>
/// Central diagnostics for the IbkrConduit library.
/// Consumers subscribe via OpenTelemetry or any System.Diagnostics listener.
/// </summary>
public static class IbkrConduitDiagnostics
{
    /// <summary>The ActivitySource name for tracing.</summary>
    public const string ActivitySourceName = "IbkrConduit";

    /// <summary>The Meter name for metrics.</summary>
    public const string MeterName = "IbkrConduit";

    /// <summary>ActivitySource for creating spans.</summary>
    public static ActivitySource ActivitySource { get; } = new(ActivitySourceName);

    /// <summary>Meter for creating metric instruments.</summary>
    public static Meter Meter { get; } = new(MeterName);
}
