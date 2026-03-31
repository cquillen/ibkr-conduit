using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Diagnostics;
using IbkrConduit.Session;

namespace IbkrConduit.Auth;

/// <summary>
/// DelegatingHandler that signs outgoing HTTP requests with OAuth HMAC-SHA256
/// using the Live Session Token. Ensures the brokerage session is initialized
/// before signing. Also sets required HTTP headers that the IBKR API gateway
/// (Akamai CDN) expects on every request.
/// </summary>
public class OAuthSigningHandler : DelegatingHandler
{
    private static readonly ProductInfoHeaderValue _defaultUserAgent = new("IbkrConduit", "1.0");

    private static readonly Histogram<double> _requestDuration =
        IbkrConduitDiagnostics.Meter.CreateHistogram<double>("ibkr.conduit.http.request.duration", "ms");

    private static readonly Counter<long> _requestCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.http.request.count");

    private static readonly UpDownCounter<long> _activeRequests =
        IbkrConduitDiagnostics.Meter.CreateUpDownCounter<long>("ibkr.conduit.http.active_requests");

    private readonly ISessionTokenProvider _tokenProvider;
    private readonly string _consumerKey;
    private readonly string _accessToken;
    private readonly ISessionManager? _sessionManager;

    /// <summary>
    /// Creates a new signing handler with optional session management.
    /// </summary>
    public OAuthSigningHandler(
        ISessionTokenProvider tokenProvider,
        string consumerKey,
        string accessToken,
        ISessionManager? sessionManager = null)
    {
        _tokenProvider = tokenProvider;
        _consumerKey = consumerKey;
        _accessToken = accessToken;
        _sessionManager = sessionManager;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Http.Request");
        activity?.SetTag(LogFields.Method, request.Method.Method);
        activity?.SetTag("url", request.RequestUri?.ToString());

        if (_sessionManager != null)
        {
            await _sessionManager.EnsureInitializedAsync(cancellationToken);
        }

        var lst = await _tokenProvider.GetLiveSessionTokenAsync(cancellationToken);

        var signer = new HmacSha256Signer(lst.Token);
        var baseStringBuilder = new StandardBaseStringBuilder();
        var headerBuilder = new OAuthHeaderBuilder(signer, baseStringBuilder);

        var url = request.RequestUri!.ToString();
        var method = request.Method.Method;

        var authHeaderValue = headerBuilder.Build(method, url, _consumerKey, _accessToken);

        // Use TryAddWithoutValidation since the OAuth header format doesn't fit
        // the standard AuthenticationHeaderValue scheme/parameter model
        request.Headers.TryAddWithoutValidation("Authorization", authHeaderValue);

        // IBKR's API gateway (Akamai CDN) returns 403 if no User-Agent is present
        if (request.Headers.UserAgent.Count == 0)
        {
            request.Headers.UserAgent.Add(_defaultUserAgent);
        }

        var endpoint = request.RequestUri?.AbsolutePath ?? "unknown";
        _activeRequests.Add(1);
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            sw.Stop();

            var statusCode = (int)response.StatusCode;
            activity?.SetTag(LogFields.StatusCode, statusCode);

            var tags = new TagList
            {
                { LogFields.Endpoint, endpoint },
                { LogFields.Method, method },
                { LogFields.StatusCode, statusCode },
            };
            _requestDuration.Record(sw.Elapsed.TotalMilliseconds, tags);
            _requestCount.Add(1, tags);

            return response;
        }
        finally
        {
            _activeRequests.Add(-1);
        }
    }
}
