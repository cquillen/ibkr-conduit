using System.Text;
using System.Text.Json;
using IbkrConduit.Errors;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Http;

/// <summary>
/// Validates JSON response bodies against expected DTO schemas.
/// In strict mode, throws <see cref="IbkrSchemaViolationException"/> on field mismatches.
/// In non-strict mode, logs a warning and continues.
/// </summary>
internal sealed partial class ResponseSchemaValidationHandler : DelegatingHandler
{
    private readonly IbkrClientOptions _options;
    private readonly RefitEndpointMap _endpointMap;
    private readonly ILogger<ResponseSchemaValidationHandler> _logger;

    /// <summary>
    /// Creates a new <see cref="ResponseSchemaValidationHandler"/>.
    /// </summary>
    public ResponseSchemaValidationHandler(
        IbkrClientOptions options,
        RefitEndpointMap endpointMap,
        ILogger<ResponseSchemaValidationHandler> logger)
    {
        _options = options;
        _endpointMap = endpointMap;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return response;
        }

        var path = request.RequestUri?.AbsolutePath;
        if (path is null)
        {
            return response;
        }

        var dtoInfo = _endpointMap.TryGetDtoType(request.Method, path);
        if (dtoInfo is null)
        {
            if (_options.StrictResponseValidation)
            {
                throw new IbkrSchemaViolationException(
                    path, typeof(object), [], [$"No DTO mapping for {request.Method} {path}"]);
            }

            LogUnmappedEndpoint(request.Method.Method, path);
            return response;
        }

        var originalContent = response.Content;
        var contentType = originalContent?.Headers.ContentType;

        // Skip non-JSON responses
        var mediaType = contentType?.MediaType;
        if (mediaType is not null && !mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return response;
        }

        var body = originalContent is not null
            ? await originalContent.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)
            : null;

        if (!string.IsNullOrWhiteSpace(body))
        {
            ValidateResponseBody(body, path, dtoInfo);
        }

        // Re-buffer the body so downstream consumers can still read it
        if (body is not null)
        {
            response.Content = new StringContent(body, Encoding.UTF8);
            if (contentType is not null)
            {
                response.Content.Headers.ContentType = contentType;
            }

            originalContent?.Dispose();
        }

        return response;
    }

    private void ValidateResponseBody(string body, string path, EndpointDtoInfo dtoInfo)
    {
        var fieldMap = DtoFieldMap.Extract(dtoInfo.DtoType);
        JsonElement jsonElement;

        try
        {
            jsonElement = JsonSerializer.Deserialize<JsonElement>(body);
        }
        catch (JsonException)
        {
            return;
        }

        var knownFields = new HashSet<string>(fieldMap.FieldNames, StringComparer.Ordinal);

        // WIR-5 (1): validate EVERY element of a collection body, not just element[0] — a field
        // missing/renamed on a later element (e.g. price on trade #5 of a trades list) is invisible
        // if only the first element is checked. Endpoint payloads are bounded (live-orders caps at
        // 1000), so full-collection validation is safe. Extra/missing findings are unioned across all
        // elements and reported once so a drift on N elements is one signal, not N.
        var extraFields = new HashSet<string>(StringComparer.Ordinal);
        var missingFields = new HashSet<string>(StringComparer.Ordinal);

        foreach (var element in EnumerateValidationTargets(jsonElement, dtoInfo))
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var responseFields = new HashSet<string>(StringComparer.Ordinal);
            foreach (var prop in element.EnumerateObject())
            {
                responseFields.Add(prop.Name);
            }

            // Extra fields: in response but not in the DTO. WIR-5 (2): run this even when the DTO has
            // [JsonExtensionData] — an extension-data DTO silently absorbs a renamed field into its
            // AdditionalData bag, so without this diff a rename is invisible in that direction.
            foreach (var field in responseFields)
            {
                if (!knownFields.Contains(field))
                {
                    extraFields.Add(field);
                }
            }

            // Missing fields: on the DTO but not in the response (only required, non-optional fields).
            foreach (var field in fieldMap.FieldNames)
            {
                if (!responseFields.Contains(field) && !fieldMap.IsOptional(field))
                {
                    missingFields.Add(field);
                }
            }
        }

        if (extraFields.Count == 0 && missingFields.Count == 0)
        {
            return;
        }

        if (_options.StrictResponseValidation)
        {
            throw new IbkrSchemaViolationException(
                path, dtoInfo.DtoType, extraFields.ToList(), missingFields.ToList());
        }

        LogSchemaMismatch(path, dtoInfo.DtoType.Name,
            string.Join(", ", extraFields), string.Join(", ", missingFields));
    }

    /// <summary>
    /// Yields every JSON object that should be validated against the endpoint's DTO. A collection
    /// body yields all of its array elements; a dictionary body yields all of its values (and, for a
    /// <c>Dictionary&lt;string, List&lt;T&gt;&gt;</c>, all elements of every value array); a plain
    /// object yields itself. Empty arrays yield nothing (skip validation).
    /// </summary>
    private static IEnumerable<JsonElement> EnumerateValidationTargets(JsonElement root, EndpointDtoInfo dtoInfo)
    {
        if (dtoInfo.IsDictionary && root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in root.EnumerateObject())
            {
                var value = prop.Value;
                if (dtoInfo.IsCollection && value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in value.EnumerateArray())
                    {
                        yield return element;
                    }
                }
                else
                {
                    yield return value;
                }
            }

            yield break;
        }

        if (dtoInfo.IsCollection && root.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in root.EnumerateArray())
            {
                yield return element;
            }

            yield break;
        }

        yield return root;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Response schema mismatch for {Path} -> {DtoTypeName}: Extra fields: [{ExtraFields}]. Missing fields: [{MissingFields}].")]
    private partial void LogSchemaMismatch(
        string path, string dtoTypeName,
        string extraFields, string missingFields);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "No DTO mapping found for {Method} {Path} — endpoint will not be validated. Add the Refit interface to RefitEndpointMap.Build() in ServiceCollectionExtensions.")]
    private partial void LogUnmappedEndpoint(string method, string path);
}
