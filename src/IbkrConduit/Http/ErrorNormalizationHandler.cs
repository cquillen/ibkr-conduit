using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using IbkrConduit.Errors;

namespace IbkrConduit.Http;

/// <summary>
/// Normalizes IBKR API error responses by remapping misleading status codes
/// and detecting error patterns hidden in 200 OK responses.
/// </summary>
internal class ErrorNormalizationHandler : DelegatingHandler
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var path = request.RequestUri?.AbsolutePath;

        var originalContent = response.Content;
        var contentType = originalContent?.Headers.ContentType;
        var body = originalContent is not null
            ? await originalContent.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)
            : null;

        if (!response.IsSuccessStatusCode)
        {
            HandleNonSuccess(response.StatusCode, body, path, response.Headers);
        }
        else
        {
            HandleSuccess(body, contentType, path);
        }

        // Re-buffer the body so downstream consumers can still read it
        if (body is not null)
        {
            response.Content = new StringContent(body, Encoding.UTF8);
            if (contentType is not null)
            {
                response.Content.Headers.ContentType = contentType;
            }
            originalContent?.Dispose();
        }

        return response;
    }

    private static void HandleNonSuccess(
        HttpStatusCode statusCode, string? body, string? path,
        HttpResponseHeaders responseHeaders)
    {
        var errorMessage = TryParseErrorMessage(body);

        switch (statusCode)
        {
            case HttpStatusCode.TooManyRequests:
                TimeSpan? retryAfter = null;
                if (responseHeaders.RetryAfter?.Delta is not null)
                {
                    retryAfter = responseHeaders.RetryAfter.Delta;
                }
                else if (responseHeaders.TryGetValues("Retry-After", out var values))
                {
                    var raw = values.FirstOrDefault();
                    if (raw is not null && int.TryParse(raw, out var seconds))
                    {
                        retryAfter = TimeSpan.FromSeconds(seconds);
                    }
                }

                throw new IbkrApiException(
                    new IbkrRateLimitError(HttpStatusCode.TooManyRequests, errorMessage, body, path, retryAfter));

            case HttpStatusCode.Unauthorized:
                // Let 401 pass through — TokenRefreshHandler (upstream) handles
                // re-authentication and retry. Throwing here would prevent retry.
                return;

            default:
                var remapped = RemapStatusCode(statusCode, path);
                throw new IbkrApiException(
                    new IbkrApiError(remapped, errorMessage, body, path));
        }
    }

    private static HttpStatusCode RemapStatusCode(HttpStatusCode original, string? path)
    {
        if (path is null)
        {
            return original;
        }

        return original switch
        {
            HttpStatusCode.InternalServerError when path.Contains("/iserver/marketdata/unsubscribe") =>
                HttpStatusCode.NotFound,
            HttpStatusCode.InternalServerError when path.Contains("/iserver/auth/ssodh/init") =>
                HttpStatusCode.BadRequest,
            HttpStatusCode.ServiceUnavailable when
                path.Contains("/iserver/dynaccount") || path.Contains("/iserver/account/search") =>
                HttpStatusCode.Forbidden,
            HttpStatusCode.ServiceUnavailable when path.Contains("/iserver/account/order/status") =>
                HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable when path.Contains("/iserver/reply/") =>
                HttpStatusCode.Gone,
            _ => original,
        };
    }

    private static void HandleSuccess(
        string? body, MediaTypeHeaderValue? contentType, string? requestUri)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        var mediaType = contentType?.MediaType;
        if (mediaType is not null && !mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var trimmed = body.TrimStart();
        if (trimmed.StartsWith('['))
        {
            return;
        }

        IbkrErrorBody? errorBody;
        try
        {
            errorBody = JsonSerializer.Deserialize<IbkrErrorBody>(body, _jsonOptions);
        }
        catch (JsonException)
        {
            return;
        }

        if (errorBody is null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(errorBody.Error))
        {
            throw new IbkrApiException(
                new IbkrOrderRejectedError(errorBody.Error, body, requestUri));
        }

        if (errorBody.Success == false)
        {
            throw new IbkrApiException(
                new IbkrApiError(HttpStatusCode.BadRequest, errorBody.FailureList, body, requestUri));
        }
    }

    private static string? TryParseErrorMessage(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            var errorBody = JsonSerializer.Deserialize<IbkrErrorBody>(body, _jsonOptions);
            return errorBody?.Error;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
