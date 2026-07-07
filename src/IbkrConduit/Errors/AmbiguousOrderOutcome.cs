using System.Net;
using System.Net.Http;

namespace IbkrConduit.Errors;

/// <summary>
/// Internal marker stashed in <see cref="HttpRequestMessage.Options"/> by
/// <c>TokenRefreshHandler</c> when it gates the automatic 401 replay of an order-mutating POST
/// (ADR-0003). <c>ResultFactory</c> reads it back off the response's request and surfaces the
/// public <see cref="IbkrAmbiguousOrderError"/>. This is the "return a response the pipeline
/// converts to the ambiguous error" hand-off — no exception crosses the handler boundary.
/// </summary>
/// <param name="Endpoint">The order endpoint whose POST was gated (absolute path).</param>
/// <param name="OriginalStatusCode">The status of the original (un-replayed) response — always 401.</param>
/// <param name="ReauthSucceeded">Whether the re-authentication triggered by the 401 succeeded.</param>
internal sealed record AmbiguousOrderOutcome(
    string? Endpoint,
    HttpStatusCode OriginalStatusCode,
    bool ReauthSucceeded)
{
    /// <summary>Key under which the marker is stored in <see cref="HttpRequestMessage.Options"/>.</summary>
    internal const string OptionKey = "IbkrConduit.AmbiguousOrderOutcome";
}
