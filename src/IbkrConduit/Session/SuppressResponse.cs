using System.Text.Json.Serialization;

namespace IbkrConduit.Session;

/// <summary>
/// Response from POST /iserver/questions/suppress.
/// </summary>
public class SuppressResponse
{
    /// <summary>
    /// Status of the suppression request.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;
}
