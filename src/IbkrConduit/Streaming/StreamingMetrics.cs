using System.Diagnostics.Metrics;
using IbkrConduit.Diagnostics;

namespace IbkrConduit.Streaming;

/// <summary>
/// Per-tenant reporter for the streaming delivery guarantee's loss-is-observable contract
/// (<see href="../../docs/adr/0002-streaming-delivery-guarantee.md">ADR-0002</see>). Owns the
/// single <c>ibkr.conduit.streaming.frames.dropped</c> counter and stamps every increment with
/// the tenant so a dropped frame — whether evicted under buffer overflow, discarded by a mapper
/// failure, or lost because a consumer's <see cref="IObserver{T}.OnNext"/> threw — is always
/// countable by cause and topic. No streaming frame is lost without a counter increment.
/// </summary>
internal sealed class StreamingMetrics
{
    /// <summary>Cause tag value for a frame evicted by bounded-channel overflow (DropOldest).</summary>
    public const string OverflowCause = "overflow";

    /// <summary>Cause tag value for a frame discarded because its mapper threw.</summary>
    public const string MapperCause = "mapper";

    /// <summary>Cause tag value for a frame lost because the consumer's observer threw.</summary>
    public const string ObserverCause = "observer";

    private static readonly Counter<long> _framesDropped =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.streaming.frames.dropped");

    private readonly TenantContext _tenant;

    /// <summary>Creates a new <see cref="StreamingMetrics"/> bound to a tenant.</summary>
    /// <param name="tenant">Per-provider tenant identity used to tag the dropped-frames counter.</param>
    public StreamingMetrics(TenantContext tenant) => _tenant = tenant;

    /// <summary>
    /// Records that one streaming frame was dropped on the given wire topic for the given cause.
    /// Increments <c>ibkr.conduit.streaming.frames.dropped</c> tagged with the tenant, the wire
    /// topic prefix, and the cause.
    /// </summary>
    /// <param name="topic">The wire topic prefix the dropped frame belonged to (e.g. <c>str</c>, <c>sor</c>).</param>
    /// <param name="cause">Why the frame was dropped: <see cref="OverflowCause"/>, <see cref="MapperCause"/>, or <see cref="ObserverCause"/>.</param>
    public void RecordDrop(string topic, string cause) =>
        _framesDropped.Add(
            1,
            new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId),
            new KeyValuePair<string, object?>(LogFields.Topic, topic),
            new KeyValuePair<string, object?>(LogFields.Cause, cause));
}
