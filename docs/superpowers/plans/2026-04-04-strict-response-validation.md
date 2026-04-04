# Strict JSON Response Validation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add optional strict validation of JSON API responses against DTO schemas, throwing on field mismatches in strict mode and logging warnings in non-strict mode.

**Architecture:** A `ResponseSchemaValidationHandler` (DelegatingHandler) intercepts 2xx responses, reads the JSON body, and compares field names against DTO schemas extracted via reflection from Refit interfaces. Two static utility classes (`DtoFieldMap` and `RefitEndpointMap`) handle the reflection logic. The handler is wired into the consumer HTTP pipeline between `TokenRefreshHandler` and `ErrorNormalizationHandler`.

**Tech Stack:** xUnit v3, Shouldly, System.Text.Json, Refit attributes, `ILogger<T>` with `LoggerMessage` source generation.

---

## File Structure

| File | Type | Task |
|---|---|---|
| `src/IbkrConduit/Session/IbkrClientOptions.cs` | Modified | T.1 |
| `src/IbkrConduit/Errors/IbkrSchemaViolationException.cs` | New | T.1 |
| `tests/IbkrConduit.Tests.Unit/Errors/IbkrSchemaViolationExceptionTests.cs` | New | T.1 |
| `src/IbkrConduit/Http/DtoFieldMap.cs` | New | T.2 |
| `tests/IbkrConduit.Tests.Unit/Http/DtoFieldMapTests.cs` | New | T.2 |
| `src/IbkrConduit/Http/RefitEndpointMap.cs` | New | T.3 |
| `tests/IbkrConduit.Tests.Unit/Http/RefitEndpointMapTests.cs` | New | T.3 |
| `src/IbkrConduit/Http/ResponseSchemaValidationHandler.cs` | New | T.4 |
| `tests/IbkrConduit.Tests.Unit/Http/ResponseSchemaValidationHandlerTests.cs` | New | T.4 |
| `src/IbkrConduit/Http/ConsumerPipelineRegistration.cs` | Modified | T.5 |
| `src/IbkrConduit/Http/ServiceCollectionExtensions.cs` | Modified | T.5 |
| `tests/IbkrConduit.Tests.Integration/Pipeline/ResponseSchemaValidationTests.cs` | New | T.5 |

---

## Task T.1 -- Add StrictResponseValidation to IbkrClientOptions + IbkrSchemaViolationException

**Files:**
- Modify: `src/IbkrConduit/Session/IbkrClientOptions.cs`
- Create: `src/IbkrConduit/Errors/IbkrSchemaViolationException.cs`
- Create: `tests/IbkrConduit.Tests.Unit/Errors/IbkrSchemaViolationExceptionTests.cs`

### Steps

- [ ] **Step 1: Write the failing test for IbkrSchemaViolationException**

Create `tests/IbkrConduit.Tests.Unit/Errors/IbkrSchemaViolationExceptionTests.cs`:

```csharp
using System.Net;
using IbkrConduit.Errors;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Errors;

public class IbkrSchemaViolationExceptionTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var extra = new List<string> { "newField1", "newField2" };
        var missing = new List<string> { "deprecatedField" };

        var ex = new IbkrSchemaViolationException(
            "/v1/api/portfolio/U1234567/summary",
            typeof(string),
            extra,
            missing);

        ex.EndpointPath.ShouldBe("/v1/api/portfolio/U1234567/summary");
        ex.DtoType.ShouldBe(typeof(string));
        ex.ExtraFields.ShouldBe(extra);
        ex.MissingFields.ShouldBe(missing);
        ex.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public void Constructor_FormatsMessageWithEndpointAndDtoType()
    {
        var ex = new IbkrSchemaViolationException(
            "/v1/api/test",
            typeof(int),
            ["extra1"],
            ["missing1"]);

        ex.Message.ShouldContain("/v1/api/test");
        ex.Message.ShouldContain("Int32");
    }

    [Fact]
    public void Constructor_EmptyLists_StillSetsProperties()
    {
        var ex = new IbkrSchemaViolationException(
            "/v1/api/test",
            typeof(string),
            [],
            []);

        ex.ExtraFields.ShouldBeEmpty();
        ex.MissingFields.ShouldBeEmpty();
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --configuration Release --filter "FullyQualifiedName~IbkrSchemaViolationExceptionTests"`

Expected: Build failure -- `IbkrSchemaViolationException` does not exist.

- [ ] **Step 3: Implement IbkrSchemaViolationException**

Create `src/IbkrConduit/Errors/IbkrSchemaViolationException.cs`:

```csharp
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
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --configuration Release --filter "FullyQualifiedName~IbkrSchemaViolationExceptionTests"`

Expected: All 3 tests pass.

- [ ] **Step 5: Add StrictResponseValidation to IbkrClientOptions**

In `src/IbkrConduit/Session/IbkrClientOptions.cs`, add after the `ProactiveRefreshMargin` property:

```csharp
    /// <summary>
    /// When true, throws <see cref="Errors.IbkrSchemaViolationException"/> if a JSON response
    /// contains fields not mapped to the DTO, or if DTO fields are missing from the response.
    /// Default is false (log warnings only). Enable in dev/test environments for fail-fast behavior.
    /// </summary>
    public bool StrictResponseValidation { get; set; }
```

- [ ] **Step 6: Build to verify zero warnings**

Run: `dotnet build --configuration Release`

Expected: Build succeeded, 0 warnings.

- [ ] **Step 7: Run full test suite**

Run: `dotnet test --configuration Release`

Expected: All tests pass (no regressions).

