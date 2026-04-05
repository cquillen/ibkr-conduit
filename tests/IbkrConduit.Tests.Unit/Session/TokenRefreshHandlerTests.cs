using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Errors;
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

        var handler = new TokenRefreshHandler(sessionManager, NullLogger<TokenRefreshHandler>.Instance)
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

        var handler = new TokenRefreshHandler(sessionManager, NullLogger<TokenRefreshHandler>.Instance)
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

        var handler = new TokenRefreshHandler(sessionManager, NullLogger<TokenRefreshHandler>.Instance)
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

        var handler = new TokenRefreshHandler(sessionManager, NullLogger<TokenRefreshHandler>.Instance)
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

        var handler = new TokenRefreshHandler(sessionManager, NullLogger<TokenRefreshHandler>.Instance)
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

        var handler = new TokenRefreshHandler(sessionManager, NullLogger<TokenRefreshHandler>.Instance)
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
