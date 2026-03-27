using System.Collections.Generic;
using System.Linq;

namespace IbkrConduit.Auth;

/// <summary>
/// Builds the standard OAuth base string: METHOD&amp;encoded_url&amp;encoded_params.
/// Used for all regular API requests.
/// </summary>
public class StandardBaseStringBuilder : IBaseStringBuilder
{
    /// <inheritdoc />
    public string Build(string method, string url, SortedDictionary<string, string> parameters)
    {
        var paramString = string.Join("&",
            parameters.Select(kvp => $"{kvp.Key}={kvp.Value}"));

        var encodedUrl = OAuthEncoding.QuotePlus(url);
        var encodedParams = OAuthEncoding.QuotePlus(paramString);

        return $"{method}&{encodedUrl}&{encodedParams}";
    }
}