- [ ] **Step 8: Commit**

```
git add src/IbkrConduit/Errors/IbkrSchemaViolationException.cs src/IbkrConduit/Session/IbkrClientOptions.cs tests/IbkrConduit.Tests.Unit/Errors/IbkrSchemaViolationExceptionTests.cs
git commit -m "feat: add IbkrSchemaViolationException and StrictResponseValidation option"
```

---

## Task T.2 -- Create DtoFieldMap

**Files:**
- Create: `src/IbkrConduit/Http/DtoFieldMap.cs`
- Create: `tests/IbkrConduit.Tests.Unit/Http/DtoFieldMapTests.cs`

### Context

`DtoFieldMap` is a static utility that takes a `Type` and extracts the set of expected JSON field names by reading `[JsonPropertyName]` attributes from constructor parameters and properties. It also detects:
- Whether the DTO has `[JsonExtensionData]` (extra fields are expected)
- Whether each field is nullable (nullable reference type or `Nullable<T>` or has a default value)
- Nested DTO types (properties whose type is another class/record with `[JsonPropertyName]` attributes)

The DTO patterns in this codebase fall into two categories:
1. **Positional records** (constructor parameters with `[property: JsonPropertyName(...)]`): e.g., `Position`, `AccountSummaryEntry`, `ServerInfo`
2. **Class-style records** (init properties with `[JsonPropertyName(...)]`): e.g., `Account`

Both need to be handled.

### Steps

- [ ] **Step 1: Write test file with all test cases**

Create `tests/IbkrConduit.Tests.Unit/Http/DtoFieldMapTests.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrConduit.Http;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

public class DtoFieldMapTests
{
    // --- Test DTOs ---

    [ExcludeFromCodeCoverage]
    public record SimpleDto(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("age")] int Age);

    [ExcludeFromCodeCoverage]
    public record DtoWithNullables(
        [property: JsonPropertyName("required_field")] string RequiredField,
        [property: JsonPropertyName("nullable_ref")] string? NullableRef,
        [property: JsonPropertyName("nullable_value")] int? NullableValue,
        [property: JsonPropertyName("with_default")] string? WithDefault = null);

    [ExcludeFromCodeCoverage]
    public record DtoWithExtensionData(
        [property: JsonPropertyName("id")] string Id)
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; init; }
    }

    [ExcludeFromCodeCoverage]
    public record NestedChild(
        [property: JsonPropertyName("childField")] string ChildField);

    [ExcludeFromCodeCoverage]
    public record DtoWithNestedType(
        [property: JsonPropertyName("topField")] string TopField,
        [property: JsonPropertyName("child")] NestedChild? Child);

    [ExcludeFromCodeCoverage]
    public sealed record ClassStyleDto
    {
        [JsonPropertyName("prop_a")]
        public string PropA { get; init; } = string.Empty;

        [JsonPropertyName("prop_b")]
        public int? PropB { get; init; }
    }

    // --- Tests ---

    [Fact]
    public void Extract_SimpleRecord_ReturnsAllFieldNames()
    {
        var result = DtoFieldMap.Extract(typeof(SimpleDto));

        result.FieldNames.ShouldBe(new[] { "name", "age" }, ignoreOrder: true);
    }

    [Fact]
    public void Extract_SimpleRecord_HasNoExtensionData()
    {
        var result = DtoFieldMap.Extract(typeof(SimpleDto));

        result.HasExtensionData.ShouldBeFalse();
    }

    [Fact]
    public void Extract_NullableFields_MarkedAsOptional()
    {
        var result = DtoFieldMap.Extract(typeof(DtoWithNullables));

        result.IsOptional("required_field").ShouldBeFalse();
        result.IsOptional("nullable_ref").ShouldBeTrue();
        result.IsOptional("nullable_value").ShouldBeTrue();
        result.IsOptional("with_default").ShouldBeTrue();
    }

    [Fact]
    public void Extract_WithExtensionData_DetectedCorrectly()
    {
        var result = DtoFieldMap.Extract(typeof(DtoWithExtensionData));

        result.HasExtensionData.ShouldBeTrue();
        result.FieldNames.ShouldBe(new[] { "id" });
    }

    [Fact]
    public void Extract_ExtensionDataProperty_NotIncludedInFieldNames()
    {
        var result = DtoFieldMap.Extract(typeof(DtoWithExtensionData));

        result.FieldNames.ShouldNotContain("AdditionalData");
    }

    [Fact]
    public void Extract_NestedType_IncludesNestedFieldsWithDotNotation()
    {
        var result = DtoFieldMap.Extract(typeof(DtoWithNestedType));

        result.FieldNames.ShouldContain("topField");
        result.FieldNames.ShouldContain("child");
        result.NestedMaps.ShouldContainKey("child");
        result.NestedMaps["child"].FieldNames.ShouldContain("childField");
    }

    [Fact]
    public void Extract_ClassStyleRecord_ReturnsPropertyNames()
    {
        var result = DtoFieldMap.Extract(typeof(ClassStyleDto));

        result.FieldNames.ShouldBe(new[] { "prop_a", "prop_b" }, ignoreOrder: true);
        result.IsOptional("prop_b").ShouldBeTrue();
    }

    [Fact]
    public void Extract_CachesResults_SameInstanceReturned()
    {
        var result1 = DtoFieldMap.Extract(typeof(SimpleDto));
        var result2 = DtoFieldMap.Extract(typeof(SimpleDto));

        ReferenceEquals(result1, result2).ShouldBeTrue();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --configuration Release --filter "FullyQualifiedName~DtoFieldMapTests"`

