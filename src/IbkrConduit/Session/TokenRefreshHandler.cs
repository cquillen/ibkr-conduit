using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Diagnostics;
using IbkrConduit.Errors;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Session;

/// <summary>
/// DelegatingHandler that detects 401 responses, triggers session re-authentication
/// via <see cref="ISessionManager"/>, and retries the request once. Tickle requests
/// are excluded from retry to avoid masking dead session detection.
/// </summary>
internal sealed partial class TokenRefreshHandler : DelegatingHandler
{
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<TokenRefreshHandler> _logger;

    /// <summary>
    /// Creates a new token refresh handler.
    /// </summary>
    /// <param name="sessionManager">Session manager for re-authentication.</param>
    /// <param name="logger">Logger for 401 retry events.</param>
    public TokenRefreshHandler(ISessionManager sessionManager, ILogger<TokenRefreshHandler> logger)
    {
        _sessionManager = sessionManager;
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

        try
        {
            await _sessionManager.ReauthenticateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            LogReauthFailed(ex, requestPath);
            response.Dispose();
            throw new IbkrApiException(
                new IbkrSessionError(
                    HttpStatusCode.Unauthorized,
                    "Re-authentication failed — credentials may be invalidated",
                    "",
                    request.RequestUri?.AbsolutePath,
                    false),
                ex);
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
