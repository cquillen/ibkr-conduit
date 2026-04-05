using System.Collections.Generic;
using System.Linq;

namespace IbkrConduit.Auth;

/// <summary>
/// Composes an <see cref="IOAuthSigner"/> and <see cref="IBaseStringBuilder"/>
/// to produce the full OAuth Authorization header value.
/// </summary>
internal class OAuthHeaderBuilder
{
    private readonly IOAuthSigner _signer;
    private readonly IBaseStringBuilder _baseStringBuilder;

    /// <summary>
    /// Creates a new header builder with the given signing and base string strategies.
    /// </summary>
    public OAuthHeaderBuilder(IOAuthSigner signer, IBaseStringBuilder baseStringBuilder)
    {
        _signer = signer;
        _baseStringBuilder = baseStringBuilder;
    }

    /// <summary>
    /// Builds the OAuth Authorization header value for the given request.
    /// </summary>
    public string Build(
        string method,
        string url,
        string consumerKey,
        string accessToken,
        IDictionary<string, string>? extraParams = null)
    {
        var nonce = OAuthEncoding.GenerateNonce();
        var timestamp = OAuthEncoding.GenerateTimestamp();

        var parameters = new SortedDictionary<string, string>
        {
            ["oauth_consumer_key"] = consumerKey,
            ["oauth_token"] = accessToken,
            ["oauth_signature_method"] = _signer.SignatureMethod,
            ["oauth_nonce"] = nonce,
            ["oauth_timestamp"] = timestamp,
        };

        if (extraParams != null)
        {
            foreach (var (key, value) in extraParams)
            {
                parameters[key] = value;
            }
        }

        var baseString = _baseStringBuilder.Build(method, url, parameters);
        var signature = _signer.Sign(baseString);
        var encodedSignature = OAuthEncoding.QuotePlus(signature);

        var headerParams = new SortedDictionary<string, string>(parameters)
        {
            ["oauth_signature"] = encodedSignature,
        };

        var paramPairs = headerParams.Select(kvp => $"{kvp.Key}=\"{kvp.Value}\"");
        return $"OAuth realm=\"limited_poa\", {string.Join(", ", paramPairs)}";
    }
}