Expected: Build failure -- `DtoFieldMap` does not exist.

- [ ] **Step 3: Implement DtoFieldMap**

Create `src/IbkrConduit/Http/DtoFieldMap.cs`:

```csharp
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;

namespace IbkrConduit.Http;

/// <summary>
/// Extracts JSON field metadata from a DTO type by reading <see cref="JsonPropertyNameAttribute"/>
/// attributes from constructor parameters and properties. Used by
/// <see cref="ResponseSchemaValidationHandler"/> to validate response schemas.
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
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --configuration Release --filter "FullyQualifiedName~DtoFieldMapTests"`

Expected: All 8 tests pass.

- [ ] **Step 5: Build to verify zero warnings**

Run: `dotnet build --configuration Release`

Expected: Build succeeded, 0 warnings.

- [ ] **Step 6: Commit**

```
git add src/IbkrConduit/Http/DtoFieldMap.cs tests/IbkrConduit.Tests.Unit/Http/DtoFieldMapTests.cs
git commit -m "feat: add DtoFieldMap for extracting JSON field metadata from DTO types"
```

---

## Task T.3 -- Create RefitEndpointMap

**Files:**
- Create: `src/IbkrConduit/Http/RefitEndpointMap.cs`
- Create: `tests/IbkrConduit.Tests.Unit/Http/RefitEndpointMapTests.cs`

### Context

`RefitEndpointMap` scans Refit interface types and builds a lookup from `(HttpMethod, request path)` to the response DTO `Type`. It:

- Reads Refit HTTP attributes (`[Get("/path")]`, `[Post("/path")]`, `[Delete("/path")]`, `[Put("/path")]`) to get method + path template
- Unwraps return types: `Task<T>` -> `T`, `List<T>` -> validate `T`, `Dictionary<string, T>` -> validate `T`
- Skips `Task` (void returns), `IApiResponse<string>` (raw string), `OneOf<T1,T2>` (handled by OrderOperations)
- Converts path templates (`{param}`) to regex patterns for runtime matching

The 9 consumer Refit interfaces are:
- `IIbkrPortfolioApi`, `IIbkrContractApi`, `IIbkrOrderApi`, `IIbkrMarketDataApi`
- `IIbkrAccountApi`, `IIbkrAlertApi`, `IIbkrWatchlistApi`, `IIbkrFyiApi`, `IIbkrAllocationApi`

### Steps

- [ ] **Step 1: Write test file with all test cases**

