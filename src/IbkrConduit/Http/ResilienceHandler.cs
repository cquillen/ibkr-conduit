using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Diagnostics;
using Polly;

namespace IbkrConduit.Http;

/// <summary>
/// DelegatingHandler that wraps outgoing HTTP requests in a Polly resilience
/// pipeline. Retries transient errors (5xx, 408, 429) with exponential backoff
/// and jitter. Non-retryable errors (4xx) pass through immediately.
/// </summary>
internal sealed class ResilienceHandler : DelegatingHandler
{
    private static readonly Counter<long> _status429Count =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.http.status_429.count");

    private static readonly Counter<long> _retryCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.http.retry.count");

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
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var attempt = 0;
        var endpoint = request.RequestUri?.AbsolutePath ?? "unknown";
        return await _pipeline.ExecuteAsync(
            async ct =>
            {
                attempt++;
                if (attempt > 1)
                {
                    using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Http.ResilienceRetry");
                    activity?.SetTag(LogFields.Attempt, attempt);

                    _retryCount.Add(1,
                        new KeyValuePair<string, object?>(LogFields.Endpoint, endpoint),
                        new KeyValuePair<string, object?>(LogFields.Attempt, attempt));
                }

                var response = await base.SendAsync(request, ct);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _status429Count.Add(1,
                        new KeyValuePair<string, object?>(LogFields.Endpoint, endpoint));
                }

                if (attempt > 1)
                {
                    Activity.Current?.SetTag(LogFields.StatusCode, (int)response.StatusCode);
                }

                return response;
            },
            cancellationToken);
    }
}
