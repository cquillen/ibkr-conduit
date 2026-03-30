using System.Text.Json.Serialization;

namespace IbkrConduit.Session;

/// <summary>
/// Response from POST /iserver/auth/ssodh/init.
/// </summary>
public class SsodhInitResponse
{
    /// <summary>
    /// Whether the session is authenticated.
    /// </summary>
    [JsonPropertyName("authenticated")]
    public bool Authenticated { get; init; }

    /// <summary>
    /// Whether the session is connected to the backend.
    /// </summary>
    [JsonPropertyName("connected")]
    public bool Connected { get; init; }

    /// <summary>
    /// Whether this session is competing with another.
    /// </summary>
    [JsonPropertyName("competing")]
    public bool Competing { get; init; }
}