Create `tests/IbkrConduit.Tests.Unit/Http/RefitEndpointMapTests.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using IbkrConduit.Http;
using Refit;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

public class RefitEndpointMapTests
{
    // --- Test interfaces and DTOs ---

    [ExcludeFromCodeCoverage]
    public record TestDto([property: JsonPropertyName("id")] string Id);

    [ExcludeFromCodeCoverage]
    public record TestItem([property: JsonPropertyName("name")] string Name);

    [ExcludeFromCodeCoverage]
    public record TestValue([property: JsonPropertyName("amount")] decimal Amount);

    public interface ITestApi
    {
        [Get("/v1/api/test/{id}")]
        Task<TestDto> GetByIdAsync(string id, CancellationToken cancellationToken = default);

        [Get("/v1/api/test/items")]
        Task<List<TestItem>> GetItemsAsync(CancellationToken cancellationToken = default);

        [Get("/v1/api/test/{accountId}/values")]
        Task<Dictionary<string, TestValue>> GetValuesAsync(
            string accountId, CancellationToken cancellationToken = default);

        [Post("/v1/api/test/action")]
        Task DoActionAsync(CancellationToken cancellationToken = default);

        [Delete("/v1/api/test/{id}")]
        Task<TestDto> DeleteAsync(string id, CancellationToken cancellationToken = default);
    }

    public interface ITestApiWithRawResponse
    {
        [Post("/v1/api/test/raw")]
        Task<IApiResponse<string>> GetRawAsync(CancellationToken cancellationToken = default);
    }

    // --- Tests ---

    [Fact]
    public void Build_SingleInterface_MapsSimpleEndpoint()
    {
        var map = RefitEndpointMap.Build([typeof(ITestApi)]);

        var result = map.TryGetDtoType(HttpMethod.Get, "/v1/api/test/abc123");

        result.ShouldNotBeNull();
        result.DtoType.ShouldBe(typeof(TestDto));
        result.IsCollection.ShouldBeFalse();
        result.IsDictionary.ShouldBeFalse();
    }

    [Fact]
    public void Build_ListReturnType_UnwrapsToElementType()
    {
        var map = RefitEndpointMap.Build([typeof(ITestApi)]);

        var result = map.TryGetDtoType(HttpMethod.Get, "/v1/api/test/items");

        result.ShouldNotBeNull();
        result.DtoType.ShouldBe(typeof(TestItem));
        result.IsCollection.ShouldBeTrue();
    }

    [Fact]
    public void Build_DictionaryReturnType_UnwrapsToValueType()
    {
        var map = RefitEndpointMap.Build([typeof(ITestApi)]);

        var result = map.TryGetDtoType(HttpMethod.Get, "/v1/api/test/U1234567/values");

        result.ShouldNotBeNull();
        result.DtoType.ShouldBe(typeof(TestValue));
        result.IsDictionary.ShouldBeTrue();
    }

    [Fact]
    public void Build_VoidReturn_SkippedInMap()
    {
        var map = RefitEndpointMap.Build([typeof(ITestApi)]);

        var result = map.TryGetDtoType(HttpMethod.Post, "/v1/api/test/action");

        result.ShouldBeNull();
    }

    [Fact]
    public void Build_DeleteMethod_MapsCorrectly()
    {
        var map = RefitEndpointMap.Build([typeof(ITestApi)]);

        var result = map.TryGetDtoType(HttpMethod.Delete, "/v1/api/test/xyz789");

        result.ShouldNotBeNull();
        result.DtoType.ShouldBe(typeof(TestDto));
    }

    [Fact]
    public void Build_IApiResponseString_SkippedInMap()
    {
        var map = RefitEndpointMap.Build([typeof(ITestApiWithRawResponse)]);

        var result = map.TryGetDtoType(HttpMethod.Post, "/v1/api/test/raw");

        result.ShouldBeNull();
    }

    [Fact]
    public void Build_UnknownPath_ReturnsNull()
    {
        var map = RefitEndpointMap.Build([typeof(ITestApi)]);

        var result = map.TryGetDtoType(HttpMethod.Get, "/v1/api/unknown/path");

        result.ShouldBeNull();
    }

    [Fact]
    public void Build_WrongHttpMethod_ReturnsNull()
    {
        var map = RefitEndpointMap.Build([typeof(ITestApi)]);

        // /v1/api/test/{id} is registered for GET, not POST
        var result = map.TryGetDtoType(HttpMethod.Post, "/v1/api/test/abc123");

        result.ShouldBeNull();
    }

    [Fact]
    public void Build_PathWithMultipleParams_MatchesCorrectly()
    {
        var map = RefitEndpointMap.Build([typeof(ITestApi)]);

        var result = map.TryGetDtoType(HttpMethod.Get, "/v1/api/test/U1234567/values");

        result.ShouldNotBeNull();
        result.DtoType.ShouldBe(typeof(TestValue));
    }

    [Fact]
    public void Build_RealPortfolioInterface_MapsPositionsEndpoint()
    {
        var map = RefitEndpointMap.Build([typeof(IbkrConduit.Portfolio.IIbkrPortfolioApi)]);

        var result = map.TryGetDtoType(HttpMethod.Get, "/v1/api/portfolio/U1234567/positions/0");

        result.ShouldNotBeNull();
        result.DtoType.ShouldBe(typeof(IbkrConduit.Portfolio.Position));
        result.IsCollection.ShouldBeTrue();
    }

    [Fact]
    public void Build_RealPortfolioInterface_MapsSummaryEndpoint()
    {
        var map = RefitEndpointMap.Build([typeof(IbkrConduit.Portfolio.IIbkrPortfolioApi)]);

        var result = map.TryGetDtoType(
            HttpMethod.Get, "/v1/api/portfolio/U1234567/summary");

        result.ShouldNotBeNull();
        result.DtoType.ShouldBe(typeof(IbkrConduit.Portfolio.AccountSummaryEntry));
        result.IsDictionary.ShouldBeTrue();
    }

    [Fact]
    public void Build_MultipleInterfaces_AllMapped()
    {
        var map = RefitEndpointMap.Build([
            typeof(IbkrConduit.Portfolio.IIbkrPortfolioApi),
            typeof(IbkrConduit.Accounts.IIbkrAccountApi),
        ]);

        map.TryGetDtoType(HttpMethod.Get, "/v1/api/portfolio/accounts").ShouldNotBeNull();
        map.TryGetDtoType(HttpMethod.Get, "/v1/api/iserver/accounts").ShouldNotBeNull();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --configuration Release --filter "FullyQualifiedName~RefitEndpointMapTests"`

Expected: Build failure -- `RefitEndpointMap` does not exist.

- [ ] **Step 3: Implement RefitEndpointMap**

Create `src/IbkrConduit/Http/RefitEndpointMap.cs`:

```csharp
using System.Reflection;
using System.Text.RegularExpressions;
using Refit;

namespace IbkrConduit.Http;

/// <summary>
/// Maps Refit interface endpoints to their response DTO types using reflection.
/// Built once at startup and used by <see cref="ResponseSchemaValidationHandler"/>
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

                var pathRegex = ConvertPathTemplateToRegex(pathTemplate);
                entries.Add(new EndpointEntry(httpMethod, pathRegex, dtoInfo));
            }
        }

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
        // Escape regex special chars, then replace {param} with [^/]+
        var escaped = Regex.Escape(pathTemplate);
        var pattern = Regex.Replace(escaped, @"\\\{[^}]+\\\}", "[^/]+");
        return new Regex($"^{pattern}$", RegexOptions.Compiled);
    }

    private sealed record EndpointEntry(
        HttpMethod HttpMethod,
        Regex PathRegex,
        EndpointDtoInfo DtoInfo);
}

/// <summary>
/// Information about the DTO type expected for an endpoint response.
/// </summary>
internal sealed record EndpointDtoInfo(
    Type DtoType,
    bool IsCollection,
    bool IsDictionary);
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --configuration Release --filter "FullyQualifiedName~RefitEndpointMapTests"`

Expected: All 12 tests pass.

- [ ] **Step 5: Build to verify zero warnings**

Run: `dotnet build --configuration Release`

Expected: Build succeeded, 0 warnings.

- [ ] **Step 6: Commit**

```
git add src/IbkrConduit/Http/RefitEndpointMap.cs tests/IbkrConduit.Tests.Unit/Http/RefitEndpointMapTests.cs
git commit -m "feat: add RefitEndpointMap for mapping Refit endpoints to DTO types"
```

---

## Task T.4 -- Create ResponseSchemaValidationHandler

