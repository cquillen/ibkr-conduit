# Strict JSON Response Validation — Design Spec

## Goal

Add optional strict validation of JSON API responses against DTO schemas. In strict mode (dev/test), throw an exception on any field mismatch — extra fields in the response not mapped to the DTO, or DTO fields missing from the response. In non-strict mode (production default), log a warning and continue.

## Background

The IBKR API is underdocumented and evolves without notice. DTOs use `[JsonExtensionData]` to capture unknown fields without failing deserialization. This is correct for production resilience, but during development and testing, schema mismatches should be caught immediately so DTOs stay in sync with the actual API.

## Design

### Configuration

New property on `IbkrClientOptions`:

```csharp
/// <summary>
/// When true, throws <see cref="IbkrSchemaViolationException"/> if a JSON response
/// contains fields not mapped to the DTO, or if DTO fields are missing from the response.
/// Default is false (log warnings only). Enable in dev/test environments for fail-fast behavior.
/// </summary>
public bool StrictResponseValidation { get; set; } = false;
```

### ResponseSchemaValidationHandler

A `DelegatingHandler` in the consumer HTTP pipeline. Intercepts 2xx responses, reads the JSON body, compares field names against the expected DTO's `[JsonPropertyName]` attributes, and either throws or logs depending on the strict mode setting.

**Pipeline position:**
```
TokenRefreshHandler → ResponseSchemaValidationHandler → ErrorNormalizationHandler → ResilienceHandler → ...
```

Placed outside `ErrorNormalizationHandler` so it sees the original response body before any status remapping or error detection. Only validates 2xx responses — error responses (4xx, 5xx) are skipped since their body shapes are different from the DTO.

**Behavior:**
1. Call `base.SendAsync()` to get the response
2. If not 2xx, return immediately (skip validation)
3. Read response body as string
4. Look up the expected DTO type from the Refit interface reflection map using request method + path
5. If no mapping found (unknown endpoint), skip validation
6. Parse JSON field names from the response body (top-level only for flat DTOs, recursive for nested DTOs)
7. Get expected field names from the DTO's `[JsonPropertyName]` attributes
8. Compute extra fields (in JSON, not on DTO) and missing fields (on DTO, not in JSON)
9. For DTOs with `[JsonExtensionData]`, extra fields are expected and not flagged
10. Missing nullable/optional fields are not flagged — only required (non-nullable value type or non-nullable reference type) fields
11. If mismatches found:
    - Strict mode (`StrictResponseValidation = true`): throw `IbkrSchemaViolationException`
    - Non-strict mode: log a warning with endpoint path, DTO type, and field lists
12. Re-buffer the response body for downstream consumption

### Refit Interface Reflection Map

Built once at handler construction time. Scans a provided set of Refit interface types and builds a lookup from `(HttpMethod, path pattern) → response DTO type`.

**Extraction logic:**
- For each method on the Refit interface, read the Refit HTTP attribute (`[Get]`, `[Post]`, `[Delete]`) to get the method and path template
- Read the return type. Unwrap `Task<T>` → `T`. Handle common wrappers:
  - `List<T>` → validate against `T`
  - `Dictionary<string, T>` → validate values against `T`
  - `OneOf<T1, T2>` → skip validation (response shape varies, already handled by `OrderOperations`)
- Path templates use `{param}` placeholders. For matching at runtime, convert to regex patterns (e.g., `/portfolio/{accountId}/positions/{page}` → `/portfolio/[^/]+/positions/[^/]+`)
- Store the set of expected `[JsonPropertyName]` values for each DTO type, along with nullability metadata for each field

**DTO field extraction:**
- Scan the DTO type's constructor parameters and properties for `[JsonPropertyName]` attributes
- Record each field's JSON name, whether it's nullable (reference type with `?`, or `Nullable<T>`), and whether it has a default value
- If the DTO has `[JsonExtensionData]`, note this — extra fields in the JSON are expected and should not be flagged
- For nested DTO types (e.g., `ServerInfo` inside `SsodhInitResponse`), recursively extract field names with dot-notation paths for the validation report

### IbkrSchemaViolationException

```csharp
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
}
```

### Logging (Non-Strict Mode)

Uses `ILogger<ResponseSchemaValidationHandler>` with `LoggerMessage` source generation:

```
[Warning] Response schema mismatch for GET /portfolio/U1234567/summary → AccountSummaryEntry:
  Extra fields: [newField1, newField2]. Missing fields: [deprecatedField].
```

Log level is `Warning` — visible in default configurations but doesn't block execution.

## Files

### New Files
| Path | Purpose |
|------|---------|
| `src/IbkrConduit/Http/ResponseSchemaValidationHandler.cs` | DelegatingHandler with validation logic |
| `src/IbkrConduit/Http/RefitEndpointMap.cs` | Reflection-based Refit interface → DTO type mapping |
| `src/IbkrConduit/Http/DtoFieldMap.cs` | DTO type → expected JSON field names extraction |
| `src/IbkrConduit/Errors/IbkrSchemaViolationException.cs` | New exception type |

### Modified Files
| Path | Change |
|------|--------|
| `src/IbkrConduit/Session/IbkrClientOptions.cs` | Add `StrictResponseValidation` property |
| `src/IbkrConduit/Http/ConsumerPipelineRegistration.cs` | Add `ResponseSchemaValidationHandler` to consumer pipeline |

## Testing

### Unit Tests
- **RefitEndpointMap**: Given a Refit interface with known methods, produces correct `(method, path) → DTO type` mappings. Tests path template → regex conversion. Tests `List<T>`, `Dictionary<string, T>` unwrapping. Tests `OneOf` skipping.
- **DtoFieldMap**: Given a DTO type with `[JsonPropertyName]` attributes, extracts correct field names. Tests nullable detection. Tests `[JsonExtensionData]` detection. Tests nested DTO recursion.
- **ResponseSchemaValidationHandler**: Given a response with extra fields + strict mode, throws `IbkrSchemaViolationException` with correct field lists. Given missing fields + strict mode, throws. Given mismatches + non-strict mode, does not throw (logs warning). Given error response (4xx), skips validation. Given `[JsonExtensionData]` DTO, extra fields are not flagged.

### Integration Tests
- Strict mode: WireMock returns a response with an extra field not on the DTO. Handler throws `IbkrSchemaViolationException` listing the extra field.
- Non-strict mode: Same scenario, no exception, response deserializes normally.

## Scope Boundaries

### In Scope
- `ResponseSchemaValidationHandler` for consumer pipeline
- Reflection map from Refit interfaces
- DTO field extraction with `[JsonPropertyName]`
- `IbkrSchemaViolationException`
- `StrictResponseValidation` option
- Warning logging in non-strict mode
- Unit and integration tests

### Out of Scope
- Validation of session pipeline responses (internal, not consumer-facing)
- Validation of request bodies
- Validation of nested field values (only field presence is checked)
- Validation of field types (only names — type mismatches are caught by STJ deserialization)
- Automatic DTO generation from mismatches
