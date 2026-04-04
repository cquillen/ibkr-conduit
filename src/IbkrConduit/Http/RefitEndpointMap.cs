using System.Reflection;
using System.Text.RegularExpressions;
using Refit;

namespace IbkrConduit.Http;

/// <summary>
/// Maps Refit interface endpoints to their response DTO types using reflection.
/// Built once at startup and used by response schema validation
/// to look up the expected DTO type for a given request.
/// </summary>
internal sealed class RefitEndpointMap
{
    private readonly List<EndpointEntry> _entries;

    private RefitEndpointMap(List<EndpointEntry> entries) => _entries = entries;

    /// <summary>
    /// Builds the endpoint map by scanning the specified Refit interface types.
    /// </summary>
    public static RefitEndpointMap Build(IReadOnlyList<Type> refitInterfaceTypes)
    {
        var entries = new List<EndpointEntry>();

        foreach (var interfaceType in refitInterfaceTypes)
        {
            foreach (var method in interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var (httpMethod, pathTemplate) = ExtractHttpMethodAndPath(method);
                if (httpMethod is null || pathTemplate is null)
                {
                    continue;
                }

                var dtoInfo = UnwrapReturnType(method.ReturnType);
                if (dtoInfo is null)
                {
                    continue;
                }

                var paramCount = Regex.Count(pathTemplate, @"\{[^}]+\}");
                var pathRegex = ConvertPathTemplateToRegex(pathTemplate);
                entries.Add(new EndpointEntry(httpMethod, pathRegex, paramCount, dtoInfo));
            }
        }

        // Sort by parameter count so literal paths match before parameterized ones
        entries.Sort((a, b) => a.ParamCount.CompareTo(b.ParamCount));

        return new RefitEndpointMap(entries);
    }

    /// <summary>
    /// Tries to find the DTO type for the given HTTP method and request path.
    /// Returns null if no mapping is found.
    /// </summary>
    public EndpointDtoInfo? TryGetDtoType(HttpMethod httpMethod, string requestPath)
    {
        foreach (var entry in _entries)
        {
            if (entry.HttpMethod == httpMethod && entry.PathRegex.IsMatch(requestPath))
            {
                return entry.DtoInfo;
            }
        }

        return null;
    }

    private static (HttpMethod? Method, string? Path) ExtractHttpMethodAndPath(MethodInfo method)
    {
        var getAttr = method.GetCustomAttribute<GetAttribute>();
        if (getAttr is not null)
        {
            return (HttpMethod.Get, getAttr.Path);
        }

        var postAttr = method.GetCustomAttribute<PostAttribute>();
        if (postAttr is not null)
        {
            return (HttpMethod.Post, postAttr.Path);
        }

        var deleteAttr = method.GetCustomAttribute<DeleteAttribute>();
        if (deleteAttr is not null)
        {
            return (HttpMethod.Delete, deleteAttr.Path);
        }

        var putAttr = method.GetCustomAttribute<PutAttribute>();
        if (putAttr is not null)
        {
            return (HttpMethod.Put, putAttr.Path);
        }

        return (null, null);
    }

    private static EndpointDtoInfo? UnwrapReturnType(Type returnType)
    {
        // Must be Task<T>
        if (!returnType.IsGenericType || returnType.GetGenericTypeDefinition() != typeof(Task<>))
        {
            // Task (void) -- skip
            return null;
        }

        var innerType = returnType.GetGenericArguments()[0];

        // IApiResponse<string> -- raw string response, skip
        if (innerType.IsGenericType &&
            innerType.GetGenericTypeDefinition() == typeof(IApiResponse<>) &&
            innerType.GetGenericArguments()[0] == typeof(string))
        {
            return null;
        }

        // List<T> -- validate element type T
        if (innerType.IsGenericType && innerType.GetGenericTypeDefinition() == typeof(List<>))
        {
            var elementType = innerType.GetGenericArguments()[0];
            return new EndpointDtoInfo(elementType, IsCollection: true, IsDictionary: false);
        }

        // Dictionary<string, T> -- validate value type T
        if (innerType.IsGenericType && innerType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            var args = innerType.GetGenericArguments();
            if (args[0] == typeof(string))
            {
                var valueType = args[1];
                // Dictionary<string, List<T>> -- e.g., GetFuturesBySymbolAsync
                if (valueType.IsGenericType && valueType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var listElement = valueType.GetGenericArguments()[0];
                    return new EndpointDtoInfo(listElement, IsCollection: true, IsDictionary: true);
                }

                return new EndpointDtoInfo(valueType, IsCollection: false, IsDictionary: true);
            }
        }

        // OneOf<T1, T2> -- skip (response shape varies)
        if (innerType.IsGenericType && innerType.FullName?.StartsWith("OneOf.OneOf", StringComparison.Ordinal) == true)
        {
            return null;
        }

        // Plain T
        return new EndpointDtoInfo(innerType, IsCollection: false, IsDictionary: false);
    }

    private static Regex ConvertPathTemplateToRegex(string pathTemplate)
    {
        // Replace {param} placeholders before escaping, then escape the rest
        var withPlaceholders = Regex.Replace(pathTemplate, @"\{[^}]+\}", "<<PARAM>>");
        var escaped = Regex.Escape(withPlaceholders);
        var pattern = escaped.Replace("<<PARAM>>", "[^/]+");
        return new Regex($"^{pattern}$", RegexOptions.Compiled);
    }

    private sealed record EndpointEntry(
        HttpMethod HttpMethod,
        Regex PathRegex,
        int ParamCount,
        EndpointDtoInfo DtoInfo);
}

/// <summary>
/// Information about the DTO type expected for an endpoint response.
/// </summary>
internal sealed record EndpointDtoInfo(
    Type DtoType,
    bool IsCollection,
    bool IsDictionary);
