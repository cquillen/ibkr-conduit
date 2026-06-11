using System.Runtime.ExceptionServices;
using System.Threading;
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

    /// <summary>
    /// Refit 11 wraps exceptions thrown by <c>HttpClient.SendAsync</c> — including caller
    /// cancellation — in <see cref="ApiRequestException"/> for raw (non-<see cref="IApiResponse"/>)
    /// interface methods. When <paramref name="cancellationToken"/> has been cancelled and the wrapped
    /// exception is an <see cref="OperationCanceledException"/>, this re-throws the original
    /// cancellation (preserving its stack) so callers observe <see cref="OperationCanceledException"/>
    /// exactly as they did under Refit 10. Otherwise it returns normally, leaving the
    /// <see cref="ApiRequestException"/> for the caller to handle.
    /// </summary>
    /// <param name="error">The Refit send-failure exception to inspect.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    public static void RethrowIfWrappedCancellation(this ApiRequestException error, CancellationToken cancellationToken)
    {
        if (error.InnerException is OperationCanceledException oce && cancellationToken.IsCancellationRequested)
        {
            ExceptionDispatchInfo.Capture(oce).Throw();
        }
    }

    /// <summary>
    /// Re-throws (with original type and stack, via <see cref="System.Runtime.ExceptionServices.ExceptionDispatchInfo"/>)
    /// the original exception that Refit 11 wrapped in this <see cref="ApiRequestException"/> for a raw
    /// (non-<see cref="IApiResponse"/>) interface method, so callers observe the same exception type they
    /// did under Refit 10. No-op when there is no inner exception.
    /// </summary>
    /// <param name="error">The Refit send-failure exception to unwrap.</param>
    public static void RethrowOriginal(this ApiRequestException error)
    {
        if (error.InnerException is { } inner)
        {
            ExceptionDispatchInfo.Capture(inner).Throw();
        }
    }
}
