using System.Net;

namespace IbkrConduit.Errors;

/// <summary>
/// Thrown when strict response validation detects fields in the API response
/// that don't match the expected DTO schema.
/// </summary>
public class IbkrSchemaViolationException : IbkrApiException
{
    /// <summary>The request path that produced the mismatched response.</summary>
    public string EndpointPath { get; }

    /// <summary>The DTO type the response was validated against.</summary>
    public Type DtoType { get; }

    /// <summary>JSON field names present in the response but not mapped to the DTO.</summary>
    public IReadOnlyList<string> ExtraFields { get; }

    /// <summary>DTO field names expected but missing from the response.</summary>
    public IReadOnlyList<string> MissingFields { get; }

    /// <summary>
    /// Creates a new <see cref="IbkrSchemaViolationException"/>.
    /// </summary>
    public IbkrSchemaViolationException(
        string endpointPath,
        Type dtoType,
        IReadOnlyList<string> extraFields,
        IReadOnlyList<string> missingFields)
        : base(
            HttpStatusCode.OK,
            FormatMessage(endpointPath, dtoType, extraFields, missingFields),
            rawResponseBody: null,
            requestUri: endpointPath)
    {
        EndpointPath = endpointPath;
        DtoType = dtoType;
        ExtraFields = extraFields;
        MissingFields = missingFields;
    }

    private static string FormatMessage(
        string endpointPath, Type dtoType,
        IReadOnlyList<string> extraFields, IReadOnlyList<string> missingFields)
    {
        var parts = new List<string>();
        if (extraFields.Count > 0)
        {
            parts.Add($"Extra fields: [{string.Join(", ", extraFields)}]");
        }

        if (missingFields.Count > 0)
        {
            parts.Add($"Missing fields: [{string.Join(", ", missingFields)}]");
        }

        return $"Response schema mismatch for {endpointPath} -> {dtoType.Name}: {string.Join(". ", parts)}.";
    }
}
