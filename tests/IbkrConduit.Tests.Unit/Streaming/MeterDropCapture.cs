using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using IbkrConduit.Diagnostics;

namespace IbkrConduit.Tests.Unit.Streaming;

/// <summary>
/// Captures <c>ibkr.conduit.streaming.frames.dropped</c> measurements for a single tenant so a
/// test can assert that a dropped frame was counted with the expected wire topic and cause. Filters
/// by tenant id so concurrently-running tests do not pollute one another's assertions.
/// </summary>
internal sealed class MeterDropCapture : IDisposable
{
    private readonly MeterListener _listener;
    private readonly string _tenantId;
    private readonly List<(string Topic, string Cause)> _drops = [];
    private readonly object _lock = new();

    public MeterDropCapture(string tenantId)
    {
        _tenantId = tenantId;
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == IbkrConduitDiagnostics.MeterName
                    && instrument.Name == "ibkr.conduit.streaming.frames.dropped")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        _listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            string? tenant = null;
            string? topic = null;
            string? cause = null;
            foreach (var tag in tags)
            {
                if (tag.Key == LogFields.TenantId)
                {
                    tenant = tag.Value as string;
                }
                else if (tag.Key == LogFields.Topic)
                {
                    topic = tag.Value as string;
                }
                else if (tag.Key == LogFields.Cause)
                {
                    cause = tag.Value as string;
                }
            }

            if (tenant == _tenantId && topic is not null && cause is not null)
            {
                lock (_lock)
                {
                    _drops.Add((topic, cause));
                }
            }
        });
        _listener.Start();
    }

    /// <summary>Every captured drop for this tenant, as (wire topic, cause) pairs, in order.</summary>
    public IReadOnlyList<(string Topic, string Cause)> Drops
    {
        get
        {
            lock (_lock)
            {
                return _drops.ToArray();
            }
        }
    }

    public void Dispose() => _listener.Dispose();
}
