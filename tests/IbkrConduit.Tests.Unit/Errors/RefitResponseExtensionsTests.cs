using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using IbkrConduit.Errors;
using IbkrConduit.Tests.Unit.TestHelpers;
using Refit;
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

    [Fact]
    public void RethrowIfWrappedCancellation_WrappedCancellationAndTokenCancelled_RethrowsOriginal()
    {
        var inner = new OperationCanceledException("cancelled");
        var error = CreateSendFailure(inner);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var ex = Should.Throw<OperationCanceledException>(() => error.RethrowIfWrappedCancellation(cts.Token));
        ex.ShouldBeSameAs(inner);
    }

    [Fact]
    public void RethrowIfWrappedCancellation_WrappedCancellationButTokenNotCancelled_DoesNothing()
    {
        var inner = new OperationCanceledException("cancelled");
        var error = CreateSendFailure(inner);

        Should.NotThrow(() => error.RethrowIfWrappedCancellation(CancellationToken.None));
    }

    [Fact]
    public void RethrowIfWrappedCancellation_WrappedTransportErrorAndTokenCancelled_DoesNothing()
    {
        var inner = new HttpRequestException("dns failure");
        var error = CreateSendFailure(inner);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Should.NotThrow(() => error.RethrowIfWrappedCancellation(cts.Token));
    }

    private static ApiRequestException CreateSendFailure(Exception inner)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.ibkr.com/test");
        return new ApiRequestException(request, HttpMethod.Get, new RefitSettings(), inner);
    }
}
