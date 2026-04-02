using System.Net;

namespace IbkrConduit.Errors;

/// <summary>
/// Base exception for all IBKR API errors after normalization.
/// The <see cref="StatusCode"/> reflects the normalized (remapped) status, not the original.
/// </summary>
public class IbkrApiException : Exception
{
    /// <summary>The normalized HTTP status code (after remapping).</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Parsed error message from the response body, if available.</summary>
    public string? ErrorMessage { get; }

    /// <summary>Full response body for diagnostics and logging.</summary>
    public string? RawResponseBody { get; }

    /// <summary>The request URI path that triggered the error.</summary>
    public string? RequestUri { get; }

    /// <summary>
    /// Creates a new <see cref="IbkrApiException"/>.
    /// </summary>
    public IbkrApiException(
        HttpStatusCode statusCode, string? errorMessage, string? rawResponseBody, string? requestUri)
        : base(errorMessage ?? $"IBKR API returned {(int)statusCode}")
    {
        StatusCode = statusCode;
        ErrorMessage = errorMessage;
        RawResponseBody = rawResponseBody;
        RequestUri = requestUri;
    }
}
