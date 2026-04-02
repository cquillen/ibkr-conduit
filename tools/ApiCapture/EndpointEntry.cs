namespace ApiCapture;

/// <summary>
/// Defines a single API endpoint to capture, including its expected response status.
/// URLs and bodies support <c>{accountId}</c> placeholders resolved at runtime.
/// </summary>
/// <param name="Category">Module grouping (e.g., "Session", "Portfolio"). Used for filtering and recording directory.</param>
/// <param name="Name">Descriptive name for this capture entry (e.g., "GetAccounts", "InvalidConid").</param>
/// <param name="Method">HTTP method.</param>
/// <param name="UrlTemplate">Relative URL with optional <c>{accountId}</c> placeholder.</param>
/// <param name="ExpectedStatus">Expected HTTP status code. Recording is deleted if actual status differs.</param>
/// <param name="BodyTemplate">Optional JSON request body with optional <c>{accountId}</c> placeholder.</param>
/// <param name="CaptureAs">Optional variable name to store a captured value from the response (e.g., <c>"orderId"</c>).</param>
/// <param name="CaptureJsonPath">Optional RFC 9535 JSONPath expression to extract a value from the response body (e.g., <c>"$[0].order_id"</c>).</param>
public record EndpointEntry(
    string Category,
    string Name,
    HttpMethod Method,
    string UrlTemplate,
    int ExpectedStatus,
    string? BodyTemplate = null,
    string? CaptureAs = null,
    string? CaptureJsonPath = null);