**Files:**
- Create: `src/IbkrConduit/Http/ResponseSchemaValidationHandler.cs`
- Create: `tests/IbkrConduit.Tests.Unit/Http/ResponseSchemaValidationHandlerTests.cs`

### Context

The handler sits in the consumer HTTP pipeline. It:
1. Calls `base.SendAsync()` to get the response
2. Skips validation for non-2xx responses
3. Reads response body as string
4. Looks up the expected DTO type from `RefitEndpointMap`
5. Parses JSON field names from the response
6. Compares against expected fields from `DtoFieldMap`
7. If mismatches found: strict mode throws `IbkrSchemaViolationException`, non-strict logs warning
8. Re-buffers the body for downstream consumption

The `ErrorNormalizationHandler` in `src/IbkrConduit/Http/ErrorNormalizationHandler.cs` shows the exact pattern for reading + re-buffering response body. Follow the same approach:
- Read body via `originalContent.ReadAsStringAsync(cancellationToken)`
- After inspection, replace `response.Content` with `new StringContent(body, Encoding.UTF8)`
- Preserve `ContentType` header

The test pattern from `ErrorNormalizationHandlerTests` uses a `StubInnerHandler` that returns a canned response. Use the same pattern.

### Steps

- [ ] **Step 1: Write test file with all test cases**

Create `tests/IbkrConduit.Tests.Unit/Http/ResponseSchemaValidationHandlerTests.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrConduit.Errors;
using IbkrConduit.Http;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

public class ResponseSchemaValidationHandlerTests
{
    // --- Test DTOs ---

    [ExcludeFromCodeCoverage]
    public record TestDto(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name);

    [ExcludeFromCodeCoverage]
    public record TestDtoWithOptional(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("optional_field")] string? OptionalField);

    [ExcludeFromCodeCoverage]
    public record TestDtoWithExtension(
        [property: JsonPropertyName("id")] string Id)
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; init; }
    }

    // --- Test Refit interface ---

    public interface ITestValidationApi
    {
        [Refit.Get("/v1/api/test/item")]
        Task<TestDto> GetItemAsync(CancellationToken cancellationToken = default);

        [Refit.Get("/v1/api/test/optional")]
        Task<TestDtoWithOptional> GetOptionalAsync(CancellationToken cancellationToken = default);

        [Refit.Get("/v1/api/test/extension")]
        Task<TestDtoWithExtension> GetExtensionAsync(CancellationToken cancellationToken = default);

        [Refit.Get("/v1/api/test/items")]
        Task<List<TestDto>> GetItemsAsync(CancellationToken cancellationToken = default);
    }

    // --- Helpers ---

    private static RefitEndpointMap BuildMap() =>
        RefitEndpointMap.Build([typeof(ITestValidationApi)]);

    private static ResponseSchemaValidationHandler CreateHandler(
        bool strict, RefitEndpointMap map, HttpResponseMessage response)
    {
        var options = new IbkrClientOptions { StrictResponseValidation = strict };
        var logger = NullLoggerFactory.Instance.CreateLogger<ResponseSchemaValidationHandler>();
        var handler = new ResponseSchemaValidationHandler(options, map, logger)
        {
            InnerHandler = new StubInnerHandler(response),
        };
        return handler;
    }

    private static HttpRequestMessage MakeRequest(HttpMethod method, string path) =>
        new(method, $"https://api.ibkr.com{path}");

    private static HttpResponseMessage MakeJsonResponse(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static Task<HttpResponseMessage> SendAsync(
        ResponseSchemaValidationHandler handler, HttpRequestMessage request) =>
        new HttpMessageInvoker(handler).SendAsync(request, TestContext.Current.CancellationToken);

    // --- Tests ---

    [Fact]
    public async Task StrictMode_ExtraField_ThrowsSchemaViolationException()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("""{"id":"1","name":"test","unexpected":"value"}""");
        var handler = CreateHandler(strict: true, map, response);

        var ex = await Should.ThrowAsync<IbkrSchemaViolationException>(
            SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/item")));

        ex.ExtraFields.ShouldContain("unexpected");
        ex.DtoType.ShouldBe(typeof(TestDto));
        ex.EndpointPath.ShouldBe("/v1/api/test/item");
    }

    [Fact]
    public async Task StrictMode_MissingRequiredField_ThrowsSchemaViolationException()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("""{"id":"1"}""");
        var handler = CreateHandler(strict: true, map, response);

        var ex = await Should.ThrowAsync<IbkrSchemaViolationException>(
            SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/item")));

        ex.MissingFields.ShouldContain("name");
    }

    [Fact]
    public async Task StrictMode_MissingOptionalField_DoesNotThrow()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("""{"id":"1"}""");
        var handler = CreateHandler(strict: true, map, response);

        var result = await SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/optional"));

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task StrictMode_ExtensionData_ExtraFieldsNotFlagged()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("""{"id":"1","extra_field":"value","another":"value2"}""");
        var handler = CreateHandler(strict: true, map, response);

        var result = await SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/extension"));

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NonStrictMode_ExtraField_DoesNotThrow()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("""{"id":"1","name":"test","unexpected":"value"}""");
        var handler = CreateHandler(strict: false, map, response);

        var result = await SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/item"));

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NonSuccessResponse_SkipsValidation()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("""{"error":"not found"}""", HttpStatusCode.NotFound);
        var handler = CreateHandler(strict: true, map, response);

        var result = await SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/item"));

        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnknownEndpoint_SkipsValidation()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("""{"random":"data"}""");
        var handler = CreateHandler(strict: true, map, response);

        var result = await SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/unknown/path"));

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task StrictMode_MatchingFields_PassesThrough()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("""{"id":"1","name":"test"}""");
        var handler = CreateHandler(strict: true, map, response);

        var result = await SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/item"));

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task BodyPreservedAfterValidation()
    {
        var map = BuildMap();
        var body = """{"id":"1","name":"test"}""";
        var response = MakeJsonResponse(body);
        var handler = CreateHandler(strict: true, map, response);

        var result = await SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/item"));

        var content = await result.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.ShouldBe(body);
    }

    [Fact]
    public async Task ContentTypePreservedAfterValidation()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("""{"id":"1","name":"test"}""");
        var handler = CreateHandler(strict: true, map, response);

        var result = await SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/item"));

        result.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
    }

    [Fact]
    public async Task ListResponse_ValidatesFirstElement()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("""[{"id":"1","name":"test","extra":"field"}]""");
        var handler = CreateHandler(strict: true, map, response);

        var ex = await Should.ThrowAsync<IbkrSchemaViolationException>(
            SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/items")));

        ex.ExtraFields.ShouldContain("extra");
    }

    [Fact]
    public async Task EmptyArrayResponse_SkipsValidation()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("[]");
        var handler = CreateHandler(strict: true, map, response);

        var result = await SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/items"));

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NonJsonResponse_SkipsValidation()
    {
        var map = BuildMap();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("plain text", Encoding.UTF8, "text/plain"),
        };
        var handler = CreateHandler(strict: true, map, response);

        var result = await SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/item"));

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EmptyBody_SkipsValidation()
    {
        var map = BuildMap();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("", Encoding.UTF8, "application/json"),
        };
        var handler = CreateHandler(strict: true, map, response);

        var result = await SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/item"));

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private class StubInnerHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --configuration Release --filter "FullyQualifiedName~ResponseSchemaValidationHandlerTests"`

