using System.Text.Json.Serialization;

namespace IbkrConduit.Session;

/// <summary>
/// Request body for POST /iserver/questions/suppress.
/// </summary>
/// <param name="MessageIds">List of message IDs to suppress.</param>
public record SuppressRequest(
    [property: JsonPropertyName("messageIds")] List<string> MessageIds);
