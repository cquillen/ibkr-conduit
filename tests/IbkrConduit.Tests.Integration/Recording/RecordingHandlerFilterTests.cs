using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.Http;
using Shouldly;

namespace IbkrConduit.Tests.Integration.Recording;

public class RecordingHandlerFilterTests
{
    [Fact]
    public void Configure_WhenRecordingEnabled_WrapsWithRecordingHandler()
    {
        var context = new RecordingContext();
        var filter = new RecordingHandlerFilter(context, enabled: true);

        Action<HttpMessageHandlerBuilder> next = _ => { };
        var configured = filter.Configure(next);

        var builder = new TestHttpMessageHandlerBuilder();
        configured(builder);

        builder.AdditionalHandlers
            .OfType<RecordingDelegatingHandler>()
            .Count()
            .ShouldBe(1);
    }

    [Fact]
    public void Configure_WhenRecordingDisabled_PassesThrough()
    {
        var context = new RecordingContext();
        var filter = new RecordingHandlerFilter(context, enabled: false);

        var nextCalled = false;
        Action<HttpMessageHandlerBuilder> next = _ => { nextCalled = true; };
        var configured = filter.Configure(next);

        var builder = new TestHttpMessageHandlerBuilder();
        configured(builder);

        nextCalled.ShouldBeTrue();
        builder.AdditionalHandlers
            .OfType<RecordingDelegatingHandler>()
            .ShouldBeEmpty();
    }

    private sealed class TestHttpMessageHandlerBuilder : HttpMessageHandlerBuilder
    {
        public override string? Name { get; set; }

        public override HttpMessageHandler PrimaryHandler { get; set; } = new HttpClientHandler();

        public override IList<DelegatingHandler> AdditionalHandlers { get; } = new List<DelegatingHandler>();

        public override HttpMessageHandler Build()
        {
            return PrimaryHandler;
        }
    }
}
