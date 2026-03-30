# Implementation Plan: Milestone 3a — Rate Limiting + Resilience

**Date:** 2026-03-30
**Spec:** `docs/superpowers/specs/2026-03-30-milestone3a-rate-limiting-resilience-design.md`
**Branch:** `feat/m3a-rate-limiting-resilience`

---

## Goal

Add rate limiting and resilience to the HTTP pipeline so requests respect IBKR's rate limits and recover from transient failures. Three new `DelegatingHandler` implementations slot into both consumer and session API pipelines.

## Architecture

```
Consumer API:
  Refit -> TokenRefreshHandler -> ResilienceHandler -> GlobalRateLimitingHandler -> EndpointRateLimitingHandler -> OAuthSigningHandler -> HttpClient

Session API:
  Refit -> ResilienceHandler -> GlobalRateLimitingHandler -> EndpointRateLimitingHandler -> OAuthSigningHandler -> HttpClient
```

## Tech Stack

- **Polly.Core 8.6.6** — `ResiliencePipelineBuilder<T>`, `RetryStrategyOptions<T>` for retry with exponential backoff + jitter
- **System.Threading.RateLimiting** — in-box on .NET 8+; `TokenBucketRateLimiter` for global and per-endpoint limits
- **xUnit v3 + Shouldly** — unit and integration tests
- **WireMock.Net** — HTTP-level integration tests

## File Structure

| File | Type | Task |
|---|---|---|
| `src/IbkrConduit/Http/RateLimitRejectedException.cs` | New | 3a.1 |
| `Directory.Packages.props` | Modified | 3a.1 |
| `src/IbkrConduit/IbkrConduit.csproj` | Modified | 3a.1 |
| `src/IbkrConduit/Http/GlobalRateLimitingHandler.cs` | New | 3a.2 |
| `tests/IbkrConduit.Tests.Unit/Http/GlobalRateLimitingHandlerTests.cs` | New | 3a.2 |
| `src/IbkrConduit/Http/EndpointRateLimitingHandler.cs` | New | 3a.3 |
| `tests/IbkrConduit.Tests.Unit/Http/EndpointRateLimitingHandlerTests.cs` | New | 3a.3 |
| `src/IbkrConduit/Http/ResilienceHandler.cs` | New | 3a.4 |
| `tests/IbkrConduit.Tests.Unit/Http/ResilienceHandlerTests.cs` | New | 3a.4 |
| `src/IbkrConduit/Http/ServiceCollectionExtensions.cs` | Modified | 3a.5 |
| `tests/IbkrConduit.Tests.Unit/Http/ServiceCollectionExtensionsTests.cs` | Modified | 3a.5 |
| `tests/IbkrConduit.Tests.Integration/Http/RateLimitingAndResilienceTests.cs` | New | 3a.6 |

---

## Task 3a.1 — RateLimitRejectedException + NuGet Dependencies

### Files
- `src/IbkrConduit/Http/RateLimitRejectedException.cs` (new)
- `Directory.Packages.props` (modified)
- `src/IbkrConduit/IbkrConduit.csproj` (modified)

### Steps

- [ ] Add `Polly.Core` 8.6.6 to `Directory.Packages.props`
- [ ] Add `<PackageReference Include="Polly.Core" />` to `IbkrConduit.csproj`
- [ ] Create `RateLimitRejectedException` with two constructors
- [ ] Run `dotnet build --configuration Release` — verify zero warnings
- [ ] Commit: `feat: add RateLimitRejectedException and Polly.Core dependency`

### Code

**`src/IbkrConduit/Http/RateLimitRejectedException.cs`:**
```csharp
using System;

namespace IbkrConduit.Http;

/// <summary>
/// Thrown when a rate limiter queue is full and cannot accept more requests.
/// </summary>
public class RateLimitRejectedException : Exception
{
    /// <summary>
    /// Creates a new instance with the specified message.
    /// </summary>
    public RateLimitRejectedException(string message) : base(message) { }

    /// <summary>
    /// Creates a new instance with the specified message and inner exception.
    /// </summary>
    public RateLimitRejectedException(string message, Exception innerException)
        : base(message, innerException) { }
}
```

---

## Task 3a.2 — GlobalRateLimitingHandler

