using System.Net;

namespace IbkrConduit.Errors;

/// <summary>
/// Thrown when IBKR returns 429 Too Many Requests.
/// </summary>
public class IbkrRateLimitException : IbkrApiException
{
    /// <summary>Retry delay from the Retry-After header, if present.</summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>
    /// Creates a new <see cref="IbkrRateLimitException"/>.
    /// </summary>
    public IbkrRateLimitException(
        TimeSpan? retryAfter, string? errorMessage, string? rawResponseBody, string? requestUri)
        : base(HttpStatusCode.TooManyRequests, errorMessage, rawResponseBody, requestUri)
    {
        RetryAfter = retryAfter;
    }
}
