using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Refit;

namespace IbkrConduit.Errors;

/// <summary>
/// Converts Refit <see cref="IApiResponse{T}"/> into <see cref="Result{T}"/>.
/// Handles all three IBKR error body formats: JSON error objects, empty bodies, and plain text/HTML.
/// </summary>
internal sealed partial class ResultFactory
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ILogger<ResultFactory> _logger;

    /// <summary>
    /// Creates a new <see cref="ResultFactory"/> instance.
    /// </summary>
    /// <param name="logger">Logger for debug-level response diagnostics.</param>
    public ResultFactory(ILogger<ResultFactory> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Converts a Refit response with a pre-deserialized body into a Result.
    /// </summary>
    public Result<T> FromResponse<T>(IApiResponse<T> response, string? requestPath = null)
    {
        var rawBody = response.Error?.Content ?? "";

        LogDeserializedResponse(requestPath ?? "unknown", (int)response.StatusCode, response.Content?.GetType().Name ?? "null", rawBody);

        if (response.IsSuccessStatusCode)
        {
            // Check for hidden errors in 200 OK responses
            var hiddenError = DetectHiddenError(rawBody, requestPath);
            if (hiddenError is not null)
            {
                return Result<T>.Failure(hiddenError);
            }

            if (response.Content is not null)
            {
                return Result<T>.Success(response.Content);
            }

            // 2xx but null content — deserialization failed
            return Result<T>.Failure(new IbkrApiError(
                response.StatusCode, "Response body could not be deserialized", rawBody, requestPath));
        }

        return Result<T>.Failure(ParseError(response.StatusCode, rawBody, response.Headers, requestPath));
    }

    /// <summary>
    /// Converts a Refit response with a raw string body into a Result using a custom parser.
    /// For <c>IApiResponse&lt;string&gt;</c>, the raw body is available via <c>Content</c>.
    /// </summary>
    public Result<T> FromResponse<T>(IApiResponse<string> response, Func<string, T> parser, string? requestPath = null)
    {
        var rawBody = response.Content ?? response.Error?.Content ?? "";

        LogRawResponse(requestPath ?? "unknown", (int)response.StatusCode, rawBody, response.Error?.Content ?? "");

        if (response.IsSuccessStatusCode)
        {
            // Check for hidden errors in 200 OK responses
            var hiddenError = DetectHiddenError(rawBody, requestPath);
            if (hiddenError is not null)
            {
                return Result<T>.Failure(hiddenError);
            }

            return Result<T>.Success(parser(rawBody));
        }

        return Result<T>.Failure(ParseError(response.StatusCode, rawBody, response.Headers, requestPath));
    }

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "ResultFactory {RequestPath}: HTTP {StatusCode}, ContentType={ContentType}, ErrorBody={ErrorBody}")]
    private partial void LogDeserializedResponse(string requestPath, int statusCode, string contentType, string errorBody);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "ResultFactory {RequestPath}: HTTP {StatusCode}, Body={Body}, ErrorBody={ErrorBody}")]
    private partial void LogRawResponse(string requestPath, int statusCode, string body, string errorBody);

    private static IbkrError ParseError(
        HttpStatusCode statusCode, string rawBody,
        System.Net.Http.Headers.HttpResponseHeaders? headers, string? requestPath)
    {
        // 429 — rate limit
        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            TimeSpan? retryAfter = null;
            if (headers?.RetryAfter?.Delta is not null)
            {
                retryAfter = headers.RetryAfter.Delta;
            }
            else if (headers is not null)
            {
                // Try raw header parsing
                if (headers.TryGetValues("Retry-After", out var values))
                {
                    var raw = values.FirstOrDefault();
                    if (raw is not null && int.TryParse(raw, out var seconds))
                    {
                        retryAfter = TimeSpan.FromSeconds(seconds);
                    }
                }
            }

            var msg = TryParseErrorMessage(rawBody);
            return new IbkrRateLimitError(statusCode, msg ?? "Rate limited", rawBody, requestPath, retryAfter);
        }

        // Try to parse JSON error body
        var errorMessage = TryParseErrorMessage(rawBody);
        return new IbkrApiError(statusCode, errorMessage, rawBody, requestPath);
    }

    private static IbkrHiddenError? DetectHiddenError(string rawBody, string? requestPath)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return null;
        }

        var trimmed = rawBody.TrimStart();
        if (trimmed.StartsWith('['))
        {
            return null;
        }

        IbkrErrorBody? errorBody;
        try
        {
            errorBody = JsonSerializer.Deserialize<IbkrErrorBody>(rawBody, _jsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (errorBody is null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(errorBody.Error))
        {
            return new IbkrHiddenError(errorBody.Error, rawBody, requestPath);
        }

        if (errorBody.Success == false)
        {
            return new IbkrHiddenError(errorBody.FailureList ?? "Operation failed", rawBody, requestPath);
        }

        return null;
    }

    private static string? TryParseErrorMessage(string? rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return null;
        }

        try
        {
            var errorBody = JsonSerializer.Deserialize<IbkrErrorBody>(rawBody, _jsonOptions);
            return errorBody?.Error;
        }
        catch (JsonException)
        {
            return rawBody;
        }
    }
}
