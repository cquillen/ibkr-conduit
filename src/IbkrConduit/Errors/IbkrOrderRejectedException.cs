using System.Net;

namespace IbkrConduit.Errors;

/// <summary>
/// Thrown when an order is rejected by IBKR. These arrive as 200 OK with
/// <c>{"error": "..."}</c> in the body — the handler detects and throws this.
/// </summary>
public class IbkrOrderRejectedException : IbkrApiException
{
    /// <summary>The rejection reason from IBKR.</summary>
    public string RejectionMessage { get; }

    /// <summary>
    /// Creates a new <see cref="IbkrOrderRejectedException"/>.
    /// </summary>
    public IbkrOrderRejectedException(
        string rejectionMessage, string? rawResponseBody, string? requestUri)
        : base(HttpStatusCode.OK, rejectionMessage, rawResponseBody, requestUri)
    {
        RejectionMessage = rejectionMessage ?? throw new ArgumentNullException(nameof(rejectionMessage));
    }
}
