using System.Net;

namespace IbkrConduit.Errors;

/// <summary>
/// Thrown when the error indicates the session is dead and requires re-initialization.
/// </summary>
public class IbkrSessionException : IbkrApiException
{
    /// <summary>True if another session took over.</summary>
    public bool IsCompeting { get; }

    /// <summary>Reason from the auth status "fail" field, if available.</summary>
    public string? Reason { get; }

    /// <summary>
    /// Creates a new <see cref="IbkrSessionException"/>.
    /// </summary>
    public IbkrSessionException(
        bool isCompeting, string? reason,
        HttpStatusCode statusCode, string? errorMessage, string? rawResponseBody, string? requestUri)
        : base(statusCode, errorMessage, rawResponseBody, requestUri)
    {
        IsCompeting = isCompeting;
        Reason = reason;
    }
}
