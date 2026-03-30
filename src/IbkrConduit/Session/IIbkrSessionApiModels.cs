using System.Text.Json.Serialization;

namespace IbkrConduit.Session;

/// <summary>
/// Request body for POST /iserver/auth/ssodh/init to initialize a brokerage session.
/// </summary>
/// <param name="Publish">Whether to publish the session.</param>
/// <param name="Compete">Whether to compete with existing sessions.</param>
public record SsodhInitRequest(
    [property: JsonPropertyName("publish")] bool Publish,
    [property: JsonPropertyName("compete")] bool Compete);

/// <summary>
/// Response from POST /iserver/auth/ssodh/init.
/// </summary>
/// <param name="Authenticated">Whether the session is authenticated.</param>
/// <param name="Connected">Whether the session is connected to the backend.</param>
/// <param name="Competing">Whether this session is competing with another.</param>
public record SsodhInitResponse(
    [property: JsonPropertyName("authenticated")] bool Authenticated,
    [property: JsonPropertyName("connected")] bool Connected,
    [property: JsonPropertyName("competing")] bool Competing);

/// <summary>
/// Response from POST /tickle. Contains session status and optional iserver auth status.
/// </summary>
/// <param name="Session">The session identifier.</param>
/// <param name="Iserver">Optional iserver status including authentication state.</param>
public record TickleResponse(
    [property: JsonPropertyName("session")] string Session,
    [property: JsonPropertyName("iserver")] TickleIserverStatus? Iserver);

/// <summary>
/// Iserver status block within a tickle response.
/// </summary>
/// <param name="AuthStatus">Authentication status of the iserver connection.</param>
public record TickleIserverStatus(
    [property: JsonPropertyName("authStatus")] TickleAuthStatus? AuthStatus);

/// <summary>
/// Authentication status details from a tickle response.
/// </summary>
/// <param name="Authenticated">Whether the session is authenticated.</param>
/// <param name="Competing">Whether this session is competing with another.</param>
/// <param name="Connected">Whether the session is connected to the backend.</param>
public record TickleAuthStatus(
    [property: JsonPropertyName("authenticated")] bool Authenticated,
    [property: JsonPropertyName("competing")] bool Competing,
    [property: JsonPropertyName("connected")] bool Connected);

/// <summary>
/// Request body for POST /iserver/questions/suppress.
/// </summary>
/// <param name="MessageIds">List of message IDs to suppress.</param>
public record SuppressRequest(
    [property: JsonPropertyName("messageIds")] List<string> MessageIds);

/// <summary>
/// Response from POST /iserver/questions/suppress.
/// </summary>
/// <param name="Status">Status of the suppression request.</param>
public record SuppressResponse(
    [property: JsonPropertyName("status")] string Status);

/// <summary>
/// Response from POST /logout.
/// </summary>
/// <param name="Confirmed">Whether the logout was confirmed by the server.</param>
public record LogoutResponse(
    [property: JsonPropertyName("confirmed")] bool Confirmed);
