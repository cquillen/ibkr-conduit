using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Errors;
using IbkrConduit.Health;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Session;

public class TokenRefreshHandlerTests
{
    [Fact]
    public async Task SendAsync_SuccessfulResponse_ReturnsWithoutRetry()
    {
        var sessionManager = new FakeSessionManager();
        var callCount = 0;
        var innerHandler = new FakeInnerHandler(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var handler = new TokenRefreshHandler(sessionManager, new SessionHealthState(), NullLogger<TokenRefreshHandler>.Instance)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.ibkr.com"),
        };

        var response = await client.GetAsync("/v1/api/portfolio/accounts", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        callCount.ShouldBe(1);
        sessionManager.ReauthCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task SendAsync_401Response_TriggersReauthAndRetries()
    {
        var sessionManager = new FakeSessionManager();
        var callCount = 0;
        var innerHandler = new FakeInnerHandler(_ =>
        {
            callCount++;
            if (callCount == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };
        });

        var handler = new TokenRefreshHandler(sessionManager, new SessionHealthState(), NullLogger<TokenRefreshHandler>.Instance)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.ibkr.com"),
        };

        var response = await client.GetAsync("/v1/api/portfolio/accounts", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        callCount.ShouldBe(2);
        sessionManager.ReauthCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task SendAsync_401OnTickle_DoesNotRetry()
    {
        var sessionManager = new FakeSessionManager();
        var callCount = 0;
        var innerHandler = new FakeInnerHandler(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        });

        var handler = new TokenRefreshHandler(sessionManager, new SessionHealthState(), NullLogger<TokenRefreshHandler>.Instance)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.ibkr.com"),
        };

        var response = await client.PostAsync("/v1/api/tickle", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        callCount.ShouldBe(1);
        sessionManager.ReauthCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task SendAsync_401OnRetry_ReturnsUnauthorizedResponse()
    {
        var sessionManager = new FakeSessionManager();
        var callCount = 0;
        var innerHandler = new FakeInnerHandler(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        });

        var handler = new TokenRefreshHandler(sessionManager, new SessionHealthState(), NullLogger<TokenRefreshHandler>.Instance)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.ibkr.com"),
        };

        var response = await client.GetAsync("/v1/api/portfolio/accounts", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        callCount.ShouldBe(2); // original + one retry
        sessionManager.ReauthCallCount.ShouldBe(1); // only one re-auth
    }

    [Fact]
    public async Task SendAsync_ReauthThrows_WrapsInSessionException()
    {
        var sessionManager = new FakeSessionManager
        {
            ThrowOnReauth = new InvalidOperationException("DH exchange failed"),
        };
        var callCount = 0;
        var innerHandler = new FakeInnerHandler(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        });

        var handler = new TokenRefreshHandler(sessionManager, new SessionHealthState(), NullLogger<TokenRefreshHandler>.Instance)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.ibkr.com"),
        };

        var ex = await Should.ThrowAsync<IbkrApiException>(
            client.GetAsync("/v1/api/portfolio/accounts", TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Re-authentication failed");
        ex.Error.ShouldBeOfType<IbkrSessionError>();
        ex.InnerException.ShouldNotBeNull();
        ex.InnerException.ShouldBeOfType<InvalidOperationException>();
        ex.InnerException!.Message.ShouldBe("DH exchange failed");
        callCount.ShouldBe(1); // only original call, no retry after failed re-auth
        sessionManager.ReauthCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task SendAsync_WithRequestBody_RetryPreservesBody()
    {
        var sessionManager = new FakeSessionManager();
        var callCount = 0;
        string? secondRequestBody = null;
        var innerHandler = new FakeInnerHandler(async req =>
        {
            callCount++;
            if (callCount == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            secondRequestBody = req.Content != null
                ? await req.Content.ReadAsStringAsync()
                : null;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var handler = new TokenRefreshHandler(sessionManager, new SessionHealthState(), NullLogger<TokenRefreshHandler>.Instance)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.ibkr.com"),
        };

        var content = new StringContent("""{"publish":true,"compete":true}""", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/v1/api/iserver/auth/ssodh/init", content, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        secondRequestBody.ShouldBe("""{"publish":true,"compete":true}""");
    }

    [Fact]
    public async Task SendAsync_ReauthThrowsCompetingSessionError_PropagatesIsCompeting()
    {
        // GAP3-1: the re-auth surfaced a competing session error (ADR-0004 authenticated=false path);
        // the handler must propagate IsCompeting=true, not the old hardcoded false.
        var sessionManager = new FakeSessionManager
        {
            ThrowOnReauth = new IbkrApiException(new IbkrSessionError(
                HttpStatusCode.Unauthorized, "authenticated=false", null, "/ssodh", IsCompeting: true)),
        };
        var innerHandler = new FakeInnerHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var handler = new TokenRefreshHandler(sessionManager, new SessionHealthState(), NullLogger<TokenRefreshHandler>.Instance)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.ibkr.com") };

        var ex = await Should.ThrowAsync<IbkrApiException>(
            client.GetAsync("/v1/api/portfolio/accounts", TestContext.Current.CancellationToken));

        ex.Error.ShouldBeOfType<IbkrSessionError>().IsCompeting.ShouldBeTrue();
    }

    [Fact]
    public async Task SendAsync_ReauthThrowsWithCompetingHealth_PropagatesIsCompetingFromHealth()
    {
        // The re-auth threw a non-session exception, but the health snapshot (fed by a tickle/sts)
        // shows the session is competing — the surfaced error must reflect that.
        var health = new SessionHealthState();
        health.MarkCompeting();
        var sessionManager = new FakeSessionManager
        {
            ThrowOnReauth = new InvalidOperationException("transport blip"),
        };
        var innerHandler = new FakeInnerHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var handler = new TokenRefreshHandler(sessionManager, health, NullLogger<TokenRefreshHandler>.Instance)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.ibkr.com") };

        var ex = await Should.ThrowAsync<IbkrApiException>(
            client.GetAsync("/v1/api/portfolio/accounts", TestContext.Current.CancellationToken));

        ex.Error.ShouldBeOfType<IbkrSessionError>().IsCompeting.ShouldBeTrue();
    }

    [Fact]
    public async Task SendAsync_PostRetryStillUnauthorizedWithCompetingHealth_ThrowsCompetingSessionError()
    {
        // GAP3-1: re-auth "succeeds" but the retried request is still 401 and health shows competing —
        // surface a competing session error rather than laundering it as a generic 401 for the facade.
        var health = new SessionHealthState();
        health.MarkCompeting();
        var sessionManager = new FakeSessionManager();
        var innerHandler = new FakeInnerHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var handler = new TokenRefreshHandler(sessionManager, health, NullLogger<TokenRefreshHandler>.Instance)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.ibkr.com") };

        var ex = await Should.ThrowAsync<IbkrApiException>(
            client.GetAsync("/v1/api/portfolio/accounts", TestContext.Current.CancellationToken));

        ex.Error.ShouldBeOfType<IbkrSessionError>().IsCompeting.ShouldBeTrue();
        sessionManager.ReauthCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task SendAsync_PostRetryStillUnauthorizedNoCompeting_ReturnsResponseUnchanged()
    {
        // Without competing evidence, a post-retry 401 is still returned to the facade for
        // interpretation (e.g., invalid account id) — behavior preserved.
        var sessionManager = new FakeSessionManager();
        var innerHandler = new FakeInnerHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var handler = new TokenRefreshHandler(sessionManager, new SessionHealthState(), NullLogger<TokenRefreshHandler>.Instance)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.ibkr.com") };

        var response = await client.GetAsync("/v1/api/portfolio/accounts", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        sessionManager.ReauthCallCount.ShouldBe(1);
    }

    private class FakeSessionManager : ISessionManager
    {
        public int ReauthCallCount { get; private set; }
        public Exception? ThrowOnReauth { get; init; }

        public Task EnsureInitializedAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ReauthenticateAsync(CancellationToken cancellationToken)
        {
            ReauthCallCount++;
            if (ThrowOnReauth != null)
            {
                throw ThrowOnReauth;
            }

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private class FakeInnerHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public FakeInnerHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
            _handler = req => Task.FromResult(handler(req));

        public FakeInnerHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) =>
            _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            _handler(request);
    }
}