Expected: Build failure -- `ResponseSchemaValidationHandler` does not exist.

- [ ] **Step 3: Implement ResponseSchemaValidationHandler**

Create `src/IbkrConduit/Http/ResponseSchemaValidationHandler.cs`:

```csharp
using System.Net.Http.Headers;
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

        // Extra fields: in response but not in DTO
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

        LogSchemaMismatch(path, dtoInfo.DtoType.Name, extraFields, missingFields);
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Response schema mismatch for {Path} -> {DtoTypeName}: Extra fields: [{ExtraFields}]. Missing fields: [{MissingFields}].")]
    private partial void LogSchemaMismatch(
        string path, string dtoTypeName,
        List<string> extraFields, List<string> missingFields);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --configuration Release --filter "FullyQualifiedName~ResponseSchemaValidationHandlerTests"`

Expected: All 14 tests pass.

- [ ] **Step 5: Build to verify zero warnings**

Run: `dotnet build --configuration Release`

Expected: Build succeeded, 0 warnings.

- [ ] **Step 6: Commit**

```
git add src/IbkrConduit/Http/ResponseSchemaValidationHandler.cs tests/IbkrConduit.Tests.Unit/Http/ResponseSchemaValidationHandlerTests.cs
git commit -m "feat: add ResponseSchemaValidationHandler for JSON response schema validation"
```

---

## Task T.5 -- Wire into ConsumerPipelineRegistration + Integration Tests

**Files:**
- Modify: `src/IbkrConduit/Http/ConsumerPipelineRegistration.cs`
- Modify: `src/IbkrConduit/Http/ServiceCollectionExtensions.cs`
- Create: `tests/IbkrConduit.Tests.Integration/Pipeline/ResponseSchemaValidationTests.cs`

### Context

The handler needs `IbkrClientOptions`, `RefitEndpointMap`, and `ILogger<ResponseSchemaValidationHandler>` injected. `IbkrClientOptions` is already registered as a singleton in `SessionServiceRegistration.Register`. The `RefitEndpointMap` must be built once and registered as a singleton. The handler is placed between `TokenRefreshHandler` and `ErrorNormalizationHandler` in the pipeline.

The 9 consumer Refit interfaces that need to be scanned are:
- `IIbkrPortfolioApi`, `IIbkrContractApi`, `IIbkrOrderApi`, `IIbkrMarketDataApi`
- `IIbkrAccountApi`, `IIbkrAlertApi`, `IIbkrWatchlistApi`, `IIbkrFyiApi`, `IIbkrAllocationApi`

### Steps

- [ ] **Step 1: Register RefitEndpointMap as singleton in ServiceCollectionExtensions**

In `src/IbkrConduit/Http/ServiceCollectionExtensions.cs`, add the `RefitEndpointMap` singleton registration. Add these using statements at the top:

```csharp
using IbkrConduit.Accounts;
using IbkrConduit.Alerts;
using IbkrConduit.Allocation;
using IbkrConduit.Contracts;
using IbkrConduit.Fyi;
using IbkrConduit.MarketData;
using IbkrConduit.Orders;
using IbkrConduit.Portfolio;
using IbkrConduit.Watchlists;
```

Then, inside `AddIbkrClient`, before the `ConsumerPipelineRegistration.Register(...)` call, add:

```csharp
        // Response schema validation map (built once, used by all consumer pipelines)
        var endpointMap = RefitEndpointMap.Build([
            typeof(IIbkrPortfolioApi),
            typeof(IIbkrContractApi),
            typeof(IIbkrOrderApi),
            typeof(IIbkrMarketDataApi),
            typeof(IIbkrAccountApi),
            typeof(IIbkrAlertApi),
            typeof(IIbkrWatchlistApi),
            typeof(IIbkrFyiApi),
            typeof(IIbkrAllocationApi),
        ]);
        services.AddSingleton(endpointMap);
```