### Files
- `src/IbkrConduit/Http/GlobalRateLimitingHandler.cs` (new)
- `tests/IbkrConduit.Tests.Unit/Http/GlobalRateLimitingHandlerTests.cs` (new)

### Steps

- [ ] RED: Write `GlobalRateLimitingHandlerTests` with tests for pass-through, rejection, and lease disposal
- [ ] Run tests — verify they fail (class does not exist)
- [ ] GREEN: Create `GlobalRateLimitingHandler` — acquire token, throw on rejection, delegate to inner handler
- [ ] Run tests — verify they pass
- [ ] REFACTOR: Clean up if needed
- [ ] Run `dotnet build --configuration Release` — zero warnings
- [ ] Commit: `feat: add GlobalRateLimitingHandler with token bucket rate limiting`

### Code

**`src/IbkrConduit/Http/GlobalRateLimitingHandler.cs`:**
```csharp
using System.Net.Http;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace IbkrConduit.Http;

/// <summary>
/// DelegatingHandler that enforces a global token bucket rate limit per tenant.
/// Requests wait asynchronously for a token; if the queue is full, a
/// <see cref="RateLimitRejectedException"/> is thrown.
/// </summary>
internal sealed class GlobalRateLimitingHandler : DelegatingHandler
{
    private readonly RateLimiter _limiter;

    public GlobalRateLimitingHandler(RateLimiter limiter)
    {
        _limiter = limiter;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var lease = await _limiter.AcquireAsync(1, cancellationToken);
        if (!lease.IsAcquired)
        {
            throw new RateLimitRejectedException(
                "Global rate limit exceeded — queue is full.");
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
```

**`tests/IbkrConduit.Tests.Unit/Http/GlobalRateLimitingHandlerTests.cs`:**
```csharp
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using IbkrConduit.Http;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

public class GlobalRateLimitingHandlerTests
{
    [Fact]
    public async Task SendAsync_WhenTokenAvailable_ForwardsRequest()
    {
        using var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = 10,
            AutoReplenishment = true,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });

        var handler = new GlobalRateLimitingHandler(limiter)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK),
        };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync("http://localhost/test");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendAsync_WhenQueueFull_ThrowsRateLimitRejectedException()
    {
        using var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 1,
            ReplenishmentPeriod = TimeSpan.FromHours(1),
            TokensPerPeriod = 1,
            AutoReplenishment = false,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });

        var handler = new GlobalRateLimitingHandler(limiter)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK),
        };

        using var client = new HttpClient(handler);

        // Consume the single token
        await client.GetAsync("http://localhost/test");

        // Next request should be rejected
        await Should.ThrowAsync<RateLimitRejectedException>(
            () => client.GetAsync("http://localhost/test"));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public StubHandler(HttpStatusCode statusCode) => _statusCode = statusCode;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_statusCode));
    }
}
```

---

## Task 3a.3 — EndpointRateLimitingHandler

### Files
- `src/IbkrConduit/Http/EndpointRateLimitingHandler.cs` (new)
- `tests/IbkrConduit.Tests.Unit/Http/EndpointRateLimitingHandlerTests.cs` (new)

### Steps

- [ ] RED: Write tests for matched endpoint limiting, unmatched pass-through, and queue rejection
- [ ] Run tests — verify they fail
- [ ] GREEN: Create `EndpointRateLimitingHandler` with path-contains matching
- [ ] Run tests — verify they pass
- [ ] REFACTOR: Clean up if needed
- [ ] Run `dotnet build --configuration Release` — zero warnings
- [ ] Commit: `feat: add EndpointRateLimitingHandler with per-endpoint rate limits`

### Code

