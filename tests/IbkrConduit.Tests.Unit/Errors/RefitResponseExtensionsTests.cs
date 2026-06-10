using System;
using System.Net;
using System.Net.Http;
using IbkrConduit.Errors;
using IbkrConduit.Tests.Unit.TestHelpers;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Errors;

public class RefitResponseExtensionsTests
{
    [Fact]
    public void ThrowOnSendFailure_WrappedCancellation_RethrowsOriginal()
    {
        var inner = new OperationCanceledException("cancelled");
        var response = FakeApiResponse.SendFailure<string>(inner);

        var ex = Should.Throw<OperationCanceledException>(() => response.ThrowOnSendFailure());
        ex.ShouldBeSameAs(inner);
    }

    [Fact]
    public void ThrowOnSendFailure_WrappedTransportError_RethrowsOriginal()
    {
        var inner = new HttpRequestException("dns failure");
        var response = FakeApiResponse.SendFailure<string>(inner);

        var ex = Should.Throw<HttpRequestException>(() => response.ThrowOnSendFailure());
        ex.ShouldBeSameAs(inner);
    }

    [Fact]
    public void ThrowOnSendFailure_NoError_DoesNothing()
    {
        var response = FakeApiResponse.Success("ok");
        Should.NotThrow(() => response.ThrowOnSendFailure());
    }

    [Fact]
    public void ThrowOnSendFailure_NormalHttpError_DoesNothing()
    {
        // ApiException (a received HTTP error) is not a send failure — must not throw.
        var response = FakeApiResponse.Failure<string>(HttpStatusCode.BadRequest, "{\"error\":\"bad\"}");
        Should.NotThrow(() => response.ThrowOnSendFailure());
    }
}
