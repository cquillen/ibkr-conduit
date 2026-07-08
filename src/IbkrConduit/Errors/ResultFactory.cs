using System.Net;
using System.Text.Json;
using IbkrConduit.Http;
using Refit;

namespace IbkrConduit.Errors;

/// <summary>
/// Converts Refit <see cref="IApiResponse{T}"/> into <see cref="Result{T}"/>.
/// Handles all three IBKR error body formats: JSON error objects, empty bodies, and plain text/HTML.
/// </summary>
internal static class ResultFactory
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Converts a Refit response with a pre-deserialized body into a Result.
    /// </summary>
    /// <param name="response">The Refit response to classify.</param>
    /// <param name="requestPath">The request path, used for error attribution.</param>
    /// <param name="orderMutating">
    /// True when the response comes from an order-mutating endpoint (place, modify, reply). A 2xx body
    /// carrying a bare-object error then classifies as <see cref="IbkrOrderRejectedError"/> rather than
    /// the generic <see cref="IbkrHiddenError"/> (§9.9, ERR-4).
    /// </param>
    public static Result<T> FromResponse<T>(IApiResponse<T> response, string? requestPath = null, bool orderMutating = false)
    {
        response.ThrowOnSendFailure();

        // ADR-0003: an order-mutating POST that 401'd was gated (not replayed) by TokenRefreshHandler.
        // The outcome is ambiguous — surface the dedicated error rather than a generic 401 failure.
        var ambiguous = GetAmbiguousOrderOutcome(response);
        if (ambiguous is not null)
        {
            return Result<T>.Failure(BuildAmbiguousError(ambiguous, GetRawBody(response), requestPath));
        }

        // Prefer the raw body captured by ResponseBodyCaptureHandler (available for all status codes).
        // Fall back to response.Error?.Content (only populated for non-2xx by Refit).
        var rawBody = GetCapturedBody(response) ?? (response.Error as ApiException)?.Content ?? "";

        if (response.IsSuccessStatusCode)
        {
            // Check for hidden errors in 200 OK responses
            var hiddenError = DetectHiddenError(rawBody, requestPath, orderMutating);
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
    /// <param name="response">The Refit response to classify.</param>
    /// <param name="parser">Parser applied to the raw body on success.</param>
    /// <param name="requestPath">The request path, used for error attribution.</param>
    /// <param name="orderMutating">
    /// True when the response comes from an order-mutating endpoint (place, modify, reply). A 2xx body
    /// carrying a bare-object error then classifies as <see cref="IbkrOrderRejectedError"/> rather than
    /// the generic <see cref="IbkrHiddenError"/> (§9.9, ERR-4).
    /// </param>
    public static Result<T> FromResponse<T>(IApiResponse<string> response, Func<string, T> parser, string? requestPath = null, bool orderMutating = false)
    {
        response.ThrowOnSendFailure();

        // ADR-0003: same order-mutating-POST 401 gate as the pre-deserialized overload (covers reply).
        var ambiguous = GetAmbiguousOrderOutcome(response);
        if (ambiguous is not null)
        {
            var body = GetCapturedBody(response) ?? response.Content ?? (response.Error as ApiException)?.Content ?? "";
            return Result<T>.Failure(BuildAmbiguousError(ambiguous, body, requestPath));
        }

        var rawBody = GetCapturedBody(response) ?? response.Content ?? (response.Error as ApiException)?.Content ?? "";

        if (response.IsSuccessStatusCode)
        {
            // Check for hidden errors in 200 OK responses
            var hiddenError = DetectHiddenError(rawBody, requestPath, orderMutating);
            if (hiddenError is not null)
            {
                return Result<T>.Failure(hiddenError);
            }

            return Result<T>.Success(parser(rawBody));
        }

        return Result<T>.Failure(ParseError(response.StatusCode, rawBody, response.Headers, requestPath));
    }

    private static IbkrError ParseError(
        HttpStatusCode? statusCode, string rawBody,
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

    /// <summary>
    /// Detects a 200-with-error "hidden error" in a bare-object 2xx body. Returns
    /// <see cref="IbkrOrderRejectedError"/> when <paramref name="orderMutating"/> is true (place, modify,
    /// reply — the request path is known to be an order endpoint, §9.9/ERR-4) and the generic
    /// <see cref="IbkrHiddenError"/> for every other surface. Array-shaped bodies are skipped (order-array
    /// rejects are classified downstream by <c>ClassifyOrderResponses</c>). Null when no error is present.
    /// </summary>
    private static IbkrError? DetectHiddenError(string rawBody, string? requestPath, bool orderMutating)
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
            return BuildHiddenOrRejected(errorBody.Error, rawBody, requestPath, orderMutating);
        }

        if (errorBody.Success == false)
        {
            return BuildHiddenOrRejected(errorBody.FailureList ?? "Operation failed", rawBody, requestPath, orderMutating);
        }

        return null;
    }

    /// <summary>
    /// Builds the taxonomy-correct 200-with-error subtype: <see cref="IbkrOrderRejectedError"/> for an
    /// order-mutating endpoint, else the generic <see cref="IbkrHiddenError"/> (§9.9, ERR-4).
    /// </summary>
    private static IbkrError BuildHiddenOrRejected(string message, string rawBody, string? requestPath, bool orderMutating) =>
        orderMutating
            ? new IbkrOrderRejectedError(message, rawBody, requestPath)
            : new IbkrHiddenError(message, rawBody, requestPath);

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

    /// <summary>
    /// Builds the public <see cref="IbkrAmbiguousOrderError"/> from the internal marker stashed by
    /// <c>TokenRefreshHandler</c> when it gated an order-mutating POST's 401 replay (ADR-0003).
    /// </summary>
    private static IbkrAmbiguousOrderError BuildAmbiguousError(
        AmbiguousOrderOutcome outcome, string? rawBody, string? requestPath) =>
        new(
            outcome.OriginalStatusCode,
            "Order request was sent but the outcome is unknown after a 401; the request was not replayed. " +
            "Reconcile via live-order/trade queries (matching your cOID) before resubmitting.",
            rawBody,
            outcome.Endpoint ?? requestPath,
            outcome.ReauthSucceeded);

    /// <summary>
    /// Reads the <see cref="AmbiguousOrderOutcome"/> marker off the response's request options, set by
    /// <c>TokenRefreshHandler</c> when it gated an order-mutating POST replay. Null when absent.
    /// </summary>
    private static AmbiguousOrderOutcome? GetAmbiguousOrderOutcome<T>(IApiResponse<T> response)
    {
        if (response.RequestMessage?.Options.TryGetValue(
                new HttpRequestOptionsKey<AmbiguousOrderOutcome>(AmbiguousOrderOutcome.OptionKey),
                out var outcome) == true)
        {
            return outcome;
        }

        return null;
    }

    /// <summary>
    /// The raw response body for an order-path classification (AMB-4): the captured body when the
    /// request went through the pipeline, else the Refit error content. Null when neither is available.
    /// </summary>
    internal static string? GetRawBody<T>(IApiResponse<T> response) =>
        GetCapturedBody(response) ?? (response.Error as ApiException)?.Content;

    /// <summary>
    /// Reads the raw response body captured by <see cref="ResponseBodyCaptureHandler"/>
    /// from <see cref="HttpRequestMessage.Options"/>. Returns null if the body was not captured
    /// (e.g., request did not go through the consumer pipeline).
    /// </summary>
    private static string? GetCapturedBody<T>(IApiResponse<T> response)
    {
        if (response.RequestMessage?.Options.TryGetValue(
                new HttpRequestOptionsKey<string>(ResponseBodyCaptureHandler.RawBodyOptionKey),
                out var body) == true)
        {
            return body;
        }

        return null;
    }
}