**`src/IbkrConduit/Http/EndpointRateLimitingHandler.cs`:**
```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace IbkrConduit.Http;

/// <summary>
/// DelegatingHandler that enforces per-endpoint token bucket rate limits.
/// Matches request URLs against known path patterns and applies the
/// corresponding limiter. Unmatched URLs pass through without limiting.
/// </summary>
internal sealed class EndpointRateLimitingHandler : DelegatingHandler
{
    private readonly IReadOnlyDictionary<string, RateLimiter> _endpointLimiters;

    public EndpointRateLimitingHandler(IReadOnlyDictionary<string, RateLimiter> endpointLimiters)
    {
        _endpointLimiters = endpointLimiters;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var limiter = FindLimiter(request);

        if (limiter != null)
        {
            using var lease = await limiter.AcquireAsync(1, cancellationToken);
            if (!lease.IsAcquired)
            {
                throw new RateLimitRejectedException(
                    $"Endpoint rate limit exceeded for {request.RequestUri?.PathAndQuery} — queue is full.");
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private RateLimiter? FindLimiter(HttpRequestMessage request)
    {
        var path = request.RequestUri?.PathAndQuery;
        if (path == null)
        {
            return null;
        }

        foreach (var kvp in _endpointLimiters)
        {
            if (path.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }

        return null;
    }
}
```

**`tests/IbkrConduit.Tests.Unit/Http/EndpointRateLimitingHandlerTests.cs`:**
```csharp
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using IbkrConduit.Http;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

public class EndpointRateLimitingHandlerTests : IDisposable
{
    private readonly TokenBucketRateLimiter _ordersLimiter;
    private readonly Dictionary<string, RateLimiter> _limiters;

    public EndpointRateLimitingHandlerTests()
    {
        _ordersLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 1,
            ReplenishmentPeriod = TimeSpan.FromHours(1),
            TokensPerPeriod = 1,
            AutoReplenishment = false,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });

        _limiters = new Dictionary<string, RateLimiter>
        {
            ["/iserver/account/orders"] = _ordersLimiter,
        };
    }

    [Fact]
    public async Task SendAsync_MatchedEndpoint_AcquiresToken()
    {
        var handler = new EndpointRateLimitingHandler(_limiters)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK),
        };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync("http://localhost/v1/api/iserver/account/orders");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendAsync_MatchedEndpoint_WhenQueueFull_ThrowsRateLimitRejectedException()
    {
        var handler = new EndpointRateLimitingHandler(_limiters)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK),
        };

        using var client = new HttpClient(handler);

        // Consume the single token
        await client.GetAsync("http://localhost/v1/api/iserver/account/orders");

        // Next request should be rejected
        await Should.ThrowAsync<RateLimitRejectedException>(
            () => client.GetAsync("http://localhost/v1/api/iserver/account/orders"));
    }

    [Fact]
    public async Task SendAsync_UnmatchedEndpoint_PassesThroughWithoutLimiting()
    {
        var handler = new EndpointRateLimitingHandler(_limiters)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK),
        };

        using var client = new HttpClient(handler);

        // Unmatched endpoint should always pass through
        for (var i = 0; i < 5; i++)
        {
            var response = await client.GetAsync("http://localhost/v1/api/some/other/endpoint");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    public void Dispose()
    {
        _ordersLimiter.Dispose();
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public StubHandler(HttpStatusCode statusCode) => _statusCode = statusCode;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_statusCode));
    }
}
```

---

## Task 3a.4 — ResilienceHandler

### Files
- `src/IbkrConduit/Http/ResilienceHandler.cs` (new)
- `tests/IbkrConduit.Tests.Unit/Http/ResilienceHandlerTests.cs` (new)

### Steps

- [ ] RED: Write tests for retry on 503, retry on 429, no retry on 400, success pass-through
- [ ] Run tests — verify they fail
- [ ] GREEN: Create `ResilienceHandler` wrapping `base.SendAsync` in `pipeline.ExecuteAsync()`
- [ ] Run tests — verify they pass
- [ ] REFACTOR: Clean up if needed
- [ ] Run `dotnet build --configuration Release` — zero warnings
- [ ] Commit: `feat: add ResilienceHandler with Polly retry for transient errors`

### Code

**`src/IbkrConduit/Http/ResilienceHandler.cs`:**
```csharp
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Polly;

namespace IbkrConduit.Http;

/// <summary>
/// DelegatingHandler that wraps outgoing HTTP requests in a Polly resilience
/// pipeline. Retries transient errors (5xx, 408, 429) with exponential backoff
/// and jitter. Non-retryable errors (4xx) pass through immediately.
/// </summary>
internal sealed class ResilienceHandler : DelegatingHandler
{
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

    public ResilienceHandler(ResiliencePipeline<HttpResponseMessage> pipeline)
    {
        _pipeline = pipeline;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        await _pipeline.ExecuteAsync(
            async ct => await base.SendAsync(request, ct),
            cancellationToken);
}
```

