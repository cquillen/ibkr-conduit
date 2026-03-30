using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Polly;

namespace IbkrConduit.Http;

/// <summary>
/// DelegatingHandler that wraps outgoing HTTP requests in a Polly resilience
/// pipeline. Retries transient errors (5xx, 408, 429) with exponential backoff
/// and jitter. Non-retryable errors (4xx) pass through immediately.
/// </summary>
internal sealed class ResilienceHandler : DelegatingHandler
{
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

    /// <summary>
    /// Creates a new resilience handler.
    /// </summary>
    /// <param name="pipeline">The Polly resilience pipeline for retry logic.</param>
    public ResilienceHandler(ResiliencePipeline<HttpResponseMessage> pipeline)
    {
        _pipeline = pipeline;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        await _pipeline.ExecuteAsync(
            async ct => await base.SendAsync(request, ct),
            cancellationToken);
}
