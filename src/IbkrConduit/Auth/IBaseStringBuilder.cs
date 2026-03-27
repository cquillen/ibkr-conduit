using System.Collections.Generic;

namespace IbkrConduit.Auth;

/// <summary>
/// Strategy interface for OAuth base string construction.
/// </summary>
public interface IBaseStringBuilder
{
    /// <summary>
    /// Builds the OAuth base string from the HTTP method, URL, and sorted parameters.
    /// </summary>
    string Build(string method, string url, SortedDictionary<string, string> parameters);
}
