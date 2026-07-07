using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Diagnostics;
using IbkrConduit.Errors;
using IbkrConduit.Health;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Session;

/// <summary>
/// DelegatingHandler that detects 401 responses, triggers session re-authentication
/// via <see cref="ISessionManager"/>, and retries the request once. Tickle requests
/// are excluded from retry to avoid masking dead session detection.
/// <para>
/// ADR-0003 (design doc §9.9): order-mutating POSTs (place, modify, reply) are excluded from the
/// automatic replay. A replay after a 401 could double-submit a live order, and whether IBKR
/// processed the original before the 401 is unpinned. Re-auth still happens, but the original call is
/// not re-sent; the request is marked with an <see cref="AmbiguousOrderOutcome"/> that
/// <c>ResultFactory</c> converts into a public <see cref="IbkrAmbiguousOrderError"/>. Idempotent
/// requests (GET, DELETE cancel) and non-mutating POSTs (e.g. <c>/orders/whatif</c>) keep the replay.
/// </para>
/// </summary>
internal sealed partial class TokenRefreshHandler : DelegatingHandler
{
    // Order-mutating POST paths (ADR-0003): place (/iserver/account/{id}/orders), modify
    // (/iserver/account/{id}/order/{orderId}), and reply (/iserver/reply/{replyId}). Anchored to the
    // path end so /orders/whatif and /account/order/status/{id} are NOT matched.
    private static readonly Regex _orderMutatingPostPattern = new(
        @"/iserver/account/[^/]+/orders$|/iserver/account/[^/]+/order/[^/]+$|/iserver/reply/[^/]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private readonly ISessionManager _sessionManager;
    private readonly SessionHealthState _sessionHealthState;
    private readonly ILogger<TokenRefreshHandler> _logger;

    /// <summary>
    /// Creates a new token refresh handler.
    /// </summary>
    /// <param name="sessionManager">Session manager for re-authentication.</param>
    /// <param name="sessionHealthState">Shared session-health state — its competing verdict is the evidence propagated into the session error (ADR-0004 / GAP3-1).</param>
    /// <param name="logger">Logger for 401 retry events.</param>
    public TokenRefreshHandler(
        ISessionManager sessionManager,
        SessionHealthState sessionHealthState,
        ILogger<TokenRefreshHandler> logger)
    {
        _sessionManager = sessionManager;
        _sessionHealthState = sessionHealthState;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Buffer request content before sending (needed for replay on retry)
        byte[]? bufferedContent = null;
        string? contentType = null;
        if (request.Content != null)
        {
            bufferedContent = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            contentType = request.Content.Headers.ContentType?.ToString();
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // Skip retry for tickle requests — dead tickle means dead session
        if (request.RequestUri != null &&
            request.RequestUri.AbsolutePath.Contains("/tickle", StringComparison.OrdinalIgnoreCase))
        {
            return response;
        }

        // Trigger re-authentication
        var requestPath = request.RequestUri?.AbsolutePath ?? "unknown";
        LogReceived401(requestPath);

        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Http.TokenRefreshRetry");
        activity?.SetTag("original_status_code", (int)response.StatusCode);

        // ADR-0003: an order-mutating POST is never replayed — its outcome is ambiguous after a 401.
        var isOrderMutatingPost = IsOrderMutatingPost(request);

        var reauthSucceeded = true;
        try
        {
            await _sessionManager.ReauthenticateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            LogReauthFailed(ex, requestPath);

            // ADR-0003 money-safety gate wins over the ADR-0004 competing-session throw: for an
            // order-mutating POST the outcome stays ambiguous even when re-auth fails — the request
            // was sent and must be reconciled, never converted into a session-error throw (which a
            // consumer's trichotomy would read as a definitive refusal). Fall through to the ambiguous
            // gate below carrying reauthSucceeded=false; the competing-throw applies only to
            // non-order 401s.
            if (!isOrderMutatingPost)
            {
                response.Dispose();

                // GAP3-1 (ADR-0004): propagate the competing evidence into the surfaced session error
                // instead of the old hardcoded false. The evidence is the IsCompeting flag on a session
                // error the re-auth itself raised (ssodh authenticated=false path), falling back to the
                // current health snapshot's competing verdict (recorded by a tickle or an sts frame).
                var isCompeting = (ex as IbkrApiException)?.Error is IbkrSessionError sessionError
                    ? sessionError.IsCompeting
                    : _sessionHealthState.GetSnapshot().Competing;

                throw new IbkrApiException(
                    new IbkrSessionError(
                        HttpStatusCode.Unauthorized,
                        "Re-authentication failed — credentials may be invalidated",
                        "",
                        request.RequestUri?.AbsolutePath,
                        isCompeting),
                    ex);
            }

            reauthSucceeded = false;
        }

        if (isOrderMutatingPost)
        {
            // Do NOT re-send. Mark the request so ResultFactory surfaces IbkrAmbiguousOrderError, and
            // return the original 401 response for the pipeline to convert. This gate returns before
            // the post-retry competing check below, so an order-mutating POST 401 is never reclassified
            // as a competing-session error — the ambiguous outcome always wins (ADR-0003).
            activity?.SetTag("order_replay_gated", true);
            LogOrderReplayGated(requestPath, reauthSucceeded);
            request.Options.Set(
                new HttpRequestOptionsKey<AmbiguousOrderOutcome>(AmbiguousOrderOutcome.OptionKey),
                new AmbiguousOrderOutcome(requestPath, response.StatusCode, reauthSucceeded));
            return response;
        }

        // Clone the request for retry
        using var retryRequest = CloneRequest(request, bufferedContent, contentType);

        // Dispose the original 401 response
        response.Dispose();

        var retryResponse = await base.SendAsync(retryRequest, cancellationToken);

        // If retry also returns 401, this is likely IBKR returning 401 for a
        // non-auth reason (e.g., invalid account ID). Return the response as-is
        // so the facade can interpret it via ResultFactory.
        if (retryResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            LogRetryStillUnauthorized(requestPath);

            // GAP3-1: a post-retry 401 whose health snapshot shows a competing session is a
            // contested-session error, not a generic 401 — surface it as an IbkrSessionError with
            // IsCompeting so the consumer's session-loss recovery path can fire.
            if (_sessionHealthState.GetSnapshot().Competing)
            {
                retryResponse.Dispose();
                throw new IbkrApiException(new IbkrSessionError(
                    HttpStatusCode.Unauthorized,
                    "Request remained unauthorized after re-authentication — session is competing with another login",
                    "",
                    request.RequestUri?.AbsolutePath,
                    IsCompeting: true));
            }
        }
        else
        {
            LogRetrySucceeded(requestPath, (int)retryResponse.StatusCode);
        }

        return retryResponse;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Received 401 for {RequestPath}, triggering re-authentication")]
    private partial void LogReceived401(string requestPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Re-authentication failed, request to {RequestPath} still unauthorized")]
    private partial void LogRetryStillUnauthorized(string requestPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Re-authentication failed with exception for {RequestPath}")]
    private partial void LogReauthFailed(Exception exception, string requestPath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "401 retry succeeded for {RequestPath} with status {StatusCode}")]
    private partial void LogRetrySucceeded(string requestPath, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "401 on order-mutating POST {RequestPath} — replay gated (ADR-0003); outcome ambiguous (reauthSucceeded={ReauthSucceeded})")]
    private partial void LogOrderReplayGated(string requestPath, bool reauthSucceeded);

    /// <summary>
    /// Whether the request is an order-mutating POST (place, modify, or reply) whose 401 replay is
    /// gated by ADR-0003. Non-mutating POSTs such as <c>/orders/whatif</c> and GET/DELETE are excluded.
    /// </summary>
    private static bool IsOrderMutatingPost(HttpRequestMessage request)
    {
        if (request.Method != HttpMethod.Post)
        {
            return false;
        }

        var path = request.RequestUri?.AbsolutePath;
        return path is not null && _orderMutatingPostPattern.IsMatch(path);
    }

    private static HttpRequestMessage CloneRequest(
        HttpRequestMessage original, byte[]? bufferedContent, string? contentType)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);

        if (bufferedContent != null)
        {
            clone.Content = new ByteArrayContent(bufferedContent);
            if (contentType != null)
            {
                clone.Content.Headers.Remove("Content-Type");
                clone.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
            }
        }

        // Copy headers (skip Content headers, already set above)
        foreach (var header in original.Headers)
        {
            // Skip Authorization — it will be re-added by OAuthSigningHandler
            if (!string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}
