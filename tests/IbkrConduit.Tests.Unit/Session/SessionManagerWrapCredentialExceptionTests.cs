using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using IbkrConduit.Errors;
using IbkrConduit.Session;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Session;

public class SessionManagerWrapCredentialExceptionTests
{
    [Fact]
    public void HttpRequestException_500_ReturnsTransientException()
    {
        var ex = new HttpRequestException("server error", null, HttpStatusCode.InternalServerError);

        var result = SessionManager.WrapCredentialException(ex);

        result.ShouldBeOfType<IbkrTransientException>();
        result.InnerException.ShouldBe(ex);
    }

    [Fact]
    public void HttpRequestException_503_ReturnsTransientException()
    {
        var ex = new HttpRequestException("service unavailable", null, HttpStatusCode.ServiceUnavailable);

        var result = SessionManager.WrapCredentialException(ex);

        result.ShouldBeOfType<IbkrTransientException>();
    }

    [Fact]
    public void HttpRequestException_429_ReturnsTransientException()
    {
        var ex = new HttpRequestException("rate limited", null, HttpStatusCode.TooManyRequests);

        var result = SessionManager.WrapCredentialException(ex);

        result.ShouldBeOfType<IbkrTransientException>();
    }

    [Fact]
    public void HttpRequestException_401_ReturnsConfigurationException()
    {
        var ex = new HttpRequestException("unauthorized", null, HttpStatusCode.Unauthorized);

        var result = SessionManager.WrapCredentialException(ex);

        result.ShouldBeOfType<IbkrConfigurationException>();
    }

    [Fact]
    public void HttpRequestException_403_ReturnsConfigurationException()
    {
        var ex = new HttpRequestException("forbidden", null, HttpStatusCode.Forbidden);

        var result = SessionManager.WrapCredentialException(ex);

        result.ShouldBeOfType<IbkrConfigurationException>();
    }

    [Fact]
    public void HttpRequestException_NullStatus_ReturnsTransientException()
    {
        // Behavior change: previously classified as IbkrConfigurationException("BaseUrl").
        // Now classified as transient because the reconnect path is the load-bearing scenario;
        // a misconfigured BaseUrl at startup is diagnosable from the inner HttpRequestException.
        var ex = new HttpRequestException("connection refused", new Exception("inner"));

        var result = SessionManager.WrapCredentialException(ex);

        result.ShouldBeOfType<IbkrTransientException>();
    }

    [Fact]
    public void TaskCanceledException_ReturnsTransientException()
    {
        // Reaches the classifier only if the caller's CancellationToken was NOT canceled
        // (the call sites filter caller-cancellation before calling WrapCredentialException).
        // So this represents a per-request HTTP timeout, not a user-initiated cancel.
        var ex = new TaskCanceledException("per-request timeout");

        var result = SessionManager.WrapCredentialException(ex);

        result.ShouldBeOfType<IbkrTransientException>();
    }

    [Fact]
    public void CryptographicException_DecryptMessage_ReturnsConfigurationException()
    {
        var ex = new CryptographicException("decrypt failed");

        var result = SessionManager.WrapCredentialException(ex);

        result.ShouldBeOfType<IbkrConfigurationException>();
    }

    [Fact]
    public void CryptographicException_SignMessage_ReturnsConfigurationException()
    {
        var ex = new CryptographicException("sign failed");

        var result = SessionManager.WrapCredentialException(ex);

        result.ShouldBeOfType<IbkrConfigurationException>();
    }

    [Fact]
    public void FormatException_ReturnsConfigurationException()
    {
        var ex = new FormatException("invalid format");

        var result = SessionManager.WrapCredentialException(ex);

        result.ShouldBeOfType<IbkrConfigurationException>();
    }

    [Fact]
    public void InvalidOperationException_ReturnsConfigurationException()
    {
        var ex = new InvalidOperationException("DH failure");

        var result = SessionManager.WrapCredentialException(ex);

        result.ShouldBeOfType<IbkrConfigurationException>();
    }

    [Fact]
    public void JsonException_ReturnsConfigurationException()
    {
        var ex = new JsonException("parse failed");

        var result = SessionManager.WrapCredentialException(ex);

        result.ShouldBeOfType<IbkrConfigurationException>();
    }

    [Fact]
    public void UnknownException_ReturnsConfigurationException()
    {
        var ex = new ApplicationException("mystery");

        var result = SessionManager.WrapCredentialException(ex);

        result.ShouldBeOfType<IbkrConfigurationException>();
    }
}
