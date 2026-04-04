using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;

namespace IbkrConduit.Http;

/// <summary>
/// Extracts JSON field metadata from a DTO type by reading <see cref="JsonPropertyNameAttribute"/>
/// attributes from constructor parameters and properties. Used by response
/// schema validation to compare API responses against expected DTO shapes.
/// </summary>
internal static class DtoFieldMap
{
    private static readonly ConcurrentDictionary<Type, DtoFieldInfo> _cache = new();

    /// <summary>
    /// Extracts field metadata for the specified DTO type. Results are cached.
    /// </summary>
    public static DtoFieldInfo Extract(Type dtoType) =>
        _cache.GetOrAdd(dtoType, ExtractCore);

    private static DtoFieldInfo ExtractCore(Type dtoType)
    {
        var fields = new Dictionary<string, bool>(); // jsonName -> isOptional
        var nestedMaps = new Dictionary<string, DtoFieldInfo>();
        var hasExtensionData = false;

        // Check properties (covers both class-style { get; init; } and positional record generated properties)
        foreach (var prop in dtoType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetCustomAttribute<JsonExtensionDataAttribute>() is not null)
            {
                hasExtensionData = true;
                continue;
            }

            var jsonAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
            if (jsonAttr is null)
            {
                continue;
            }

            var jsonName = jsonAttr.Name;
            var isOptional = IsOptionalType(prop.PropertyType) || HasDefaultValue(dtoType, jsonName);
            fields[jsonName] = isOptional;

            // Check if this property is a nested DTO type (has its own JsonPropertyName fields)
            var elementType = GetNestedDtoType(prop.PropertyType);
            if (elementType is not null && elementType != dtoType)
            {
                nestedMaps[jsonName] = Extract(elementType);
            }
        }

        return new DtoFieldInfo(fields, hasExtensionData, nestedMaps);
    }

    private static bool IsOptionalType(Type type)
    {
        // Nullable<T> (value types)
        if (Nullable.GetUnderlyingType(type) is not null)
        {
            return true;
        }

        // Nullable reference types show up via NullabilityInfoContext in .NET 6+,
        // but for positional records with default = null, we check the constructor.
        // For simplicity, reference types that are not string are considered optional
        // if they are annotated as nullable via the NullabilityInfoContext.
        // However, the simplest reliable check: if it's a reference type,
        // check the NullableAttribute on the property.
        if (!type.IsValueType)
        {
            // Will be refined by HasDefaultValue check and NullableContext below
            return false;
        }

        return false;
    }

    private static bool HasDefaultValue(Type dtoType, string jsonName)
    {
        // For positional records, check if the corresponding constructor parameter has a default value
        var ctors = dtoType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        foreach (var ctor in ctors)
        {
            foreach (var param in ctor.GetParameters())
            {
                var paramJsonAttr = param.GetCustomAttribute<JsonPropertyNameAttribute>();
                if (paramJsonAttr is null)
                {
                    // Check the corresponding property for the attribute
                    var prop = dtoType.GetProperty(param.Name!, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    paramJsonAttr = prop?.GetCustomAttribute<JsonPropertyNameAttribute>();
                }

                if (paramJsonAttr?.Name == jsonName)
                {
                    if (param.HasDefaultValue)
                    {
                        return true;
                    }

                    // Check nullability via NullabilityInfoContext for reference types
                    if (!param.ParameterType.IsValueType)
                    {
                        var nullabilityContext = new NullabilityInfoContext();
                        var nullabilityInfo = nullabilityContext.Create(param);
                        if (nullabilityInfo.WriteState == NullabilityState.Nullable ||
                            nullabilityInfo.ReadState == NullabilityState.Nullable)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        // For class-style records, check property nullability
        var property = FindPropertyByJsonName(dtoType, jsonName);
        if (property is not null && !property.PropertyType.IsValueType)
        {
            var nullabilityContext = new NullabilityInfoContext();
            var nullabilityInfo = nullabilityContext.Create(property);
            if (nullabilityInfo.WriteState == NullabilityState.Nullable ||
                nullabilityInfo.ReadState == NullabilityState.Nullable)
            {
                return true;
            }
        }

        return false;
    }

    private static PropertyInfo? FindPropertyByJsonName(Type dtoType, string jsonName)
    {
        foreach (var prop in dtoType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
            if (attr?.Name == jsonName)
            {
                return prop;
            }
        }

        return null;
    }

    private static Type? GetNestedDtoType(Type type)
    {
        // Unwrap Nullable<T>
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        // Skip primitives, string, JsonElement, collections of primitives
        if (underlying.IsPrimitive || underlying == typeof(string) ||
            underlying == typeof(decimal) || underlying == typeof(DateTime) ||
            underlying == typeof(DateTimeOffset) ||
            underlying.Namespace?.StartsWith("System.Text.Json", StringComparison.Ordinal) == true)
        {
            return null;
        }

        // Skip generic collections (List<T>, Dictionary<K,V>) -- we don't recurse into list element types at the field map level
        if (underlying.IsGenericType)
        {
            var genDef = underlying.GetGenericTypeDefinition();
            if (genDef == typeof(List<>) || genDef == typeof(Dictionary<,>))
            {
                return null;
            }
        }

        // Check if the type has any JsonPropertyName attributes (i.e., is a DTO)
        var hasJsonProps = underlying.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(p => p.GetCustomAttribute<JsonPropertyNameAttribute>() is not null);

        return hasJsonProps ? underlying : null;
    }
}

/// <summary>
/// Metadata about a DTO type's JSON field expectations.
/// </summary>
internal sealed class DtoFieldInfo
{
    private readonly Dictionary<string, bool> _fields; // jsonName -> isOptional

    /// <summary>All expected JSON field names for this DTO.</summary>
    public IReadOnlyList<string> FieldNames { get; }

    /// <summary>Whether the DTO has a <see cref="JsonExtensionDataAttribute"/> property.</summary>
    public bool HasExtensionData { get; }

    /// <summary>Nested DTO field maps keyed by the parent field's JSON name.</summary>
    public IReadOnlyDictionary<string, DtoFieldInfo> NestedMaps { get; }

    /// <summary>
    /// Creates a new <see cref="DtoFieldInfo"/>.
    /// </summary>
    public DtoFieldInfo(
        Dictionary<string, bool> fields,
        bool hasExtensionData,
        Dictionary<string, DtoFieldInfo> nestedMaps)
    {
        _fields = fields;
        FieldNames = fields.Keys.ToList().AsReadOnly();
        HasExtensionData = hasExtensionData;
        NestedMaps = nestedMaps;
    }

    /// <summary>
    /// Returns true if the field is nullable, has a default value, or is otherwise optional.
    /// </summary>
    public bool IsOptional(string jsonFieldName) =>
        _fields.TryGetValue(jsonFieldName, out var isOptional) && isOptional;
}
