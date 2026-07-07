using System;
using IbkrConduit.Diagnostics;
using IbkrConduit.Streaming;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class StreamingMetricsTests
{
    [Fact]
    public void RecordMissingMoneyField_IncrementsCounterWithTopicAndField()
    {
        // WIR-5: the required-money-field census is its own counter (money_field.absent), distinct
        // from the VCR-02 frames.dropped taxonomy — a censused frame is still delivered, never dropped.
        var tenantId = $"tenant-{Guid.NewGuid()}";
        using var capture = new MeterMoneyFieldAbsentCapture(tenantId);
        var metrics = new StreamingMetrics(new TenantContext(tenantId));

        metrics.RecordMissingMoneyField("str", "size");

        capture.Absences.ShouldHaveSingleItem();
        capture.Absences[0].ShouldBe(("str", "size"));
    }

    [Fact]
    public void RecordMissingMoneyField_DoesNotIncrementFramesDroppedCounter()
    {
        // The census must not pollute the drop taxonomy (ADR-0002): a counter increment on
        // frames.dropped implies a lost frame, but a censused frame is delivered intact.
        var tenantId = $"tenant-{Guid.NewGuid()}";
        using var dropCapture = new MeterDropCapture(tenantId);
        var metrics = new StreamingMetrics(new TenantContext(tenantId));

        metrics.RecordMissingMoneyField("sor", "price");

        dropCapture.Drops.ShouldBeEmpty();
    }
}
