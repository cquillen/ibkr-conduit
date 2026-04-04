using System;
using System.Net;
using IbkrConduit.Errors;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Errors;

public class IbkrErrorTests
{
    [Fact]
    public void IbkrApiError_CanBeCreated()
    {
        var error = new IbkrApiError(HttpStatusCode.BadRequest, "bad input", "{}", "/test");
        error.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        error.Message.ShouldBe("bad input");
        error.RawBody.ShouldBe("{}");
        error.RequestPath.ShouldBe("/test");
    }

    [Fact]
    public void IbkrSessionError_HasIsCompeting()
    {
        var error = new IbkrSessionError(HttpStatusCode.Unauthorized, "competing", "", "/auth", true);
        error.IsCompeting.ShouldBeTrue();
        (error is IbkrError).ShouldBeTrue();
    }

    [Fact]
    public void IbkrRateLimitError_HasRetryAfter()
    {
        var delay = TimeSpan.FromSeconds(30);
        var error = new IbkrRateLimitError(HttpStatusCode.TooManyRequests, "slow down", "", "/orders", delay);
        error.RetryAfter.ShouldBe(delay);
    }

    [Fact]
    public void IbkrOrderRejectedError_HasRejectionMessage()
    {
        var error = new IbkrOrderRejectedError("insufficient funds", "{\"error\":\"insufficient funds\"}", "/orders");
        error.RejectionMessage.ShouldBe("insufficient funds");
        error.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public void IbkrHiddenError_HasMessage()
    {
        var error = new IbkrHiddenError("some error", "{\"error\":\"some error\"}", "/test");
        error.Message.ShouldBe("some error");
        error.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public void IbkrError_PatternMatching_Works()
    {
        IbkrError error = new IbkrRateLimitError(HttpStatusCode.TooManyRequests, "slow", "", "/test", TimeSpan.FromSeconds(5));
        var matched = error switch
        {
            IbkrRateLimitError { RetryAfter: var delay } => delay?.TotalSeconds,
            _ => null
        };
        matched.ShouldBe(5);
    }
}
