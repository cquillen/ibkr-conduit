using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Errors;
using IbkrConduit.Session;
using Refit;
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
    public void LiveSessionTokenValidationException_NamesImplicatedCredentialFields_NotSigning()
    {
        // AUT-4: an LST-validation failure implicates the fields that derive/validate the token —
        // ConsumerKey, EncryptionPrivateKey (via the decrypted access-token secret), and DhPrime.
        // It must NOT be classified as an RSA "signing" failure: the signing key only signs the LST
        // *request*, and a bad signing key is rejected upstream as an HTTP 401 — it never reaches
        // the local signature comparison. The message string "...signature..." would otherwise match
        // the generic "sign" branch and point the operator at the wrong credential.
        var ex = new LiveSessionTokenValidationException(
            "Live Session Token validation failed: computed signature does not match server's signature.");

        var result = SessionManager.WrapCredentialException(ex);

        var config = result.ShouldBeOfType<IbkrConfigurationException>();
        config.InnerException.ShouldBeSameAs(ex);
        config.CredentialHint.ShouldNotBeNull();
        config.CredentialHint!.ShouldContain("ConsumerKey");
        config.CredentialHint.ShouldContain("EncryptionPrivateKey");
        config.CredentialHint.ShouldContain("DhPrime");
        config.CredentialHint.ShouldNotContain("SignaturePrivateKey");
        config.Message.ShouldNotContain("RSA signature failed");
        config.Message.ShouldContain("Live Session Token");
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

    // FO-3 / ADR-0007: the ssodh/init raw Task<T> path throws a Refit ApiException, whose base
    // HttpRequestException.StatusCode is left unset in Refit 12 — so it must be classified by
    // ApiException.StatusCode, uniformly with the raw-HttpRequestException path above.

    [Fact]
    public async Task ApiException_500_ReturnsTransientException()
    {
        var ex = await CreateApiException(HttpStatusCode.InternalServerError);

        var result = SessionManager.WrapCredentialException(ex);

        result.ShouldBeOfType<IbkrTransientException>();
        result.InnerException.ShouldBe(ex);
    }

    [Fact]
    public async Task ApiException_503_ReturnsTransientException()
    {
        var ex = await CreateApiException(HttpStatusCode.ServiceUnavailable);

        var result = SessionManager.WrapCredentialException(ex);

        result.ShouldBeOfType<IbkrTransientException>();
    }

    [Fact]
    public async Task ApiException_429_ReturnsTransientException()
    {
        var ex = await CreateApiException(HttpStatusCode.TooManyRequests);

        var result = SessionManager.WrapCredentialException(ex);

        result.ShouldBeOfType<IbkrTransientException>();
    }

    [Fact]
    public async Task ApiException_401_ReturnsConfigurationException()
    {
        var ex = await CreateApiException(HttpStatusCode.Unauthorized);

        var result = SessionManager.WrapCredentialException(ex);

        var config = result.ShouldBeOfType<IbkrConfigurationException>();
        config.CredentialHint.ShouldNotBeNull();
        config.CredentialHint!.ShouldContain("ConsumerKey");
        config.CredentialHint.ShouldContain("AccessToken");
    }

    [Fact]
    public async Task ApiException_403_ReturnsConfigurationException()
    {
        var ex = await CreateApiException(HttpStatusCode.Forbidden);

        var result = SessionManager.WrapCredentialException(ex);

        result.ShouldBeOfType<IbkrConfigurationException>();
    }

    [Fact]
    public async Task ApiException_400_ReturnsConfigurationException()
    {
        var ex = await CreateApiException(HttpStatusCode.BadRequest);

        var result = SessionManager.WrapCredentialException(ex);

        var config = result.ShouldBeOfType<IbkrConfigurationException>();
        config.CredentialHint.ShouldNotBeNull();
        config.CredentialHint!.ShouldContain("ConsumerKey");
        config.CredentialHint.ShouldContain("AccessToken");
    }

    [Fact]
    public async Task ApiException_404_ReturnsConfigurationException()
    {
        var ex = await CreateApiException(HttpStatusCode.NotFound);

        var result = SessionManager.WrapCredentialException(ex);

        result.ShouldBeOfType<IbkrConfigurationException>();
    }

    /// <summary>
    /// Builds a Refit <see cref="ApiException"/> for the given status code, exactly as Refit surfaces a
    /// non-success HTTP response from a raw <c>Task&lt;T&gt;</c> session call (e.g. ssodh/init).
    /// </summary>
    private static async Task<ApiException> CreateApiException(HttpStatusCode statusCode)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, "https://api.ibkr.com/v1/api/iserver/auth/ssodh/init");
        using var response = new HttpResponseMessage(statusCode);
        return await ApiException.Create(request, HttpMethod.Post, response, new RefitSettings());
    }
}
