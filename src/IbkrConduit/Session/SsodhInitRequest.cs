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