Update the `ConsumerPipelineRegistration.Register` call to pass the endpoint map:

```csharp
        ConsumerPipelineRegistration.Register(services, credentials, clientOptions, endpointMap, baseUrl);
```

- [ ] **Step 2: Update ConsumerPipelineRegistration to accept and wire the handler**

In `src/IbkrConduit/Http/ConsumerPipelineRegistration.cs`:

Update the `Register` method signature to accept `IbkrClientOptions` and `RefitEndpointMap`:

```csharp
    public static void Register(
        IServiceCollection services,
        IbkrOAuthCredentials credentials,
        IbkrClientOptions clientOptions,
        RefitEndpointMap endpointMap,
        string baseUrl)
```

Update the `RegisterConsumerRefitClient` method signature:

```csharp
    private static void RegisterConsumerRefitClient<TApi>(
        IServiceCollection services,
        IbkrOAuthCredentials credentials,
        IbkrClientOptions clientOptions,
        RefitEndpointMap endpointMap,
        string baseUrl) where TApi : class
```

Pass the new parameters through all 9 registration calls:

```csharp
        RegisterConsumerRefitClient<IIbkrPortfolioApi>(services, credentials, clientOptions, endpointMap, baseUrl);
        RegisterConsumerRefitClient<IIbkrContractApi>(services, credentials, clientOptions, endpointMap, baseUrl);
        RegisterConsumerRefitClient<IIbkrOrderApi>(services, credentials, clientOptions, endpointMap, baseUrl);
        RegisterConsumerRefitClient<IIbkrMarketDataApi>(services, credentials, clientOptions, endpointMap, baseUrl);
        RegisterConsumerRefitClient<IIbkrAccountApi>(services, credentials, clientOptions, endpointMap, baseUrl);
        RegisterConsumerRefitClient<IIbkrAlertApi>(services, credentials, clientOptions, endpointMap, baseUrl);
        RegisterConsumerRefitClient<IIbkrWatchlistApi>(services, credentials, clientOptions, endpointMap, baseUrl);
        RegisterConsumerRefitClient<IIbkrFyiApi>(services, credentials, clientOptions, endpointMap, baseUrl);
        RegisterConsumerRefitClient<IIbkrAllocationApi>(services, credentials, clientOptions, endpointMap, baseUrl);
```

Add the `ResponseSchemaValidationHandler` in the pipeline, between `TokenRefreshHandler` and `ErrorNormalizationHandler`. Add a `using Microsoft.Extensions.Logging;` import at the top. The pipeline in `RegisterConsumerRefitClient` becomes:

```csharp
        services.AddRefitClient<TApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            })
            .AddHttpMessageHandler(sp =>
                new TokenRefreshHandler(
                    sp.GetRequiredService<ISessionManager>()))
            .AddHttpMessageHandler(sp =>
                new ResponseSchemaValidationHandler(
                    clientOptions,
                    endpointMap,
                    sp.GetRequiredService<ILogger<ResponseSchemaValidationHandler>>()))
            .AddHttpMessageHandler(_ =>
                new ErrorNormalizationHandler())
            .AddHttpMessageHandler(sp =>
                new ResilienceHandler(
                    sp.GetRequiredService<ResiliencePipeline<HttpResponseMessage>>()))
            .AddHttpMessageHandler(sp =>
                new GlobalRateLimitingHandler(
                    sp.GetRequiredService<RateLimiter>()))
            .AddHttpMessageHandler(sp =>
                new EndpointRateLimitingHandler(
                    sp.GetRequiredService<IReadOnlyDictionary<string, RateLimiter>>()))
            .AddHttpMessageHandler(sp =>
                new OAuthSigningHandler(
                    sp.GetRequiredService<ISessionTokenProvider>(),
                    credentials.ConsumerKey,
                    credentials.AccessToken,
                    sp.GetRequiredService<ISessionManager>()));
```

- [ ] **Step 3: Build to verify zero warnings**

Run: `dotnet build --configuration Release`

Expected: Build succeeded, 0 warnings.

- [ ] **Step 4: Run full test suite to verify no regressions**

Run: `dotnet test --configuration Release`

Expected: All existing tests pass.

- [ ] **Step 5: Commit the wiring changes**

```
git add src/IbkrConduit/Http/ConsumerPipelineRegistration.cs src/IbkrConduit/Http/ServiceCollectionExtensions.cs
git commit -m "feat: wire ResponseSchemaValidationHandler into consumer pipeline"
```

- [ ] **Step 6: Write integration test for strict mode**

Create `tests/IbkrConduit.Tests.Integration/Pipeline/ResponseSchemaValidationTests.cs`:

