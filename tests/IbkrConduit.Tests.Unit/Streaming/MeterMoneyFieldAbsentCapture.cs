using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using IbkrConduit.Diagnostics;

namespace IbkrConduit.Tests.Unit.Streaming;

/// <summary>
/// Captures <c>ibkr.conduit.streaming.money_field.absent</c> measurements for a single tenant so a
/// test can assert that an absent required streaming money field (WIR-5 census signal) was counted
/// with the expected wire topic and field name. Filters by tenant id so concurrently-running tests
/// do not pollute one another's assertions.
/// </summary>
internal sealed class MeterMoneyFieldAbsentCapture : IDisposable
{
    private readonly MeterListener _listener;
    private readonly string _tenantId;
    private readonly List<(string Topic, string Field)> _absences = [];
    private readonly object _lock = new();

    public MeterMoneyFieldAbsentCapture(string tenantId)
    {
        _tenantId = tenantId;
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == IbkrConduitDiagnostics.MeterName
                    && instrument.Name == "ibkr.conduit.streaming.money_field.absent")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        _listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            string? tenant = null;
            string? topic = null;
            string? field = null;
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
                else if (tag.Key == LogFields.Field)
                {
                    field = tag.Value as string;
                }
            }

            if (tenant == _tenantId && topic is not null && field is not null)
            {
                lock (_lock)
                {
                    _absences.Add((topic, field));
                }
            }
        });
        _listener.Start();
    }

    /// <summary>Every captured absence for this tenant, as (wire topic, field) pairs, in order.</summary>
    public IReadOnlyList<(string Topic, string Field)> Absences
    {
        get
        {
            lock (_lock)
            {
                return _absences.ToArray();
            }
        }
    }

    public void Dispose() => _listener.Dispose();
}
