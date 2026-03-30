using System.Text.Json.Serialization;

namespace IbkrConduit.Session;

/// <summary>
/// Response from POST /logout.
/// </summary>
public class LogoutResponse
{
    /// <summary>
    /// Whether the logout was confirmed by the server.
    /// </summary>
    [JsonPropertyName("confirmed")]
    public bool Confirmed { get; init; }
}
