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
/// Error from a Flex Web Service query. Carries the numeric error code,
/// the canonical description from IBKR's documentation, and whether the
/// error is retryable. StatusCode is null because Flex returns all logical
/// errors with HTTP 200 — the actual error information is in the XML body.
/// </summary>
[ExcludeFromCodeCoverage]
public record IbkrFlexError(
    int ErrorCode,
    string? CodeDescription,
    bool IsRetryable,
    string? Message,
    string? RawBody,
    string? RequestPath)
    : IbkrError(null, Message, RawBody, RequestPath);

/// <summary>
/// Hidden error — IBKR returned 200 OK with an error body on a non-order endpoint.
/// </summary>
[ExcludeFromCodeCoverage]
public record IbkrHiddenError(
    string? Message,
    string? RawBody,
    string? RequestPath)
    : IbkrError(HttpStatusCode.OK, Message, RawBody, RequestPath);

/// <summary>
/// Ambiguous order outcome — an order-mutating request was transmitted but whether IBKR processed it is
/// unknown, so the library never silently re-sends it (a replay could double-submit a live order). This
/// is produced for two situations:
/// <list type="bullet">
///   <item>an order-mutating POST (place, modify, or reply) received a <b>401</b> and was deliberately
///     <b>not</b> replayed (ADR-0003, design doc §9.9); or</item>
///   <item>a <b>reply</b> to a confirmation returned a <b>503</b> (an invalidated confirmation window —
///     ADR-0006, design doc §9.10): a live probe observed the invalidated order go live <i>after</i> its
///     reply 503'd, so this is ambiguous, not a definitive refusal.</item>
/// </list>
/// <para>
/// <b>Consumer obligation:</b> treat this as "sent — outcome unknown" (never as a definitive refusal, and
/// never re-place). Reconcile via <c>GetLiveOrdersAsync</c> / <c>GetTradesAsync</c> (matching on your
/// <c>cOID</c>) before resubmitting. <c>StatusCode</c> is the status of the original response — <c>401</c>
/// for the replay-gate case, <c>503</c> for the invalidated-reply case; <c>RequestPath</c> is the order
/// endpoint. <see cref="ReauthSucceeded"/> reports whether the re-authentication triggered by a 401
/// succeeded; it is <c>false</c> and <b>not applicable</b> when no 401/reauth was involved (the 503 case).
/// </para>
/// </summary>
[ExcludeFromCodeCoverage]
public record IbkrAmbiguousOrderError(
    HttpStatusCode? StatusCode,
    string? Message,
    string? RawBody,
    string? RequestPath,
    bool ReauthSucceeded)
    : IbkrError(StatusCode, Message, RawBody, RequestPath);
