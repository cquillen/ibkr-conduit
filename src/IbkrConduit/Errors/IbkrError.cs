using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace IbkrConduit.Errors;

/// <summary>
/// Base type for all IBKR API errors. Use pattern matching to discriminate subtypes.
/// </summary>
[ExcludeFromCodeCoverage]
public abstract record IbkrError(
    HttpStatusCode? StatusCode,
    string? Message,
    string? RawBody,
    string? RequestPath);

/// <summary>
/// Generic API error for non-2xx responses that don't match a more specific subtype.
/// </summary>
[ExcludeFromCodeCoverage]
public record IbkrApiError(
    HttpStatusCode? StatusCode,
    string? Message,
    string? RawBody,
    string? RequestPath)
    : IbkrError(StatusCode, Message, RawBody, RequestPath);

/// <summary>
/// Session/authentication error — competing session or expired credentials.
/// </summary>
[ExcludeFromCodeCoverage]
public record IbkrSessionError(
    HttpStatusCode? StatusCode,
    string? Message,
    string? RawBody,
    string? RequestPath,
    bool IsCompeting)
    : IbkrError(StatusCode, Message, RawBody, RequestPath);

/// <summary>
/// Rate limit error from IBKR server (HTTP 429).
/// </summary>
[ExcludeFromCodeCoverage]
public record IbkrRateLimitError(
    HttpStatusCode? StatusCode,
    string? Message,
    string? RawBody,
    string? RequestPath,
    TimeSpan? RetryAfter)
    : IbkrError(StatusCode, Message, RawBody, RequestPath);

/// <summary>
/// Order rejected — IBKR returned 200 OK with an error body on an order endpoint.
/// </summary>
[ExcludeFromCodeCoverage]
public record IbkrOrderRejectedError(
    string RejectionMessage,
    string? RawBody,
    string? RequestPath)
    : IbkrError(HttpStatusCode.OK, RejectionMessage, RawBody, RequestPath);

/// <summary>
/// Hidden error — IBKR returned 200 OK with an error body on a non-order endpoint.
/// </summary>
[ExcludeFromCodeCoverage]
public record IbkrHiddenError(
    string? Message,
    string? RawBody,
    string? RequestPath)
    : IbkrError(HttpStatusCode.OK, Message, RawBody, RequestPath);
