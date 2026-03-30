using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace IbkrConduit.Session;

/// <summary>
/// DelegatingHandler that detects 401 responses, triggers session re-authentication
/// via <see cref="ISessionManager"/>, and retries the request once. Tickle requests
/// are excluded from retry to avoid masking dead session detection.
/// </summary>
internal sealed class TokenRefreshHandler : DelegatingHandler
{
    private readonly ISessionManager _sessionManager;

    /// <summary>
    /// Creates a new token refresh handler.
    /// </summary>
    /// <param name="sessionManager">Session manager for re-authentication.</param>
    public TokenRefreshHandler(ISessionManager sessionManager)
    {
        _sessionManager = sessionManager;
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
        await _sessionManager.ReauthenticateAsync(cancellationToken);

        // Clone the request for retry
        using var retryRequest = CloneRequest(request, bufferedContent, contentType);

        // Dispose the original 401 response
        response.Dispose();

        return await base.SendAsync(retryRequest, cancellationToken);
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
