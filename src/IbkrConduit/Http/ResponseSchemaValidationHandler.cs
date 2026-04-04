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

        // For collection responses, validate the first element
        if (dtoInfo.IsCollection && jsonElement.ValueKind == JsonValueKind.Array)
        {
            if (jsonElement.GetArrayLength() == 0)
            {
                return;
            }

            jsonElement = jsonElement[0];
        }

        // For dictionary responses, validate the first value
        if (dtoInfo.IsDictionary && jsonElement.ValueKind == JsonValueKind.Object)
        {
            using var enumerator = jsonElement.EnumerateObject();
            if (!enumerator.MoveNext())
            {
                return;
            }

            jsonElement = enumerator.Current.Value;

            // Handle Dictionary<string, List<T>> -- get first element of the array
            if (jsonElement.ValueKind == JsonValueKind.Array)
            {
                if (jsonElement.GetArrayLength() == 0)
                {
                    return;
                }

                jsonElement = jsonElement[0];
            }
        }

        if (jsonElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var responseFields = new HashSet<string>();
        foreach (var prop in jsonElement.EnumerateObject())
        {
            responseFields.Add(prop.Name);
        }

        // Extra fields: in response but not in DTO (skip if DTO has [JsonExtensionData])
        var extraFields = fieldMap.HasExtensionData
            ? []
            : responseFields.Except(fieldMap.FieldNames).ToList();

        // Missing fields: on DTO but not in response (only required fields)
        var missingFields = fieldMap.FieldNames
            .Where(f => !responseFields.Contains(f) && !fieldMap.IsOptional(f))
            .ToList();

        if (extraFields.Count == 0 && missingFields.Count == 0)
        {
            return;
        }

        if (_options.StrictResponseValidation)
        {
            throw new IbkrSchemaViolationException(path, dtoInfo.DtoType, extraFields, missingFields);
        }

        LogSchemaMismatch(path, dtoInfo.DtoType.Name,
            string.Join(", ", extraFields), string.Join(", ", missingFields));
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Response schema mismatch for {Path} -> {DtoTypeName}: Extra fields: [{ExtraFields}]. Missing fields: [{MissingFields}].")]
    private partial void LogSchemaMismatch(
        string path, string dtoTypeName,
        string extraFields, string missingFields);
}
