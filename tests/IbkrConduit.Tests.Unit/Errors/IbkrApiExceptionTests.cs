using System;
using System.Net;
using IbkrConduit.Errors;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Errors;

public class IbkrApiExceptionTests
{
    [Fact]
    public void Constructor_SetsErrorAndMessage()
    {
        var error = new IbkrApiError(HttpStatusCode.BadRequest, "bad input", "{}", "/test");
        var ex = new IbkrApiException(error);
        ex.Error.ShouldBe(error);
        ex.Message.ShouldBe("bad input");
    }

    [Fact]
    public void Constructor_WithInnerException_SetsAll()
    {
        var error = new IbkrApiError(HttpStatusCode.InternalServerError, "fail", "", "/test");
        var inner = new InvalidOperationException("inner");
        var ex = new IbkrApiException(error, inner);
        ex.Error.ShouldBe(error);
        ex.InnerException.ShouldBe(inner);
    }

    [Fact]
    public void PatternMatching_OnError_Works()
    {
        var error = new IbkrRateLimitError(HttpStatusCode.TooManyRequests, "slow", "", "/test", TimeSpan.FromSeconds(5));
        var ex = new IbkrApiException(error);
        var delay = ex.Error switch
        {
            IbkrRateLimitError rle => rle.RetryAfter,
            _ => null
        };
        delay.ShouldBe(TimeSpan.FromSeconds(5));
    }
}
