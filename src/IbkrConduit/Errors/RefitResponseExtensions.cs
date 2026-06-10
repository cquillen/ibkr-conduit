using System.Runtime.ExceptionServices;
using Refit;

namespace IbkrConduit.Errors;

/// <summary>
/// Adapts Refit 11's error model to preserve Refit 10 propagation semantics.
/// Refit 11 captures every exception thrown by <c>HttpClient.SendAsync</c> — caller
/// cancellation, transport failures, and exceptions thrown by our own
/// <see cref="System.Net.Http.DelegatingHandler"/>s (e.g. token refresh and schema
/// validation) — into <see cref="IApiResponse.Error"/> as an
/// <see cref="ApiRequestException"/> instead of letting them propagate.
/// </summary>
internal static class RefitResponseExtensions
{
    /// <summary>
    /// Re-throws the original exception (with its stack intact) when the response carries
    /// an <see cref="ApiRequestException"/> — i.e. the request failed before a response was
    /// received. No-op when the request reached the server (success, or a normal HTTP error
    /// represented by an <see cref="ApiException"/>).
    /// </summary>
    /// <param name="response">The Refit response to inspect.</param>
    public static void ThrowOnSendFailure(this IApiResponse response)
    {
        if (response.Error is ApiRequestException { InnerException: { } inner })
        {
            ExceptionDispatchInfo.Capture(inner).Throw();
        }
    }
}
