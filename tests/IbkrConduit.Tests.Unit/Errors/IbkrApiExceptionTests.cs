using System;
using System.Net;
using IbkrConduit.Errors;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Errors;

public class IbkrApiExceptionTests
{
    [Fact]
    public void IbkrApiException_CarriesAllProperties()
    {
        var ex = new IbkrApiException(
            HttpStatusCode.BadRequest, "bad param", """{"error":"bad param"}""", "/v1/api/test");

        ex.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ex.ErrorMessage.ShouldBe("bad param");
        ex.RawResponseBody.ShouldBe("""{"error":"bad param"}""");
        ex.RequestUri.ShouldBe("/v1/api/test");
        ex.Message.ShouldContain("bad param");
    }

    [Fact]
    public void IbkrApiException_NullErrorMessage_UsesDefaultMessage()
    {
        var ex = new IbkrApiException(HttpStatusCode.InternalServerError, null, "<html>error</html>", "/v1/api/test");

        ex.ErrorMessage.ShouldBeNull();
        ex.RawResponseBody.ShouldBe("<html>error</html>");
        ex.Message.ShouldContain("500");
    }

    [Fact]
    public void IbkrRateLimitException_InheritsFromBase()
    {
        var ex = new IbkrRateLimitException(
            TimeSpan.FromSeconds(5), "rate limited", """{"error":"rate limited"}""", "/v1/api/test");

        ex.RetryAfter.ShouldBe(TimeSpan.FromSeconds(5));
        ex.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        ex.ShouldBeAssignableTo<IbkrApiException>();
    }

    [Fact]
    public void IbkrRateLimitException_NullRetryAfter_Allowed()
    {
        var ex = new IbkrRateLimitException(
            null, "rate limited", """{"error":"rate limited"}""", "/v1/api/test");

        ex.RetryAfter.ShouldBeNull();
    }

    [Fact]
    public void IbkrSessionException_CarriesExtraProperties()
    {
        var ex = new IbkrSessionException(
            true, "competing session", HttpStatusCode.Unauthorized, "session dead",
            """{"authenticated":false}""", "/v1/api/iserver/auth/status");

        ex.IsCompeting.ShouldBeTrue();
        ex.Reason.ShouldBe("competing session");
        ex.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        ex.ShouldBeAssignableTo<IbkrApiException>();
    }

    [Fact]
    public void IbkrOrderRejectedException_CarriesRejectionMessage()
    {
        var ex = new IbkrOrderRejectedException(
            "insufficient funds", """{"error":"insufficient funds"}""", "/v1/api/iserver/account/DU123/orders");

        ex.RejectionMessage.ShouldBe("insufficient funds");
        ex.StatusCode.ShouldBe(HttpStatusCode.OK);
        ex.ShouldBeAssignableTo<IbkrApiException>();
    }

    [Fact]
    public void AllExceptions_CatchableAsIbkrApiException()
    {
        var exceptions = new IbkrApiException[]
        {
            new(HttpStatusCode.BadRequest, "test", "{}", "/test"),
            new IbkrRateLimitException(null, "test", "{}", "/test"),
            new IbkrSessionException(false, null, HttpStatusCode.Unauthorized, "test", "{}", "/test"),
            new IbkrOrderRejectedException("test", "{}", "/test"),
        };

        foreach (var ex in exceptions)
        {
            ex.ShouldBeAssignableTo<IbkrApiException>();
        }
    }
}
