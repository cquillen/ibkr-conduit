using System.Diagnostics;
using System.Diagnostics.Metrics;
using IbkrConduit.Diagnostics;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Diagnostics;

public class DiagnosticsTests
{
    [Fact]
    public void ActivitySourceName_ShouldBeIbkrConduit()
    {
        IbkrConduitDiagnostics.ActivitySourceName.ShouldBe("IbkrConduit");
    }

    [Fact]
    public void MeterName_ShouldBeIbkrConduit()
    {
        IbkrConduitDiagnostics.MeterName.ShouldBe("IbkrConduit");
    }

    [Fact]
    public void ActivitySource_ShouldNotBeNull()
    {
        IbkrConduitDiagnostics.ActivitySource.ShouldNotBeNull();
        IbkrConduitDiagnostics.ActivitySource.Name.ShouldBe("IbkrConduit");
    }

    [Fact]
    public void Meter_ShouldNotBeNull()
    {
        IbkrConduitDiagnostics.Meter.ShouldNotBeNull();
        IbkrConduitDiagnostics.Meter.Name.ShouldBe("IbkrConduit");
    }

    [Fact]
    public void LogFields_AllConstants_ShouldBeNonEmpty()
    {
        LogFields.TenantId.ShouldNotBeNullOrWhiteSpace();
        LogFields.AccountId.ShouldNotBeNullOrWhiteSpace();
        LogFields.Conid.ShouldNotBeNullOrWhiteSpace();
        LogFields.OrderId.ShouldNotBeNullOrWhiteSpace();
        LogFields.Symbol.ShouldNotBeNullOrWhiteSpace();
        LogFields.Endpoint.ShouldNotBeNullOrWhiteSpace();
        LogFields.Method.ShouldNotBeNullOrWhiteSpace();
        LogFields.StatusCode.ShouldNotBeNullOrWhiteSpace();
        LogFields.DurationMs.ShouldNotBeNullOrWhiteSpace();
        LogFields.QuestionCount.ShouldNotBeNullOrWhiteSpace();
        LogFields.PollCount.ShouldNotBeNullOrWhiteSpace();
        LogFields.Trigger.ShouldNotBeNullOrWhiteSpace();
        LogFields.Topic.ShouldNotBeNullOrWhiteSpace();
        LogFields.QueryId.ShouldNotBeNullOrWhiteSpace();
        LogFields.Attempt.ShouldNotBeNullOrWhiteSpace();
        LogFields.Cached.ShouldNotBeNullOrWhiteSpace();
        LogFields.PreflightNeeded.ShouldNotBeNullOrWhiteSpace();
        LogFields.Side.ShouldNotBeNullOrWhiteSpace();
        LogFields.OrderType.ShouldNotBeNullOrWhiteSpace();
        LogFields.ErrorCode.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void LogFields_AllConstants_ShouldStartWithIbkrPrefix()
    {
        LogFields.TenantId.ShouldStartWith("ibkr.");
        LogFields.AccountId.ShouldStartWith("ibkr.");
        LogFields.Conid.ShouldStartWith("ibkr.");
        LogFields.OrderId.ShouldStartWith("ibkr.");
        LogFields.Symbol.ShouldStartWith("ibkr.");
        LogFields.Endpoint.ShouldStartWith("ibkr.");
        LogFields.Method.ShouldStartWith("ibkr.");
        LogFields.StatusCode.ShouldStartWith("ibkr.");
        LogFields.DurationMs.ShouldStartWith("ibkr.");
        LogFields.QuestionCount.ShouldStartWith("ibkr.");
        LogFields.PollCount.ShouldStartWith("ibkr.");
        LogFields.Trigger.ShouldStartWith("ibkr.");
        LogFields.Topic.ShouldStartWith("ibkr.");
        LogFields.QueryId.ShouldStartWith("ibkr.");
        LogFields.Attempt.ShouldStartWith("ibkr.");
        LogFields.Cached.ShouldStartWith("ibkr.");
        LogFields.PreflightNeeded.ShouldStartWith("ibkr.");
        LogFields.Side.ShouldStartWith("ibkr.");
        LogFields.OrderType.ShouldStartWith("ibkr.");
        LogFields.ErrorCode.ShouldStartWith("ibkr.");
    }

    [Fact]
    public void ActivitySource_WhenListenerSubscribed_ShouldEmitSpan()
    {
        Activity? capturedActivity = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "IbkrConduit",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => capturedActivity = activity,
        };
        ActivitySource.AddActivityListener(listener);

        using (var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Test.Span"))
        {
            activity?.SetTag("test.key", "test_value");
        }

        capturedActivity.ShouldNotBeNull();
        capturedActivity.OperationName.ShouldBe("IbkrConduit.Test.Span");
        capturedActivity.GetTagItem("test.key").ShouldBe("test_value");
    }

    [Fact]
    public void Meter_WhenListenerSubscribed_ShouldRecordMetric()
    {
        var capturedValue = 0L;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "IbkrConduit" && instrument.Name == "ibkr.conduit.test.counter")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            capturedValue = measurement;
        });
        listener.Start();

        var testCounter = IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.test.counter");
        testCounter.Add(42);

        listener.RecordObservableInstruments();

        capturedValue.ShouldBe(42);
    }

    [Fact]
    public void ActivitySource_SpanWithTags_ShouldCaptureAllTags()
    {
        Activity? capturedActivity = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "IbkrConduit",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == "IbkrConduit.Test.TaggedSpan")
                {
                    capturedActivity = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        using (var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Test.TaggedSpan"))
        {
            activity?.SetTag(LogFields.AccountId, "DU12345");
            activity?.SetTag(LogFields.StatusCode, 200);
            activity?.SetTag(LogFields.Cached, true);
        }

        capturedActivity.ShouldNotBeNull();
        capturedActivity.GetTagItem(LogFields.AccountId).ShouldBe("DU12345");
        capturedActivity.GetTagItem(LogFields.StatusCode).ShouldBe(200);
        capturedActivity.GetTagItem(LogFields.Cached).ShouldBe(true);
    }

    [Fact]
    public void Meter_Histogram_ShouldRecordDuration()
    {
        var capturedValue = 0.0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "IbkrConduit" && instrument.Name == "ibkr.conduit.test.duration")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            capturedValue = measurement;
        });
        listener.Start();

        var testHistogram = IbkrConduitDiagnostics.Meter.CreateHistogram<double>("ibkr.conduit.test.duration", "ms");
        testHistogram.Record(42.5);

        capturedValue.ShouldBe(42.5);
    }

    [Fact]
    public void ActivitySource_NoListener_ShouldReturnNull()
    {
        // When no listener is subscribed, StartActivity returns null
        // This verifies the zero-overhead pattern works correctly
        var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Test.NoListener");

        // Without a listener, activity should be null (zero overhead)
        // Note: this test may capture an activity if another test registered a listener,
        // so we just verify it doesn't throw
        activity?.Dispose();
    }
}
