using System.Text.Json.Serialization;

namespace IbkrConduit.Http;

/// <summary>
/// Internal model for detecting error patterns in IBKR API response bodies.
/// </summary>
internal record IbkrErrorBody(
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("success")] bool? Success,
    [property: JsonPropertyName("failure_list")] string? FailureList,
    [property: JsonPropertyName("statusCode")] int? StatusCode);
