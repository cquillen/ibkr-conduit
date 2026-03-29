using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace IbkrConduit.Auth;

/// <summary>
/// DelegatingHandler that signs outgoing HTTP requests with OAuth HMAC-SHA256
/// using the Live Session Token. Also sets required HTTP headers that the
/// IBKR API gateway (Akamai CDN) expects on every request.
/// </summary>
public class OAuthSigningHandler : DelegatingHandler
{
    private static readonly ProductInfoHeaderValue _defaultUserAgent = new("IbkrConduit", "1.0");

    private readonly ISessionTokenProvider _tokenProvider;
    private readonly string _consumerKey;
    private readonly string _accessToken;

    /// <summary>
    /// Creates a new signing handler.
    /// </summary>
    public OAuthSigningHandler(
        ISessionTokenProvider tokenProvider,
        string consumerKey,
        string accessToken)
    {
        _tokenProvider = tokenProvider;
        _consumerKey = consumerKey;
        _accessToken = accessToken;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
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

        return await base.SendAsync(request, cancellationToken);
    }
}