```csharp
using System;
using System.Threading.Tasks;
using IbkrConduit.Errors;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Pipeline;

/// <summary>
/// Integration tests for <see cref="IbkrConduit.Http.ResponseSchemaValidationHandler"/>
/// exercised through the full DI pipeline.
/// </summary>
public class ResponseSchemaValidationTests : IAsyncDisposable
{
    private TestHarness? _harness;

    [Fact]
    public async Task StrictMode_ExtraFieldOnAccountsEndpoint_ThrowsSchemaViolationException()
    {
        _harness = await TestHarness.CreateAsync(opts =>
        {
            opts.StrictResponseValidation = true;
        });

        // Stub accounts returning a response with an extra field "newField" not on IserverAccountsResponse
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"accounts":["U1234567"],"selectedAccount":"U1234567","unexpectedNewField":"surprise"}"""));

        var ex = await Should.ThrowAsync<IbkrSchemaViolationException>(
            _harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken));

        ex.ExtraFields.ShouldContain("unexpectedNewField");
    }

    [Fact]
    public async Task NonStrictMode_ExtraField_DoesNotThrow()
    {
        _harness = await TestHarness.CreateAsync(opts =>
        {
            opts.StrictResponseValidation = false;
        });

        // Same extra field, but non-strict mode -- should not throw
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"accounts":["U1234567"],"selectedAccount":"U1234567","unexpectedNewField":"surprise"}"""));

        // Should NOT throw -- non-strict mode logs a warning but continues
        var result = await _harness.Client.Accounts.GetAccountsAsync(
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Accounts.ShouldContain("U1234567");
    }

    [Fact]
    public async Task StrictMode_ExtensionDataDto_ExtraFieldsAllowed()
    {
        _harness = await TestHarness.CreateAsync(opts =>
        {
            opts.StrictResponseValidation = true;
        });

        // IserverAccountsResponse has [JsonExtensionData], so extra fields should be allowed
        // But wait -- the previous test showed it doesn't. Let's use a DTO that has extension data.
        // SwitchAccountResponse has [JsonExtensionData], endpoint: POST /v1/api/iserver/account
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"success":"Account switched","extraField":"should be ok"}"""));

        // SwitchAccountResponse has [JsonExtensionData], so extra fields should NOT be flagged
        var result = await _harness.Client.Accounts.SwitchAccountAsync(
            "U1234567", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Success.ShouldBe("Account switched");
    }

    [Fact]
    public async Task StrictMode_MatchingFields_PassesThrough()
    {
        _harness = await TestHarness.CreateAsync(opts =>
        {
            opts.StrictResponseValidation = true;
        });

        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/accounts",
            """{"accounts":["U1234567"],"selectedAccount":"U1234567"}""");

        var result = await _harness.Client.Accounts.GetAccountsAsync(
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Accounts.ShouldContain("U1234567");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_harness is not null)
        {
            await _harness.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
```

- [ ] **Step 7: Run integration tests**

Run: `dotnet test tests/IbkrConduit.Tests.Integration --configuration Release --filter "FullyQualifiedName~ResponseSchemaValidationTests"`

Expected: All 4 integration tests pass.

**Troubleshooting:** If the `StrictMode_ExtraFieldOnAccountsEndpoint_ThrowsSchemaViolationException` test does not throw because `IserverAccountsResponse` has `[JsonExtensionData]`, update the test to use a different endpoint whose DTO does NOT have `[JsonExtensionData]`. Check the DTO for the targeted endpoint. A good candidate is any DTO without `[JsonExtensionData]` -- but note that most DTOs in this codebase DO have it. If all consumer DTOs have `[JsonExtensionData]`, then the integration test should verify that extra fields are silently accepted (no throw), and instead test a _missing required field_ scenario to exercise the strict mode throw path. Adjust the test accordingly.

- [ ] **Step 8: Run the full test suite**

Run: `dotnet test --configuration Release`

Expected: All tests pass.

- [ ] **Step 9: Run lint check**

Run: `dotnet format --verify-no-changes`

Expected: No formatting violations.

- [ ] **Step 10: Commit**

```
git add tests/IbkrConduit.Tests.Integration/Pipeline/ResponseSchemaValidationTests.cs
git commit -m "test: add integration tests for response schema validation handler"
```

---

## Notes for Implementer

### IserverAccountsResponse has [JsonExtensionData]

Most consumer DTOs in this codebase have `[JsonExtensionData]`. This means the "extra field throws" scenario only applies to DTOs WITHOUT that attribute. Review the actual DTO definitions when writing integration tests. If all DTOs have extension data, the primary strict-mode value comes from detecting _missing required fields_.

### LoggerMessage source generation

The `[LoggerMessage]` attribute on the partial method in `ResponseSchemaValidationHandler` requires the class to be `partial` and the containing file must use the correct `using Microsoft.Extensions.Logging;` import. If the `List<string>` parameters cause issues with source generation (LoggerMessage prefers simple types), change the signature to accept `string` parameters and join the lists before calling:

```csharp
LogSchemaMismatch(path, dtoInfo.DtoType.Name,
    string.Join(", ", extraFields), string.Join(", ", missingFields));
```

And update the attribute:

```csharp
[LoggerMessage(
    Level = LogLevel.Warning,
    Message = "Response schema mismatch for {Path} -> {DtoTypeName}: Extra fields: [{ExtraFields}]. Missing fields: [{MissingFields}].")]
private partial void LogSchemaMismatch(
    string path, string dtoTypeName,
    string extraFields, string missingFields);
```

### Pipeline ordering matters

The `ResponseSchemaValidationHandler` MUST be between `TokenRefreshHandler` and `ErrorNormalizationHandler`. If placed after `ErrorNormalizationHandler`, error responses that are remapped or thrown will never reach validation. If placed before `TokenRefreshHandler`, 401 retry logic won't be applied before validation.

### NullabilityInfoContext

`NullabilityInfoContext` (used in `DtoFieldMap`) is available in .NET 6+. This project targets .NET 10, so it's safe. However, `NullabilityInfoContext` is not thread-safe -- a new instance is created per call in `HasDefaultValue`. Since `DtoFieldMap.Extract` results are cached, this is only called once per type and has no performance concern.
