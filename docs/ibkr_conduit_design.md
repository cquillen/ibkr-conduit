# IbkrConduit — Architecture & Design Document

**Version 1.0 | March 2026**

---

## Legal Disclaimer

IbkrConduit is an independent, community-developed open source library. It is not affiliated with, endorsed by, or supported by Interactive Brokers LLC or any of its subsidiaries. Interactive Brokers®, IBKR®, and related marks are trademarks of Interactive Brokers LLC.

Financial trading involves substantial risk of loss. IbkrConduit is provided as infrastructure software only — it does not provide investment advice and is not responsible for trading decisions or financial outcomes. Use at your own risk. Always test thoroughly with a paper trading account before connecting to a live account.

This disclaimer must appear in the project README, NuGet package description, and any other consumer-facing documentation.

---

## Purpose

This document captures all design decisions, architectural choices, known IBKR API behaviors, and implementation guidance for IbkrConduit — a C#/.NET open source client library for the Interactive Brokers Client Portal Web API (CP Web API) using OAuth 1.0a authentication. It is intended as the primary implementation reference for use in Claude Code with Superpowers.

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [IbkrConduit vs IBKR.Sdk.Client](#2-ibkrconduit-vs-ibkrsdkclient)
3. [IBKR API Choice — CP Web API with OAuth 1.0a](#3-ibkr-api-choice--cp-web-api-with-oauth-10a)
4. [OAuth 1.0a Authentication](#4-oauth-10a-authentication)
5. [Multi-Tenant Architecture](#5-multi-tenant-architecture)
6. [Technology Stack](#6-technology-stack)
7. [Session Lifecycle Management](#7-session-lifecycle-management)
8. [Rate Limiting](#8-rate-limiting)
9. [Order Management](#9-order-management)
10. [Order History and Reconciliation Data](#10-order-history-and-reconciliation-data)
11. [Flex Web Service](#11-flex-web-service)
12. [WebSocket Support](#12-websocket-support)
13. [Known IBKR CP Web API Behaviors](#13-known-ibkr-cp-web-api-behaviors)
14. [Testing Strategy](#14-testing-strategy)
15. [Security Considerations](#15-security-considerations)
16. [Key IBKR API Endpoints Reference](#16-key-ibkr-api-endpoints-reference)
17. [Open Source Distribution](#17-open-source-distribution)
18. [Reference Materials](#18-reference-materials)

---

## 1. Project Overview

IbkrConduit is a C#/.NET open source library that provides a clean, production-ready client for the Interactive Brokers Client Portal Web API (CPAPI 1.0 / Web API 1.0). It handles OAuth 1.0a authentication, multi-tenant session management, rate limiting, order submission with question/reply flow, market data, portfolio data, and Flex Web Service integration.

### 1.1 Goals

- Provide the first serious C# open source client for the IBKR CP Web API with OAuth 1.0a support
- Handle all IBKR API quirks (session lifecycle, question/reply flow, rate limits, pre-flight requests) so consuming applications do not have to
- Support multiple independent IBKR accounts within a single process — multi-tenant by design
- Be storage-agnostic — credentials are provided as pre-loaded objects, not file paths or cloud-specific constructs
- Be distribution-quality — proper NuGet packaging, CI/CD, documentation, and open source governance

### 1.2 What IbkrConduit Is Not

- It is not a trading strategy framework
- It is not a backtesting engine
- It is not a portfolio management system
- It does not perform position reconciliation — it surfaces the data needed for reconciliation
- It does not implement the brokerage abstraction layer — that is the consuming application's responsibility
- It does not support the TWS socket API — that is a separate API path entirely

### 1.3 Target Framework and Dependencies

- Target: .NET 6+ minimum (required for `PeriodicTimer`, `System.Threading.RateLimiting`)
- Language: C# 10+
- All crypto via `System.Security.Cryptography` — no external crypto dependencies
- HTTP via Refit + `IHttpClientFactory`
- Resilience via Polly / `Microsoft.Extensions.Http.Resilience`
- Rate limiting via `System.Threading.RateLimiting` (built into .NET 7+, backport available for .NET 6)

---

## 2. IbkrConduit vs IBKR.Sdk.Client

There is an existing .NET package on NuGet called `IBKR.Sdk.Client`. Users finding both packages should understand the difference:

| Aspect | IbkrConduit | IBKR.Sdk.Client |
|---|---|---|
| API target | Web API 1.0 (CPAPI 1.0) | Web API 2.0 (newer, beta) |
| Auth method | OAuth 1.0a (first-party self-service) | OAuth 2.0 (private_key_jwt) |
| Status | Targets stable, fully documented API | Targets API still in beta, incomplete documentation |
| Multi-tenant | First-class design consideration | Not specifically documented |
| Flex Web Service | Included | Not included |
| Open source | MIT licensed, community driven | Unclear governance |

**Why IbkrConduit targets Web API 1.0:** IBKR's Web API 2.0 is still in beta as of the time of writing and is not fully documented. IBKR has stated that existing endpoints and authentication schemes are not deprecated and will continue to receive features and updates. Web API 1.0 is production-stable, fully documented, and the OAuth 1.0a self-service path is accessible to individual IBKR Pro account holders without institutional approval. IbkrConduit will be extended to support Web API 2.0 once it reaches general availability with complete documentation.

---

## 3. IBKR API Choice — CP Web API with OAuth 1.0a

### 3.1 Decision

IbkrConduit uses the IBKR Client Portal Web API (CPAPI 1.0) exclusively, authenticated via OAuth 1.0a. The TWS socket API is explicitly out of scope.

### 3.2 Rationale

| Factor | CP Web API + OAuth 1.0a | TWS Socket API |
|---|---|---|
| Authentication | OAuth 1.0a — stateless, token-based, no browser or Selenium required | Requires running TWS or IB Gateway process with credential injection |
| Multi-tenant support | Clean — each account has independent OAuth credentials, no process coupling | Requires separate Gateway process per account or complex client ID management |
| Headless operation | Fully headless — no display, no Chrome, no Selenium | Requires virtual display or GUI process |
| Cloud deployment | Native HTTP/REST — clean container deployment | Stateful socket connection to a GUI process — awkward in containers |
| Order types | Market, Limit, Stop, Stop-Limit, MOC, LOC, Trailing — covers systematic retail trading needs | Full algo suite (VWAP, TWAP, Adaptive) |
| Multi-leg options | Not supported — legs submitted separately | Native combo orders supported |
| Maturity | Newer, IBKR's stated forward direction (OAuth 2.0 consolidation roadmap) | Battle-tested, 30+ year heritage |

### 3.3 Known CP Web API Limitations

- No IB algo order types (VWAP, TWAP, Adaptive, Accumulate/Distribute) — TWS API only
- No native multi-leg combo orders — legs submitted separately
- SmartRouting available as a destination but routing configuration options are limited vs TWS
- OAuth CP API users cannot specify IBKR server location — routed to nearest server automatically
- `/iserver/account/orders` is session-scoped — does not survive session restarts (see Section 10)

---

## 4. OAuth 1.0a Authentication

### 4.1 Overview

IBKR OAuth 1.0a for individual accounts (first-party OAuth) is available via the IBKR Self-Service Portal without requiring institutional third-party compliance approval. It provides fully headless authentication — no browser, no Selenium, no credential injection.

### 4.2 Self-Service Portal

```
https://ndcdyn.interactivebrokers.com/sso/Login?action=OAUTH&RL=1&ip2loc=US
```

Log in with the IBKR username for each account. Paper and live accounts require separate OAuth setups with separate key pairs.

### 4.3 Setup Process (Per Account)

Generate RSA key pairs and Diffie-Hellman parameters using OpenSSL:

```bash
openssl genrsa -out private_signature.pem 2048
openssl rsa -in private_signature.pem -outform PEM -pubout -out public_signature.pem
openssl genrsa -out private_encryption.pem 2048
openssl rsa -in private_encryption.pem -outform PEM -pubout -out public_encryption.pem
openssl dhparam -out dhparam.pem 2048
```

In the Self-Service Portal:

- Choose a Consumer Key (9 characters, A-Z alphanumeric — portal converts to uppercase)
- Upload `public_signature.pem` and `public_encryption.pem`
- Upload `dhparam.pem`
- Generate Access Token and Access Token Secret — **copy immediately, shown only once**

> **NOTE:** Generate separate key pairs for live and paper accounts. Do not reuse keys across accounts.

### 4.4 Credentials Required Per Tenant

| Credential | Description | Source |
|---|---|---|
| Consumer Key | 9-character identifier chosen during portal setup | Self-Service Portal |
| Access Token | OAuth access token | Generated in Self-Service Portal (shown once) |
| Access Token Secret (encrypted) | Encrypted access token secret | Generated in Self-Service Portal (shown once) |
| Private Signature Key | RSA private key for request signing | Generated locally (private_signature.pem) |
| Private Encryption Key | RSA private key for token decryption | Generated locally (private_encryption.pem) |
| DH Prime | Diffie-Hellman prime (hex) for live session token derivation | Extracted from dhparam.pem |

### 4.5 IbkrOAuthCredentials Model

The library accepts pre-loaded credential objects. Storage of key material (Key Vault, local files, environment variables, HSM) is the consuming application's responsibility — IbkrConduit is storage-agnostic.

```csharp
public record IbkrOAuthCredentials(
    string TenantId,
    string ConsumerKey,
    string AccessToken,
    string EncryptedAccessTokenSecret,
    RSA SignaturePrivateKey,      // pre-loaded RSA object, not a file path
    RSA EncryptionPrivateKey,     // pre-loaded RSA object, not a file path
    BigInteger DhPrime
) : IDisposable
{
    public void Dispose()
    {
        SignaturePrivateKey.Dispose();
        EncryptionPrivateKey.Dispose();
    }
}
```

### 4.6 Authentication Flow (Per Session)

1. Decrypt the Access Token Secret using the private encryption key (RSA-OAEP)
2. Generate a random 256-bit DH private value (`a`)
3. Compute DH public value: `A = g^a mod p` (g=2, p=DH prime)
4. POST to `/oauth/live_session_token` with signed OAuth header including DH public value
5. Receive encrypted live session token and IBKR's DH public value (`B`) from response
6. Derive shared DH secret: `K = B^a mod p`
7. Compute live session token: `HMAC-SHA1(K, decrypted_access_token_secret)`
8. POST to `/iserver/auth/ssodh/init` to initialize brokerage session
9. POST to `/iserver/questions/suppress` with known question type IDs
10. Start per-tenant tickle timer
11. Session is ready for use

> **NOTE:** Steps 8 and 9 must occur after every new live session token — suppressions are per-session and do not persist.

### 4.7 Request Signing

Each API request requires an OAuth Authorization header containing:

- `oauth_consumer_key` — the 9-character consumer key
- `oauth_nonce` — unique random value per request
- `oauth_timestamp` — seconds since Unix epoch
- `oauth_token` — the access token
- `oauth_signature_method` — `HMAC-SHA256`
- `oauth_signature` — HMAC-SHA256 signature using the live session token as the key

All crypto primitives are available in `System.Security.Cryptography`. No external crypto libraries are required or used.

> **NOTE:** IBind (the Python reference implementation) uses pyCrypto which is deprecated and has known security vulnerabilities because IBKR's reference code uses it. The C# implementation using `System.Security.Cryptography` is meaningfully more secure.

### 4.8 Live Session Token Lifecycle

- Valid for 24 hours from creation
- Used to sign all subsequent API requests (more efficient than RSA per-request)
- Proactive refresh at ~23 hours (1 hour before expiry)
- Reactive fallback: 401 triggers immediate refresh
- Brokerage session must be re-initialized after every token refresh

---

## 5. Multi-Tenant Architecture

### 5.1 Requirements

- Single process manages multiple independent IBKR accounts simultaneously
- No cross-tenant state leakage
- Independent session lifecycle per tenant
- Thread-safe concurrent access within and across tenants
- Graceful handling of per-tenant auth failures without affecting other tenants

### 5.2 Session Manager Design

```csharp
public class IbkrSessionManager
{
    private readonly ConcurrentDictionary<string, TenantSession> _sessions;
    // SemaphoreSlim(1,1) per tenant — prevents thundering herd on token refresh
    // PeriodicTimer per tenant — independent tickle timers
    // TenantSession holds: LiveSessionToken, Expiry, BrokerageSessionStatus, RateLimiters
}
```

Key design points:

- `ConcurrentDictionary<string, TenantSession>` keyed by `TenantId`
- `SemaphoreSlim(1,1)` per tenant for token refresh serialization — concurrent requests hitting an expired token wait rather than all attempting refresh simultaneously
- Single refresh flight per tenant — others await the semaphore
- Proactive refresh via background timer — not in the request hot path
- Per-tenant `PeriodicTimer` for tickle (~60 second interval)
- Brokerage session re-initialization after every token refresh
- Question suppression re-applied after every brokerage session initialization

### 5.3 HttpClient Strategy — Named Client Per Tenant

Each tenant gets a named `HttpClient` registered with `IHttpClientFactory`, each with its own `OAuthSigningHandler` pipeline configured with that tenant's credentials. Clean isolation, bounded instance count.

```csharp
// Called once per tenant during registration
services.AddRefitClient<IIbkrRestApi>(tenantId)
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.ibkr.com/v1/api"))
    .AddHttpMessageHandler(sp => sp.GetRequiredService<OAuthSigningHandlerFactory>()
        .Create(tenantId))
    .AddStandardResilienceHandler();
```

---

## 6. Technology Stack

### 6.1 Core Dependencies

| Package | Purpose | Notes |
|---|---|---|
| Refit | HTTP client generation from interfaces | Eliminates HttpClient boilerplate, pairs with IHttpClientFactory |
| Polly / Microsoft.Extensions.Http.Resilience | Retry/backoff policies | 503s and network errors are expected operational conditions |
| Microsoft.Extensions.Http | IHttpClientFactory, DelegatingHandler pipeline | Standard .NET HTTP infrastructure |
| System.Security.Cryptography | RSA, HMAC-SHA256, DH | All crypto — no external libraries |
| System.Threading.RateLimiting | Token bucket rate limiters | Built into .NET 7+, available as backport for .NET 6 |
| System.Text.Json | JSON deserialization | Default; Newtonsoft supported via configuration |
| WireMock.Net | HTTP mock for integration testing | Record/replay + hand-crafted edge case scenarios |
| xUnit | Test runner | Unit and integration tests |

### 6.2 HTTP Pipeline Architecture

```
Refit IIbkrRestApi call
  → OAuthSigningHandler    (adds OAuth Authorization header)
  → RateLimitingHandler    (token bucket, wait-not-fail)
  → ResilienceHandler      (Polly retry/backoff for transient failures)
  → HttpClient
  → IBKR CP Web API        (or WireMock in tests)
```

### 6.3 OAuthSigningHandler

```csharp
public class OAuthSigningHandler : DelegatingHandler
{
    private readonly ISessionTokenProvider _tokenProvider;

    public OAuthSigningHandler(ISessionTokenProvider tokenProvider)
        => _tokenProvider = tokenProvider;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetLiveSessionTokenAsync(cancellationToken);
        request.Headers.Authorization = BuildOAuthHeader(request, token);
        return await base.SendAsync(request, cancellationToken);
    }
}
```

### 6.4 Refit Interface Structure

```csharp
public interface IIbkrAuthApi
{
    [Post("/iserver/auth/ssodh/init")]
    Task<BrokerageSessionResponse> InitBrokerageSession();

    [Get("/iserver/auth/status")]
    Task<AuthStatusResponse> GetAuthStatus();

    [Post("/tickle")]
    Task<TickleResponse> Tickle();

    [Post("/iserver/questions/suppress")]
    Task SuppressQuestions([Body] SuppressQuestionsRequest request);
}

public interface IIbkrOrderApi
{
    [Post("/iserver/account/{accountId}/orders")]
    Task<OrderSubmissionResult> PlaceOrder(
        string accountId, [Body] OrderRequest request);

    [Post("/iserver/reply/{replyId}")]
    Task<OrderSubmissionResult> ReplyToQuestion(
        string replyId, [Body] ReplyRequest request);

    [Delete("/iserver/account/{accountId}/order/{orderId}")]
    Task CancelOrder(string accountId, string orderId);

    [Get("/iserver/account/{accountId}/orders")]
    Task<LiveOrdersResponse> GetLiveOrders(string accountId);

    [Get("/iserver/trades")]
    Task<TradesResponse> GetTrades();
}

public interface IIbkrPortfolioApi
{
    [Get("/portfolio/{accountId}/positions/0")]
    Task<List<Position>> GetPositions(string accountId);

    [Get("/portfolio/{accountId}/summary")]
    Task<AccountSummary> GetAccountSummary(string accountId);
}

public interface IIbkrMarketDataApi
{
    [Get("/iserver/marketdata/snapshot")]
    Task<List<MarketDataSnapshot>> GetSnapshot(
        [AliasAs("conids")] string conids,
        [AliasAs("fields")] string fields);

    [Get("/iserver/marketdata/history")]
    Task<HistoricalDataResponse> GetHistory(
        [AliasAs("conid")] long conid,
        [AliasAs("period")] string period,
        [AliasAs("bar")] string bar);

    [Get("/iserver/secdef/search")]
    Task<List<ContractSearchResult>> SearchContracts(
        [AliasAs("symbol")] string symbol);

    [Get("/iserver/contract/{conid}/info")]
    Task<ContractInfo> GetContractInfo(long conid);
}
```

---

## 7. Session Lifecycle Management

### 7.1 Session Initialization Sequence

1. Decrypt Access Token Secret using private encryption key (RSA-OAEP)
2. Generate random 256-bit DH private value (`a`)
3. Compute DH public value: `A = g^a mod p` (g=2, p=DH prime)
4. POST `/oauth/live_session_token` with signed request including DH public value
5. Receive encrypted live session token and IBKR's DH public value (`B`)
6. Derive shared DH secret: `K = B^a mod p`
7. Compute live session token: `HMAC-SHA1(K, decrypted_access_token_secret)`
8. POST `/iserver/auth/ssodh/init`
9. POST `/iserver/questions/suppress` with known question type IDs
10. Start per-tenant tickle timer
11. Mark session as ready

### 7.2 Brokerage Session vs Live Session Token

These are two distinct concepts:

- **Live Session Token** — OAuth token for signing requests. 24-hour lifetime. Refresh via full OAuth handshake.
- **Brokerage Session** — trading session initialized via `/iserver/auth/ssodh/init`. Times out after ~6 minutes without a tickle. Can be re-initialized without a full token refresh by calling `/iserver/auth/ssodh/init` again while the live session token is still valid.

### 7.3 Tickle Management

- POST `/tickle` approximately every 60 seconds per tenant
- Missed tickle causes session to go unauthenticated
- Use `PeriodicTimer` per tenant (.NET 6+)
- Tickle timer cancelled and restarted on session refresh
- Monitor tickle response for `authenticated: false` even on HTTP 200
- Tickle failures trigger session re-authentication, not silent swallowing

### 7.4 Token Refresh Strategy

- Proactive: schedule refresh at ~23 hours
- Reactive fallback: 401 triggers immediate refresh
- `SemaphoreSlim(1,1)` per tenant prevents concurrent refresh flights
- Requests awaiting refresh retry automatically after refresh completes
- Failed refresh surfaces as an error — do not silently leave the tenant session dead

### 7.5 Question Suppression

Call `/iserver/questions/suppress` after every brokerage session initialization. Suppressions are per-session. Common types to suppress for automated trading:

- Order without stop loss warning
- Outside regular trading hours confirmation
- Margin utilization warnings
- Large order size warnings

> **NOTE:** Discover specific message IDs during paper account testing using WireMock recording. Build the suppression list from real observed IDs.

### 7.6 Daily Maintenance Windows

IBKR briefly takes down brokerage functionality (`/iserver` endpoints) each evening:

| Region | Maintenance Onset |
|---|---|
| North America (NY and Chicago) | 01:00 US/Eastern |
| Europe | 01:00 CEST |
| Asia | 01:00 HKT |

The session manager must treat maintenance windows as expected operational events, not errors. Recommended behavior:

- Detect the maintenance window via failed tickle or 503 response during known maintenance period
- Pause operation gracefully during the window (~5-10 minute duration)
- Re-initialize session after the window completes
- Do not attempt aggressive reconnection during the window — wait and retry

The CP Web API itself (non-brokerage endpoints) remains accessible during maintenance. Only `/iserver` endpoints are affected.

---

## 8. Rate Limiting

### 8.1 Rate Limit Landscape

IBKR enforces two tiers of rate limits, both per authenticated username (per tenant session):

**Global limit:**
- 10 requests per second across all endpoints

**Per-endpoint stricter limits:**

| Endpoint | Limit |
|---|---|
| `/iserver/marketdata/snapshot` | 10 req/s |
| `/iserver/scanner/params` | 1 req/15 mins |
| `/iserver/scanner/run` | 1 req/s |
| `/iserver/trades` | 1 req/5 secs |
| `/iserver/orders` | 1 req/5 secs |
| `/iserver/account/pnl/partitioned` | 1 req/5 secs |
| `/portfolio/accounts` | 1 req/5 secs |
| `/portfolio/subaccounts` | 1 req/5 secs |

In a multi-tenant system each tenant has their own independent limits — these are per-session, not global across the library.

### 8.2 Design Choice — Wait, Not Fail

When a rate limit is reached, requests wait for a token to become available rather than failing immediately. This is the correct behavior for a library — the consuming application should not need to implement retry logic for rate limiting. The library handles it transparently via async waiting.

### 8.3 Token Bucket Implementation

`System.Threading.RateLimiting` (built into .NET 7+) provides the token bucket implementation:

```csharp
// Global limit per tenant: 10 req/s
var globalLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
{
    TokenLimit = 10,
    ReplenishmentPeriod = TimeSpan.FromSeconds(1),
    TokensPerPeriod = 10,
    AutoReplenishment = true,
    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
    QueueLimit = 100
});

// Slow endpoint limit per tenant: 1 req/5s
var slowEndpointLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
{
    TokenLimit = 1,
    ReplenishmentPeriod = TimeSpan.FromSeconds(5),
    TokensPerPeriod = 1,
    AutoReplenishment = true,
    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
    QueueLimit = 20
});
```

The global limiter sits in the `DelegatingHandler` pipeline for all requests. The slow endpoint limiter is applied at the Refit interface method level for the specific endpoints with stricter limits.

### 8.4 Per-Tenant Rate Limiter Lifecycle

Each `TenantSession` owns its rate limiter instances. Limiters are created on session initialization and disposed on session teardown. They are not shared across tenants.

### 8.5 429 Adaptive Response

A 429 response means the local rate limiter has drifted from IBKR's actual enforcement. In a correctly functioning system a 429 should never occur — it is a signal worth investigating.

Adaptive behavior on 429:

1. Back off immediately — do not retry the failed request for a minimum cooldown period
2. Tighten the local rate limiter by 10-20% (e.g. reduce effective token replenishment rate)
3. Run at the tightened rate for a 60-second recovery window
4. If no further 429s during recovery, gradually relax back toward the configured limit over the next 5 minutes
5. Log and emit a metric on every 429 — this should be a rare and notable event

This is a control loop, not a permanent ratchet. The local rate limiter is the primary defense; the 429 is feedback that something drifted.

> **NOTE:** IBKR imposes a 10-minute IP penalty box for repeated rate limit violations. Because the penalty box is IP-based, it affects all tenant sessions simultaneously — making this a shared risk across all tenants in the system. The adaptive response must be aggressive enough to prevent repeated 429s.

### 8.6 Flex Web Service Rate Limiting

The Flex Web Service is a separate service with its own implicit limits. It is not designed for polling — data updates at most every 5-10 minutes for Trade Confirmation queries and once daily for Activity Statements. A simple per-tenant cooldown timer (minimum 60 seconds between requests) is sufficient. Do not apply the CP Web API token bucket to Flex requests.

---

## 9. Order Management

### 9.1 The Question/Reply Flow

Order submission through the CP Web API has a stateful question/reply pattern. This is the programmatic equivalent of TWS dialog box confirmations — IBKR's API was built on the same backend as the TWS GUI, so confirmation prompts surface as JSON question objects in API responses.

Flow:

1. POST `/iserver/account/{accountId}/orders` with order payload
2. Response is either an **order confirmation** OR a **list of question objects**
3. If questions: POST each question ID to `/iserver/reply/{replyId}` with `{"confirmed": true}`
4. Reply may return further questions — loop until no questions remain
5. Final response contains order ID(s)

> **NOTE:** Reply IDs are stateful server-side and expire quickly. Do not delay between receiving a question and replying. Question suppression at session init is the primary defense — pre-suppress known question types to avoid the round-trip entirely.

### 9.2 OrderSubmissionResult Model

```csharp
public class OrderSubmissionResult
{
    public bool IsQuestion => Questions?.Count > 0;
    public bool IsConfirmation => Orders?.Count > 0;
    public List<OrderQuestion>? Questions { get; set; }
    public List<OrderConfirmation>? Orders { get; set; }
}
```

### 9.3 Order Submission Helper

```csharp
public async Task<List<OrderConfirmation>> SubmitOrderAsync(
    string accountId,
    OrderRequest request,
    CancellationToken ct)
{
    var result = await _orderApi.PlaceOrder(accountId, request);

    while (result.IsQuestion)
    {
        foreach (var question in result.Questions!)
        {
            result = await _orderApi.ReplyToQuestion(
                question.Id,
                new ReplyRequest { Confirmed = true },
                ct);
        }
    }

    return result.Orders ?? throw new InvalidOperationException(
        "Order submission produced neither questions nor confirmations.");
}
```

### 9.4 Order Submission Serialization

Submissions for the same account must be serialized due to the stateful question/reply flow. Use a `SemaphoreSlim(1,1)` per account. Different tenant accounts can submit concurrently without conflict.

### 9.5 US Futures — manualIndicator Field

For automated systems submitting US Futures orders, the `manualIndicator` field is required for CME Group Rule 536-B compliance. Orders without this field will be rejected.

```csharp
public class OrderRequest
{
    // ... other fields
    
    /// <summary>
    /// Required for US Futures orders submitted by automated systems.
    /// Set to false to indicate the order was generated programmatically,
    /// not manually entered by a human. CME Group Rule 536-B compliance.
    /// </summary>
    public bool? ManualIndicator { get; set; }
}
```

Consuming applications submitting US Futures orders must set `ManualIndicator = false`. IbkrConduit surfaces the field — it does not set it automatically since the library cannot know whether a given order is manual or automated from the perspective of the end user.

### 9.6 Conid Resolution and Caching

All order submission requires IBKR contract IDs (conids), not ticker symbols.

- Use `GET /iserver/secdef/search?symbol={symbol}` for resolution
- Cache aggressively — conids are stable for ETFs and equities
- Cache keyed by: symbol + exchange + currency + instrument type
- Consider persistent cache across service restarts for known instruments
- Validate periodically — conids can theoretically change

### 9.7 Supported Order Types

| Order Type | TIF Options | Notes |
|---|---|---|
| Market (MKT) | DAY, IOC | Use with caution |
| Limit (LMT) | DAY, GTC, IOC | Primary order type |
| Stop (STP) | DAY, GTC | Stop-loss orders |
| Stop Limit (STP LMT) | DAY, GTC | Preferred over plain stop |
| Market-on-Close (MOC) | DAY | End-of-day execution |
| Limit-on-Close (LOC) | DAY | End-of-day with price protection |
| Trailing Stop | DAY, GTC | Dynamic stop management |

### 9.8 Multi-Leg Options — Leg Risk Note

The CP Web API does not support native multi-leg combo orders. Legs must be submitted individually. The consuming application is responsible for managing leg risk (the window between first and second leg fills where a naked position may exist temporarily). IbkrConduit surfaces single-leg order submission cleanly — leg risk management is out of scope.

---

## 10. Order History and Reconciliation Data

### 10.1 The Session-Scoped Order History Problem

`/iserver/account/orders` returns orders from the **current brokerage session only** — orders currently working as well as those cancelled or filled within the same session. When the service restarts and a new session is established, this endpoint returns empty regardless of what was submitted previously.

This is particularly problematic for GTC (Good Till Cancelled) limit orders and stop orders — these can remain working on IBKR's servers for days or weeks. If your system restarts, it has no knowledge of these working orders from the `/iserver` endpoint alone.

> **NOTE:** GTC orders submitted via the CP Web API are held on IBKR's servers, not locally. They persist across your session restarts even though your session cannot see them after reconnecting. You cannot assume a clean slate on startup just because the orders endpoint returns empty.

### 10.2 Available Data Sources for Reconciliation

IbkrConduit surfaces all of the following. The consuming application is responsible for reconciliation logic.

| Data Source | Endpoint / Method | Scope | Use Case |
|---|---|---|---|
| Current positions | `GET /portfolio/{accountId}/positions/0` | Always current, session-independent | Ground truth for filled positions at any time |
| Current session orders | `GET /iserver/account/orders` | Current session only | Working orders submitted in current session |
| Current session fills | `GET /iserver/trades` | Current session only, 1 req/5s limit | Fill details for current session |
| WebSocket order history | WebSocket `sor` topic with `days` parameter | Up to 7 calendar days | Short outage recovery, order history lookback |
| Trade Confirmation flex query | Flex Web Service | Up to 365 days, includes open orders section | Cross-session fill history and working order state |
| Activity Statement flex query | Flex Web Service | Daily snapshot at market close | End-of-day baseline for positions and P&L |

### 10.3 The `/portfolio/positions` Advantage

`/portfolio` endpoints are served by a different backend process and do not require a brokerage session. They are accessible immediately after OAuth authentication, before `/iserver/auth/ssodh/init` is called. Current positions are always available regardless of session state.

### 10.4 WebSocket `sor` Topic — Days Parameter

The WebSocket order stream (`sor`) supports a `days` parameter (integer, 1-7) for historical order lookback:

```
sor+{"days": 3}         // last 3 days of order history
sor+{"realtimeUpdatesOnly": true}  // only new updates going forward
```

This is useful for short outages but capped at 7 days. Does not help for long-lived GTC orders older than 7 days.

### 10.5 Recommended Startup Reconciliation Sequence

IbkrConduit surfaces the necessary APIs. A consuming application performing startup reconciliation would typically:

1. Pull current positions from `/portfolio/{accountId}/positions` — always accurate regardless of downtime duration
2. Pull Trade Confirmation flex query with `OpenOrders` section — captures all currently working GTC orders regardless of when they were submitted
3. Pull WebSocket `sor` with appropriate `days` value — captures recent order activity
4. Reconcile against internal state

This sequence handles arbitrarily long outages. The flex query for open orders is the only reliable way to enumerate working GTC orders across session boundaries.

---

## 11. Flex Web Service

### 11.1 Overview

The Flex Web Service is a separate HTTP API for programmatically generating and retrieving pre-configured Flex Query reports. It is distinct from the CP Web API in every way — different base URL, different authentication, XML responses, and asynchronous two-step retrieval.

### 11.2 Authentication — Flex Token

Flex Web Service authentication uses a **separate long-lived token** generated in Client Portal — completely independent of OAuth 1.0a. Once the token is obtained, IBKR credentials are not involved in subsequent API calls.

**Token setup (manual, per account):**
- Log into Client Portal
- Navigate to Reports → Flex Queries → Flex Web Configuration
- Generate token — specify validity period (6 hours to 1 year) and optionally restrict to specific IPs
- Copy token — it is shown once

**Token characteristics:**
- Long-lived (configurable up to 1 year) — store securely, treat as a secret
- Generating a new token invalidates the previous one
- Per-username — each IBKR account needs its own token
- Separate from OAuth credentials — the library manages both independently

### 11.3 Query Template Setup (Manual, Per Account)

Flex Query templates are configured manually in Client Portal and identified by a Query ID. The Query ID is stable and reused programmatically.

**Required queries for a typical automated trading system:**

| Query Type | Purpose | Recommended Sections |
|---|---|---|
| Trade Confirmation | Fills and working orders | Trades, OpenOrders |
| Activity Statement | Daily end-of-day baseline | Trades, OpenPositions, CashReport |

> **NOTE:** Query templates are username-specific. In a multi-tenant system, each tenant must configure their own query templates in their own Client Portal and provide the resulting Query IDs to IbkrConduit.

### 11.4 Flex Web Service Base URL

```
https://ndcdyn.interactivebrokers.com/AccountManagement/FlexWebService/
```

This is different from the CP Web API base URL (`https://api.ibkr.com/v1/api`).

### 11.5 Two-Step Async Retrieval Flow

Flex queries are generated asynchronously. The flow is:

**Step 1 — Request generation:**
```
GET /SendRequest?t={token}&q={queryId}&v=3
```

Response (XML):
```xml
<FlexStatementResponse timestamp="...">
    <Status>Success</Status>
    <ReferenceCode>1234567890</ReferenceCode>
    <Url>https://ndcdyn.interactivebrokers.com/...</Url>
</FlexStatementResponse>
```

**Step 2 — Retrieve report (poll until ready):**
```
GET /GetStatement?t={token}&q={referenceCode}&v=3
```

If not yet ready, IBKR returns a "Statement generation in progress" response. Poll with backoff until the report is available. Typical wait: 5-10 seconds for Trade Confirmation, longer for large Activity Statements.

**Date range override (optional):**
```
GET /SendRequest?t={token}&q={queryId}&fd=20260101&td=20260315&v=3
```

Allows overriding the date range configured in the template. Format: `yyyyMMdd`. Maximum range: 365 days.

### 11.6 Response Format

Flex Web Service returns XML, not JSON. IbkrConduit parses the XML and exposes typed .NET objects. The XML schema is well-defined and stable.

Key data available in Trade Confirmation queries:
- `Trades` section — execution details (symbol, conid, quantity, price, time, order type, side)
- `OpenOrders` section — currently working orders (critical for GTC order reconciliation)

### 11.7 Data Refresh Characteristics

| Query Type | Refresh Rate | Notes |
|---|---|---|
| Trade Confirmation | 5-10 minutes after execution | Not real-time — do not poll continuously |
| Activity Statement | Once daily at market close | End-of-day only — not intraday |

The Flex Web Service is not suitable for active polling or real-time data. Design for scheduled pulls, not continuous monitoring.

### 11.8 C# Reference Implementation

`gabbersepp/ib-flex-reader` on GitHub is a .NET Standard 2.0 C# library for fetching and parsing IBKR flex queries. Useful as implementation reference.

### 11.9 Per-Tenant Flex Configuration Model

```csharp
public record IbkrFlexCredentials(
    string TenantId,
    string FlexToken,                    // long-lived Flex Web Service token
    string TradeConfirmationQueryId,     // Query ID for Trade Confirmation flex query
    string ActivityStatementQueryId      // Query ID for Activity Statement flex query
);
```

---

## 12. WebSocket Support

### 12.1 Overview

The CP Web API supports WebSocket connections for real-time streaming of market data, order updates, and P&L. The WebSocket session has its own lifecycle distinct from the REST session.

### 12.2 WebSocket URL — OAuth Path

For OAuth 1.0a connections, the WebSocket URL includes the live session token:

```
wss://api.ibkr.com/v1/api/ws?oauth_token={liveSessionToken}
```

This is different from the Gateway-based WebSocket URL (`wss://localhost:5000/v1/api/ws`).

### 12.3 WebSocket vs REST Session Relationship

The WebSocket connection is separate from the REST session but shares the same live session token. Key implications:

- WebSocket connection must be re-established after live session token refresh
- WebSocket heartbeat (10 seconds) is separate from REST tickle (60 seconds) — both must be maintained
- WebSocket disconnection does not invalidate the REST session and vice versa
- Authentication state is shared — if the brokerage session expires, both REST and WebSocket are affected

### 12.4 WebSocket Heartbeat

Send a heartbeat message every 10 seconds to maintain the WebSocket connection:

```
{"topic": "tic"}
```

This is separate from the REST `/tickle` endpoint. Both must be maintained independently.

### 12.5 Key WebSocket Topics

| Topic | Subscribe Message | Purpose |
|---|---|---|
| Order updates | `sor+{}` | Real-time order status changes |
| Order history | `sor+{"days": N}` | Historical orders, N = 1-7 days |
| Real-time only orders | `sor+{"realtimeUpdatesOnly": true}` | No prior history, new updates only |
| Market data | `smd+{conid}+{"fields":["31","84","86"]}` | Real-time top-of-book |
| P&L | `spl+{}` | Real-time P&L streaming |
| Portfolio | `ssd+{}` | Portfolio summary streaming |

### 12.6 Reconnection Handling

WebSocket connections can drop due to network issues, maintenance windows, or token expiry. IbkrConduit handles reconnection transparently:

- Detect disconnection via WebSocket close event or heartbeat timeout
- If live session token is still valid: reconnect and resubscribe to active topics
- If live session token has expired: refresh token first, then reconnect
- Apply exponential backoff on repeated reconnection failures
- During maintenance windows: pause reconnection attempts, retry after window

### 12.7 Market Data Pre-flight — WebSocket vs REST

The pre-flight requirement (first request returns no data) applies to the REST snapshot endpoint (`/iserver/marketdata/snapshot`). WebSocket market data subscriptions (`smd+`) do not require a pre-flight — data flows immediately on subscription.

---

## 13. Known IBKR CP Web API Behaviors

### 13.1 Market Data Pre-flight

REST market data snapshot requests require a pre-flight:

1. `GET /iserver/marketdata/snapshot?conids={conid}&fields={fields}` — first call returns empty or partial data
2. Repeat after a short delay — subsequent calls return data
3. Pre-flight required once per conid per session
4. Does not apply to WebSocket market data subscriptions

### 13.2 Brokerage Session Timeout

The brokerage session times out after approximately 6 minutes without a tickle:

- `GET /iserver/auth/status` returns `connected: true, authenticated: false`
- `POST /iserver/auth/ssodh/init` re-initializes without a full token refresh
- Re-apply question suppressions after re-initialization

### 13.3 Rate Limit Enforcement

- Global: 10 req/s per session (see Section 8)
- 429 response triggers 10-minute IP penalty box for repeated violations
- Penalty box affects all tenant sessions on the same IP simultaneously

### 13.4 503 Service Unavailable

503s are common and expected — IBKR servers restart periodically. Treat as a normal operational condition:

- Retry with exponential backoff
- Distinguish IBKR-side 503 (transient, retry) from auth failure 401 (requires re-auth)
- Maintenance window 503s should not trigger aggressive reconnection

### 13.5 The Question/Reply Design — Historical Context

The question/reply flow exists because the CP Web API is built on the same backend as the TWS desktop GUI. When a human trader submits an order that triggers a risk warning, TWS shows a dialog box. The REST API exposes the same mechanism as JSON question objects. The suppress endpoint exists because IBKR recognized automated systems cannot click dialog boxes. This is not elegant API design — it is an artifact of bolting a REST API onto decades-old desktop application internals.

### 13.6 Paper Account Behavioral Differences

Paper trading executes in a simulated environment. Notable differences from live:

- Order fills may not accurately reflect real market conditions — fills can occur at prices that would not be achievable in live markets
- Some question types behave differently in paper vs live
- Market data in paper accounts may have delays or gaps not present in live
- Flex query data in paper accounts may be incomplete or formatted differently

Use paper accounts for integration and connectivity validation. Do not use paper account behavior as a proxy for live account behavior when validating execution logic.

### 13.7 Server Location

OAuth CP API sessions route to the nearest IBKR server automatically. Server location cannot be specified, unlike the TWS API. Not a concern for systematic retail trading.

---

## 14. Testing Strategy

### 14.1 Testing Layers

| Layer | Tool | Network | Purpose |
|---|---|---|---|
| Unit — OAuth crypto | xUnit | None | RSA signing, DH derivation, OAuth header construction against IBKR Campus example values |
| Unit — Session manager concurrency | xUnit | None (mocked HTTP) | Token refresh thundering herd, tickle timers, concurrent tenant isolation |
| Unit — Rate limiter behavior | xUnit | None | Token bucket behavior, 429 adaptive response, recovery window |
| Integration — API client | WireMock.Net | None (mock server) | Question/reply flow, error responses, retry, session lifecycle edge cases |
| Integration — Flex parsing | xUnit | None (local XML fixtures) | Flex response parsing, OpenOrders section, Trade section |
| Integration — End-to-end | Paper IBKR account | Real IBKR | Full auth flow, real response shapes, order submission |
| Live validation | Paper account extended run | Real IBKR | Stability before live credentials |

### 14.2 WireMock.Net Strategy

WireMock.Net is the primary tool for integration testing without IBKR connectivity.

- Record real CP Web API sessions during paper account testing to capture authentic response shapes
- Store cassettes as test fixtures — living documentation of actual API behavior
- Hand-edit cassettes for edge cases

Key edge case scenarios to model:

- Order submission returning question instead of confirmation
- Chained question replies (question → question → confirmation)
- Simultaneous token refresh across N tenants — assert exactly one refresh per tenant
- 503 mid-session triggering retry and eventual success
- Tickle response with `authenticated: false` triggering re-auth
- Token expiry during order submission — refresh and retry
- Market data pre-flight pattern — first call empty, second returns data
- 429 response triggering adaptive rate limiter tightening
- Maintenance window 503 pattern — pause, wait, reconnect
- WebSocket disconnect and reconnect with topic resubscription
- Flex Web Service "Statement generation in progress" polling flow
- GTC order appearing in Flex OpenOrders section but not in `/iserver/account/orders`

### 14.3 Concurrency Test Pattern

```csharp
[Fact]
public async Task TokenRefresh_ConcurrentRequests_OnlyOneRefreshFlight()
{
    _sessionManager.ExpireToken(tenantId);
    var refreshCallCount = 0;
    _mockTokenProvider
        .Setup(x => x.RefreshAsync(tenantId, It.IsAny<CancellationToken>()))
        .Callback(() => Interlocked.Increment(ref refreshCallCount))
        .ReturnsAsync(new LiveSessionToken(...));

    var tasks = Enumerable.Range(0, 20)
        .Select(_ => _adapter.PlaceOrderAsync(tenantId, _request, CancellationToken.None));
    await Task.WhenAll(tasks);

    Assert.Equal(1, refreshCallCount);
}
```

### 14.4 Unit Testing OAuth Crypto

The IBKR Campus OAuth 1.0a documentation includes example values. Pin tests against these:

```csharp
[Fact]
public void OAuthHeader_KnownInputs_ProducesExpectedSignature()
{
    // Use example values from IBKR Campus OAuth 1.0a documentation
    var header = OAuthHeaderBuilder.Build(
        method: "POST",
        url: "https://api.ibkr.com/v1/api/iserver/account/DU123/orders",
        consumerKey: "EXAMPLE01",
        nonce: "abc123",             // fixed for deterministic test
        timestamp: "1700000000",     // fixed for deterministic test
        liveSessionToken: new LiveSessionToken("known_token_value"));

    Assert.Equal("expected_signature_value", ExtractSignature(header));
}
```

### 14.5 Paper Account Considerations

- Paper OAuth requires separate setup from live (separate keys, separate self-service portal session)
- Paper flex queries require separate Flex token and Query IDs
- Paper account behaviors differ from live — see Section 13.6
- Use paper for connectivity and integration validation, not behavioral validation

---

## 15. Security Considerations

### 15.1 Credential Storage (Consuming Application Responsibility)

IbkrConduit is storage-agnostic — it accepts pre-loaded `RSA` objects and string tokens, not file paths or cloud-specific constructs. The consuming application is responsible for loading credentials securely. Recommended approaches:

- Azure Key Vault with managed identity (Azure deployments)
- AWS Secrets Manager (AWS deployments)
- HashiCorp Vault (on-premises or multi-cloud)
- Encrypted local configuration (development/single-machine)

Never store private RSA keys or Flex tokens in source control, plain text configuration files, or environment variables in production.

### 15.2 Key Material Handling in IbkrConduit

- RSA private keys held as `RSA` objects — not retained as strings or byte arrays
- Live session tokens are ephemeral — 24-hour lifetime, never persisted
- Flex tokens held in memory only — not logged, not serialized
- Log sanitization — ensure no credential material appears in log output
- `IbkrOAuthCredentials` implements `IDisposable` — `RSA` objects disposed properly

### 15.3 Security Improvement Over Python References

IBind (the primary Python reference) uses pyCrypto which is deprecated and has known CVEs. IBKR provided this as their reference implementation and IBind has notified IBKR of the issue. The C# implementation uses `System.Security.Cryptography` throughout — this is a meaningful security improvement.

### 15.4 Network Security

- All CP Web API traffic is HTTPS
- Flex Web Service traffic is HTTPS
- No inbound ports required (unlike Gateway which exposes port 5000)
- Consuming applications should restrict outbound traffic to `api.ibkr.com` and `ndcdyn.interactivebrokers.com`

### 15.5 SECURITY.md

The repository must include a `SECURITY.md` file describing the responsible disclosure process for security vulnerabilities. Given the library handles financial credentials, this is non-optional for a credible open source project. Minimum content:

- How to report a security vulnerability (private channel, not public GitHub issues)
- Expected response timeline
- Scope — what counts as a security issue for this library
- Out of scope — IBKR's own security issues should be reported to IBKR directly

---

## 16. Key IBKR API Endpoints Reference

### 16.1 CP Web API Base URL (OAuth 1.0a)

```
https://api.ibkr.com/v1/api
```

### 16.2 WebSocket URL (OAuth 1.0a)

```
wss://api.ibkr.com/v1/api/ws?oauth_token={liveSessionToken}
```

### 16.3 Flex Web Service Base URL

```
https://ndcdyn.interactivebrokers.com/AccountManagement/FlexWebService/
```

### 16.4 Endpoint Reference

| Endpoint | Method | Purpose |
|---|---|---|
| `/oauth/live_session_token` | POST | OAuth handshake — acquire live session token |
| `/iserver/auth/ssodh/init` | POST | Initialize brokerage session |
| `/iserver/auth/status` | GET | Check session authentication status |
| `/iserver/questions/suppress` | POST | Pre-suppress question types for current session |
| `/tickle` | POST | Keep REST session alive — every ~60 seconds |
| `/iserver/account/{id}/orders` | POST | Submit order — may return questions or confirmation |
| `/iserver/reply/{replyId}` | POST | Reply to order confirmation question |
| `/iserver/account/{id}/order/{orderId}` | DELETE | Cancel order |
| `/iserver/account/{id}/orders` | GET | Get current session orders |
| `/iserver/trades` | GET | Get current session fills (1 req/5s limit) |
| `/portfolio/{id}/positions/0` | GET | Current positions — session-independent |
| `/portfolio/{id}/summary` | GET | Account summary — session-independent |
| `/iserver/marketdata/snapshot` | GET | Market data snapshot (requires pre-flight) |
| `/iserver/marketdata/history` | GET | Historical market data (5 concurrent max) |
| `/iserver/secdef/search` | GET | Symbol to conid resolution |
| `/iserver/contract/{conid}/info` | GET | Contract details by conid |
| `FlexWebService/SendRequest` | GET | Initiate flex query generation |
| `FlexWebService/GetStatement` | GET | Retrieve generated flex query |

---

## 17. Open Source Distribution

### 17.1 Project Identity

| Item | Value |
|---|---|
| Package name | `IbkrConduit` |
| GitHub repository | `ibkr-conduit` |
| Default namespace | `IbkrConduit` |
| Install command | `dotnet add package IbkrConduit` |
| License | MIT |
| Target frameworks | net8.0, net10.0 |

### 17.2 Repository Structure

```
ibkr-conduit/
├── src/
│   └── IbkrConduit/              # Main library project
│       ├── Auth/                 # OAuth 1.0a implementation
│       ├── Session/              # Session and tenant management
│       ├── RateLimit/            # Rate limiting infrastructure
│       ├── Orders/               # Order submission and management
│       ├── Portfolio/            # Positions and account data
│       ├── MarketData/           # Snapshot, history, WebSocket
│       ├── Flex/                 # Flex Web Service client
│       └── WebSocket/            # WebSocket client and topics
├── tests/
│   ├── IbkrConduit.Tests.Unit/   # Unit tests
│   └── IbkrConduit.Tests.Integration/ # WireMock integration tests
├── samples/
│   └── IbkrConduit.Samples/      # Basic usage examples
├── .github/
│   ├── workflows/
│   │   ├── ci.yml                # Build and test on PR
│   │   └── publish.yml           # Publish to NuGet on release tag
│   ├── ISSUE_TEMPLATE/
│   │   ├── bug_report.md
│   │   └── feature_request.md
│   └── pull_request_template.md
├── IbkrConduit.sln
├── README.md
├── CONTRIBUTING.md
├── CHANGELOG.md
├── SECURITY.md
└── LICENSE                       # MIT
```

### 17.3 NuGet Package Metadata

```xml
<PropertyGroup>
    <PackageId>IbkrConduit</PackageId>
    <Version>0.1.0</Version>
    <Authors>Craig Quillen</Authors>
    <Description>
        A C#/.NET client library for the Interactive Brokers Client Portal Web API
        with OAuth 1.0a authentication, multi-tenant session management, rate limiting,
        and Flex Web Service integration. Not affiliated with Interactive Brokers LLC.
    </Description>
    <PackageTags>ibkr;interactive-brokers;trading;oauth;client-portal-api;algorithmic-trading</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageProjectUrl>https://github.com/[owner]/ibkr-conduit</PackageProjectUrl>
    <RepositoryUrl>https://github.com/[owner]/ibkr-conduit</RepositoryUrl>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

### 17.4 Versioning Strategy

Semantic Versioning (SemVer 2.0):

- `0.x.y` during initial development — public API may change between minor versions
- `1.0.0` when the public API is stable and production-validated
- Breaking changes increment major version
- New features increment minor version
- Bug fixes increment patch version

Pre-release versions: `1.0.0-alpha.1`, `1.0.0-beta.1`, `1.0.0-rc.1`

### 17.5 GitHub Actions CI/CD

**CI pipeline (`ci.yml`) — triggers on PR and push to main:**

```yaml
name: CI
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: |
            6.x
            7.x
            8.x
            9.x
      - run: dotnet restore
      - run: dotnet build --no-restore --configuration Release
      - run: dotnet test --no-build --configuration Release --collect:"XPlat Code Coverage"
      - uses: codecov/codecov-action@v4
```

**Publish pipeline (`publish.yml`) — triggers on release tag:**

```yaml
name: Publish
on:
  push:
    tags: ['v*.*.*']

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.x
      - run: dotnet pack src/IbkrConduit --configuration Release --output ./artifacts
      - run: dotnet nuget push ./artifacts/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json
```

### 17.6 Branch Protection Rules (main branch)

- Require pull request before merging — no direct pushes
- Require at least 1 approving review
- Dismiss stale reviews when new commits are pushed
- Require status checks to pass: `build-and-test`
- Require branches to be up to date before merging
- Do not allow bypassing the above settings

### 17.7 PR Template (`.github/pull_request_template.md`)

```markdown
## Description
<!-- What does this PR do? -->

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Breaking change
- [ ] Documentation update
- [ ] Dependency update

## Testing
- [ ] Unit tests added/updated
- [ ] Integration tests added/updated (WireMock cassettes updated if needed)
- [ ] Tested against paper account

## Checklist
- [ ] Code follows project style conventions
- [ ] XML documentation comments added/updated for public APIs
- [ ] CHANGELOG.md updated
- [ ] No secrets or credentials in code or test fixtures
```

### 17.8 Issue Templates

**Bug report** — captures: IbkrConduit version, .NET version, authentication method, steps to reproduce, expected vs actual behavior, logs (sanitized).

**Feature request** — captures: problem statement, proposed solution, alternatives considered, IBKR API documentation links if relevant.

### 17.9 CONTRIBUTING.md

Must cover:

- Prerequisites (dotnet SDK, openssl for OAuth setup)
- Building and running tests locally
- Running integration tests (WireMock-based, no IBKR account needed)
- Running end-to-end tests (requires paper account setup)
- Code style — follow existing conventions, XML docs on all public APIs
- Commit message format
- How to add a new endpoint — Refit interface, model types, unit test, WireMock cassette
- How to report security issues — reference SECURITY.md
- Contributor License Agreement (if applicable)

### 17.10 README Structure

1. Badge row — NuGet version, build status, coverage, license
2. One-paragraph description
3. Legal disclaimer (brief, link to full disclaimer)
4. Quick start — `dotnet add package IbkrConduit`, DI registration, minimal usage example
5. Authentication setup — link to detailed OAuth setup guide
6. Features overview
7. Documentation links
8. IbkrConduit vs IBKR.Sdk.Client comparison table
9. Contributing
10. License

### 17.11 Samples Project

The `IbkrConduit.Samples` project demonstrates real-world usage patterns with no trading system context. Required samples:

- OAuth credential setup and DI registration
- Placing a limit order and handling the response
- Retrieving current positions
- Subscribing to real-time order updates via WebSocket
- Executing a Flex query and parsing open orders
- Multi-tenant setup with two paper accounts

---

## 18. Reference Materials

### 18.1 IBKR Official Documentation

- CP Web API v1.0: https://www.interactivebrokers.com/campus/ibkr-api-page/cpapi-v1/
- OAuth 1.0a Extended guide: https://www.interactivebrokers.com/campus/ibkr-api-page/oauth-1-0a-extended/
- Order Types reference: https://www.interactivebrokers.com/campus/ibkr-api-page/order-types/
- Web API Changelog: https://www.interactivebrokers.com/campus/ibkr-api-page/web-api-changelog/
- Flex Web Service: https://www.interactivebrokers.com/campus/ibkr-api-page/flex-web-service/
- Flex Web Service v3 configuration: https://www.ibkrguides.com/clientportal/performanceandstatements/flex3.htm
- OAuth Self-Service Portal: https://ndcdyn.interactivebrokers.com/sso/Login?action=OAUTH&RL=1&ip2loc=US

### 18.2 Reference Implementations

- IBind (Python) — most complete OAuth 1.0a reference: https://github.com/Voyz/ibind
- IBind OAuth wiki — detailed setup walkthrough: https://github.com/Voyz/ibind/wiki/OAuth-1.0a
- Hulkstance/interactive-brokers-oauth (C#): https://github.com/Hulkstance/interactive-brokers-oauth
- quentinadam/ibkr (Deno/TypeScript, clean OAuth implementation): https://jsr.io/@quentinadam/ibkr
- gabbersepp/ib-flex-reader (C# .NET Standard Flex client): https://github.com/gabbersepp/ib-flex-reader
- IBKR.Sdk.Client (existing .NET package, Web API 2.0): https://www.nuget.org/packages/IBKR.Sdk.Client

### 18.3 Design Decisions Log

| Decision | Choice | Rationale |
|---|---|---|
| API path | CP Web API 1.0 | OAuth 1.0a support, headless, REST/HTTP, multi-tenant friendly, stable and fully documented |
| Auth method | OAuth 1.0a only | Eliminates Selenium/Gateway dependency entirely |
| Language | C# / .NET | System.Security.Cryptography, no pyCrypto dependency |
| HTTP client | Refit + IHttpClientFactory | Interface-driven, eliminates boilerplate, clean DI |
| Retry | Polly via MS.Extensions.Http.Resilience | Standard .NET resilience pattern |
| Rate limiting | System.Threading.RateLimiting token bucket | Built-in, async wait-not-fail, per-tenant isolation |
| Multi-tenant HTTP | Named HttpClient per tenant | Clean isolation, bounded tenant count |
| Concurrency control | SemaphoreSlim(1,1) per tenant | Single refresh flight, others wait |
| Integration testing | WireMock.Net | Edge case control, no IBKR dependency in CI |
| Credential storage | Storage-agnostic (accepts RSA objects) | Library not opinionated on storage — consuming app decides |
| Flex auth | Separate long-lived Flex token | Independent from OAuth — different service, different auth mechanism |
| Naming | IbkrConduit | Descriptive, infrastructure-flavored, unique on NuGet and GitHub |
| License | MIT | Standard permissive license for infrastructure libraries |
| Web API version | 1.0 (not 2.0) | 2.0 still in beta, incomplete documentation — extend to 2.0 when GA |

---

*IbkrConduit — Architecture & Design Document — v1.0 — March 2026*

*IbkrConduit is not affiliated with, endorsed by, or supported by Interactive Brokers LLC.*
