using System.Text.Json.Serialization;

namespace IbkrConduit.Session;

/// <summary>
/// Response from POST /tickle. Contains session status and optional iserver auth status.
/// </summary>
public class TickleResponse
{
    /// <summary>
    /// The session identifier.
    /// </summary>
    [JsonPropertyName("session")]
    public string Session { get; init; } = string.Empty;

    /// <summary>
    /// Optional iserver status including authentication state.
    /// </summary>
    [JsonPropertyName("iserver")]
    public TickleIserverStatus? Iserver { get; init; }
}

/// <summary>
/// Iserver status block within a tickle response.
/// </summary>
public class TickleIserverStatus
{
    /// <summary>
    /// Authentication status of the iserver connection.
    /// </summary>
    [JsonPropertyName("authStatus")]
    public TickleAuthStatus? AuthStatus { get; init; }
}

/// <summary>
/// Authentication status details from a tickle response.
/// </summary>
public class TickleAuthStatus
{
    /// <summary>
    /// Whether the session is authenticated.
    /// </summary>
    [JsonPropertyName("authenticated")]
    public bool Authenticated { get; init; }

    /// <summary>
    /// Whether this session is competing with another.
    /// </summary>
    [JsonPropertyName("competing")]
    public bool Competing { get; init; }

    /// <summary>
    /// Whether the session is connected to the backend.
    /// </summary>
    [JsonPropertyName("connected")]
    public bool Connected { get; init; }
}
