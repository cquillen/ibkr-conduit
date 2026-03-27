using System;
using System.Collections.Generic;

namespace IbkrConduit.Auth;

/// <summary>
/// Prepends the decrypted access token secret hex to the standard base string.
/// Used exclusively for LST requests.
/// </summary>
public class PrependingBaseStringBuilder : IBaseStringBuilder
{
    private readonly string _prependHex;
    private readonly StandardBaseStringBuilder _inner = new();

    /// <summary>
    /// Creates a prepending builder with the given decrypted access token secret bytes.
    /// </summary>
    public PrependingBaseStringBuilder(byte[] decryptedAccessTokenSecret)
    {
        _prependHex = Convert.ToHexString(decryptedAccessTokenSecret).ToLowerInvariant();
    }

    /// <inheritdoc />
    public string Build(string method, string url, SortedDictionary<string, string> parameters) =>
        _prependHex + _inner.Build(method, url, parameters);
}
