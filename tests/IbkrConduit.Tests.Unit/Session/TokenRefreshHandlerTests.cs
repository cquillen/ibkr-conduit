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

    // --- ADR-0003 order-mutating POST replay gate (AMB-2) ---

    [Theory]
    [InlineData("/v1/api/iserver/account/DU1234567/orders")]           // place
    [InlineData("/v1/api/iserver/account/DU1234567/order/473740665")]  // modify
    [InlineData("/v1/api/iserver/reply/test-reply-id")]                 // reply
    public async Task SendAsync_OrderMutatingPost401_DoesNotReplayAndMarksAmbiguous(string path)
    {
        var sessionManager = new FakeSessionManager();
        var callCount = 0;
        var innerHandler = new FakeInnerHandler(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("Unauthorized"),
            };
        });

        var handler = new TokenRefreshHandler(sessionManager, new SessionHealthState(), NullLogger<TokenRefreshHandler>.Instance)
        {
            InnerHandler = innerHandler,
        };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.ibkr.com") };

        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent("""{"orders":[]}"""),
        };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        callCount.ShouldBe(1, "the order-mutating POST must be sent exactly once — never replayed");
        sessionManager.ReauthCallCount.ShouldBe(1, "re-authentication still happens after the 401");

        request.Options.TryGetValue(
            new HttpRequestOptionsKey<AmbiguousOrderOutcome>(AmbiguousOrderOutcome.OptionKey),
            out var outcome).ShouldBeTrue("the request must carry the ambiguous-outcome marker");
        outcome!.OriginalStatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        outcome.ReauthSucceeded.ShouldBeTrue();
        outcome.Endpoint.ShouldBe(path);
    }

    [Fact]
    public async Task SendAsync_OrderPost401_ReauthFails_MarksAmbiguousReauthFalse_DoesNotThrow()
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
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.ibkr.com") };

        using var request = new HttpRequestMessage(
            HttpMethod.Post, "/v1/api/iserver/account/DU1234567/orders")
        {
            Content = new StringContent("""{"orders":[]}"""),
        };

        // Order POSTs never surface a session-exception throw — an ambiguous outcome is still ambiguous
        // when re-auth fails; the caller must reconcile, not treat it as a definitive refusal.
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        callCount.ShouldBe(1, "no replay even when re-auth fails");
        request.Options.TryGetValue(
            new HttpRequestOptionsKey<AmbiguousOrderOutcome>(AmbiguousOrderOutcome.OptionKey),
            out var outcome).ShouldBeTrue();
        outcome!.ReauthSucceeded.ShouldBeFalse("re-auth threw, so the marker records a failed re-auth");
    }

    [Fact]
    public async Task SendAsync_WhatIfPost401_Replays()
    {
        // /orders/whatif is a preview, not order-mutating — it keeps the idempotent replay behavior.
        var sessionManager = new FakeSessionManager();
        var callCount = 0;
        var innerHandler = new FakeInnerHandler(_ =>
        {
            callCount++;
            return callCount == 1
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                };
        });

        var handler = new TokenRefreshHandler(sessionManager, new SessionHealthState(), NullLogger<TokenRefreshHandler>.Instance)
        {
            InnerHandler = innerHandler,
        };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.ibkr.com") };

        using var request = new HttpRequestMessage(
            HttpMethod.Post, "/v1/api/iserver/account/DU1234567/orders/whatif")
        {
            Content = new StringContent("""{"orders":[]}"""),
        };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        callCount.ShouldBe(2, "whatif is not order-mutating — it replays and succeeds");
        request.Options.TryGetValue(
            new HttpRequestOptionsKey<AmbiguousOrderOutcome>(AmbiguousOrderOutcome.OptionKey),
            out _).ShouldBeFalse("whatif is not gated — no ambiguous marker");
    }

    [Fact]
    public async Task SendAsync_LiveOrdersGet401_Replays()
    {
        // GET is idempotent — the live-orders GET keeps replay-and-succeed.
        var sessionManager = new FakeSessionManager();
        var callCount = 0;
        var innerHandler = new FakeInnerHandler(_ =>
        {
            callCount++;
            return callCount == 1
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"orders":[]}""", Encoding.UTF8, "application/json"),
                };
        });

        var handler = new TokenRefreshHandler(sessionManager, new SessionHealthState(), NullLogger<TokenRefreshHandler>.Instance)
        {
            InnerHandler = innerHandler,
        };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.ibkr.com") };

        var response = await client.GetAsync(
            "/v1/api/iserver/account/orders", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        callCount.ShouldBe(2, "the idempotent live-orders GET still replays");
    }

    // --- ERR-1: retry-leg captured-body plumbing (response.RequestMessage identity) ---

    [Fact]
    public async Task SendAsync_401RetryLeg_ResponseRequestMessageIsOriginalRequest()
    {
        // ERR-1: after a 401 replay, the returned response's RequestMessage must be the ORIGINAL
        // request, not the internal clone. ResponseBodyCaptureHandler (outer to this handler) stashes
        // the retried body on the original request's Options; ResultFactory.GetCapturedBody reads
        // response.RequestMessage.Options — so unless the retry response points back at the original
        // request, hidden-error detection is silently disabled on the retry leg.
        var sessionManager = new FakeSessionManager();
        var callCount = 0;
        var innerHandler = new FakeInnerHandler(_ =>
        {
            callCount++;
            return callCount == 1
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                };
        });

        var handler = new TokenRefreshHandler(sessionManager, new SessionHealthState(), NullLogger<TokenRefreshHandler>.Instance)
        {
            InnerHandler = innerHandler,
        };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(
            HttpMethod.Get, "https://api.ibkr.com/v1/api/portfolio/accounts");

        var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        callCount.ShouldBe(2, "the idempotent GET replays after re-auth");
        response.RequestMessage.ShouldBeSameAs(request,
            "the retry response must carry the original request so its captured body is discoverable");
    }

    // --- SES-3: caller cancellation during re-auth must surface as cancellation ---

    [Fact]
    public async Task SendAsync_ConsumerCancelsDuringReauth_PropagatesOperationCanceled()
    {
        // SES-3: a non-order request whose caller-token cancels while ReauthenticateAsync is in flight
        // must surface OperationCanceledException — not a laundered IbkrApiException(IbkrSessionError),
        // which a consumer would misread as a definitive session-loss and trip a spurious recovery saga.
        using var cts = new CancellationTokenSource();
        var sessionManager = new FakeSessionManager
        {
            OnReauth = cts.Cancel,
            ThrowOnReauth = new OperationCanceledException(),
        };
        var innerHandler = new FakeInnerHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var handler = new TokenRefreshHandler(sessionManager, new SessionHealthState(), NullLogger<TokenRefreshHandler>.Instance)
        {
            InnerHandler = innerHandler,
        };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(
            HttpMethod.Get, "https://api.ibkr.com/v1/api/portfolio/accounts");

        await Should.ThrowAsync<OperationCanceledException>(
            invoker.SendAsync(request, cts.Token));
        sessionManager.ReauthCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task SendAsync_OrderPost401_ConsumerCancelsDuringReauth_StillMarksAmbiguous()
    {
        // SES-3 x ADR-0003: for an order-mutating POST, a caller-cancelled re-auth does NOT preempt the
        // ambiguous outcome — the order was sent and its result is genuinely unknown, so the marker must
        // still be set (cancellation must not become the reported outcome and hide the reconcile signal).
        using var cts = new CancellationTokenSource();
        var sessionManager = new FakeSessionManager
        {
            OnReauth = cts.Cancel,
            ThrowOnReauth = new OperationCanceledException(),
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
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(
            HttpMethod.Post, "https://api.ibkr.com/v1/api/iserver/account/DU1234567/orders")
        {
            Content = new StringContent("""{"orders":[]}"""),
        };

        var response = await invoker.SendAsync(request, cts.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        callCount.ShouldBe(1, "no replay for an order-mutating POST, even on a cancelled re-auth");
        request.Options.TryGetValue(
            new HttpRequestOptionsKey<AmbiguousOrderOutcome>(AmbiguousOrderOutcome.OptionKey),
            out var outcome).ShouldBeTrue("the ambiguous marker must survive a caller-cancelled re-auth");
        outcome!.ReauthSucceeded.ShouldBeFalse("re-auth was cancelled, so the marker records a failed re-auth");
    }

    private class FakeSessionManager : ISessionManager
    {
        public int ReauthCallCount { get; private set; }
        public Exception? ThrowOnReauth { get; init; }

        /// <summary>
        /// Optional hook run at the start of <see cref="ReauthenticateAsync"/>, before any throw —
        /// used to simulate the caller's own token cancelling mid-reauth (cancel a CTS here, then set
        /// <see cref="ThrowOnReauth"/> to an <see cref="OperationCanceledException"/>).
        /// </summary>
        public Action? OnReauth { get; init; }

        public bool SessionEstablished => true;

        public Task EnsureInitializedAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ReauthenticateAsync(CancellationToken cancellationToken)
        {
            ReauthCallCount++;
            OnReauth?.Invoke();
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