**`tests/IbkrConduit.Tests.Unit/Http/ResilienceHandlerTests.cs`:**
```csharp
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Http;
using Polly;
using Polly.Retry;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

public class ResilienceHandlerTests
{
    private static ResiliencePipeline<HttpResponseMessage> CreateTestPipeline() =>
        new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Constant,
                Delay = TimeSpan.Zero,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(r => (int)r.StatusCode >= 500)
                    .HandleResult(r => r.StatusCode == HttpStatusCode.RequestTimeout)
                    .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests),
            })
            .Build();

    [Fact]
    public async Task SendAsync_OnTransient503ThenSuccess_RetriesAndReturnsSuccess()
    {
        var innerHandler = new SequenceHandler(
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.OK);

        var handler = new ResilienceHandler(CreateTestPipeline())
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync("http://localhost/test");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        innerHandler.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task SendAsync_On429ThenSuccess_RetriesAndReturnsSuccess()
    {
        var innerHandler = new SequenceHandler(
            HttpStatusCode.TooManyRequests,
            HttpStatusCode.OK);

        var handler = new ResilienceHandler(CreateTestPipeline())
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync("http://localhost/test");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        innerHandler.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task SendAsync_On400_DoesNotRetry()
    {
        var innerHandler = new SequenceHandler(HttpStatusCode.BadRequest);

        var handler = new ResilienceHandler(CreateTestPipeline())
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync("http://localhost/test");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        innerHandler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task SendAsync_OnSuccess_PassesThroughDirectly()
    {
        var innerHandler = new SequenceHandler(HttpStatusCode.OK);

        var handler = new ResilienceHandler(CreateTestPipeline())
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync("http://localhost/test");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        innerHandler.CallCount.ShouldBe(1);
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode[] _responses;
        private int _callCount;

        public int CallCount => _callCount;

        public SequenceHandler(params HttpStatusCode[] responses) =>
            _responses = responses;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var index = Interlocked.Increment(ref _callCount) - 1;
            var statusCode = index < _responses.Length
                ? _responses[index]
                : _responses[^1];

            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }
}
```

---

## Task 3a.5 — Pipeline Wiring

### Files
- `src/IbkrConduit/Http/ServiceCollectionExtensions.cs` (modified)
- `tests/IbkrConduit.Tests.Unit/Http/ServiceCollectionExtensionsTests.cs` (modified)

### Steps

- [ ] RED: Add test verifying the new handlers are registered (build the service provider, resolve the Refit client)
- [ ] Run tests — verify they fail or existing tests still pass
- [ ] GREEN: Update `ServiceCollectionExtensions` to register singletons (rate limiters, resilience pipeline) and add handlers in correct order
- [ ] Run tests — verify they pass
- [ ] REFACTOR: Extract factory methods for rate limiter and pipeline configuration
- [ ] Run `dotnet build --configuration Release` — zero warnings
- [ ] Commit: `feat: wire rate limiting and resilience handlers into HTTP pipelines`

### Code

See implementation section — the `ServiceCollectionExtensions` is updated with singleton registrations and handler ordering.

---

## Task 3a.6 — Integration Tests

### Files
- `tests/IbkrConduit.Tests.Integration/Http/RateLimitingAndResilienceTests.cs` (new)

### Steps

- [ ] Write WireMock integration tests for: transient retry, 429 retry, non-retryable pass-through, rate limit rejection
- [ ] Run `dotnet test --configuration Release` — verify all pass
- [ ] Run `dotnet format --verify-no-changes` — verify lint clean
- [ ] Commit: `test: add integration tests for rate limiting and resilience pipeline`

---

## Dependency Graph

```
Task 3a.1 (exception + NuGet deps)
         |
         +----------+----------+
         v          v          v
Task 3a.2       Task 3a.3   Task 3a.4
(global)       (endpoint)  (resilience)
         |          |          |
         +----------+----------+
                    |
                    v
            Task 3a.5 (pipeline wiring)
                    |
                    v
            Task 3a.6 (integration tests)
```
