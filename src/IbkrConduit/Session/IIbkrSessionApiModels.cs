using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrConduit.Session;

/// <summary>
/// Request body for POST /iserver/auth/ssodh/init to initialize a brokerage session.
/// </summary>
/// <param name="Publish">Whether to publish the session.</param>
/// <param name="Compete">Whether to compete with existing sessions.</param>
[ExcludeFromCodeCoverage]
public record SsodhInitRequest(
    [property: JsonPropertyName("publish")] bool Publish,
    [property: JsonPropertyName("compete")] bool Compete);

/// <summary>
/// Response from POST /iserver/auth/ssodh/init.
/// </summary>
/// <param name="Authenticated">Whether the session is authenticated.</param>
/// <param name="Connected">Whether the session is connected to the backend.</param>
/// <param name="Competing">Whether this session is competing with another.</param>
[ExcludeFromCodeCoverage]
public record SsodhInitResponse(
    [property: JsonPropertyName("authenticated")] bool Authenticated,
    [property: JsonPropertyName("connected")] bool Connected,
    [property: JsonPropertyName("competing")] bool Competing);

/// <summary>
/// Response from POST /tickle. Contains session status and optional iserver auth status.
/// </summary>
/// <param name="Session">The session identifier.</param>
/// <param name="Iserver">Optional iserver status including authentication state.</param>
[ExcludeFromCodeCoverage]
public record TickleResponse(
    [property: JsonPropertyName("session")] string Session,
    [property: JsonPropertyName("iserver")] TickleIserverStatus? Iserver);

/// <summary>
/// Iserver status block within a tickle response.
/// </summary>
/// <param name="AuthStatus">Authentication status of the iserver connection.</param>
[ExcludeFromCodeCoverage]
public record TickleIserverStatus(
    [property: JsonPropertyName("authStatus")] TickleAuthStatus? AuthStatus);

/// <summary>
/// Authentication status details from a tickle response.
/// </summary>
/// <param name="Authenticated">Whether the session is authenticated.</param>
/// <param name="Competing">Whether this session is competing with another.</param>
/// <param name="Connected">Whether the session is connected to the backend.</param>
[ExcludeFromCodeCoverage]
public record TickleAuthStatus(
    [property: JsonPropertyName("authenticated")] bool Authenticated,
    [property: JsonPropertyName("competing")] bool Competing,
    [property: JsonPropertyName("connected")] bool Connected);

/// <summary>
/// Request body for POST /iserver/questions/suppress.
/// </summary>
/// <param name="MessageIds">List of message IDs to suppress.</param>
[ExcludeFromCodeCoverage]
public record SuppressRequest(
    [property: JsonPropertyName("messageIds")] List<string> MessageIds);

/// <summary>
/// Response from POST /iserver/questions/suppress.
/// </summary>
/// <param name="Status">Status of the suppression request.</param>
[ExcludeFromCodeCoverage]
public record SuppressResponse(
    [property: JsonPropertyName("status")] string Status);

/// <summary>
/// Response from POST /logout.
/// </summary>
/// <param name="Confirmed">Whether the logout was confirmed by the server.</param>
[ExcludeFromCodeCoverage]
public record LogoutResponse(
    [property: JsonPropertyName("confirmed")] bool Confirmed);

/// <summary>
/// Response from POST /iserver/questions/suppress/reset.
/// </summary>
/// <param name="Status">Status of the reset request.</param>
[ExcludeFromCodeCoverage]
public record SuppressResetResponse(
    [property: JsonPropertyName("status")] string Status)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Response from GET /iserver/auth/status.
/// </summary>
/// <param name="Authenticated">Whether the session is authenticated.</param>
/// <param name="Competing">Whether this session is competing with another.</param>
/// <param name="Connected">Whether the session is connected to the backend.</param>
/// <param name="Fail">Failure reason, if any.</param>
/// <param name="Message">Optional status message.</param>
/// <param name="Prompts">Optional prompts from the server.</param>
[ExcludeFromCodeCoverage]
public record AuthStatusResponse(
    [property: JsonPropertyName("authenticated")] bool Authenticated,
    [property: JsonPropertyName("competing")] bool Competing,
    [property: JsonPropertyName("connected")] bool Connected,
    [property: JsonPropertyName("fail")] string? Fail,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("prompts")] List<string>? Prompts)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Response from POST /iserver/reauthenticate.
/// </summary>
/// <param name="Message">Status message from the server.</param>
[ExcludeFromCodeCoverage]
public record ReauthenticateResponse(
    [property: JsonPropertyName("message")] string? Message)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>
/// Response from GET /sso/validate.
/// </summary>
/// <param name="UserId">The authenticated user ID.</param>
/// <param name="Expire">Expiration timestamp.</param>
/// <param name="Result">Validation result indicator.</param>
/// <param name="AuthTime">Time of authentication.</param>
[ExcludeFromCodeCoverage]
public record SsoValidateResponse(
    [property: JsonPropertyName("USER_ID")] int UserId,
    [property: JsonPropertyName("expire")] long Expire,
    [property: JsonPropertyName("RESULT")] bool Result,
    [property: JsonPropertyName("AUTH_TIME")] long AuthTime)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
