# FlexQueryException IsRetryable + CodeDescription — Design Spec

## Goal

Surface the `FlexErrorCodes` classification (retryable vs permanent) and canonical code description on `FlexQueryException` so consumers can make retry decisions without replicating the library's internal table.

## Background

`FlexErrorCodes` (added in PR #113) classifies each of IBKR's 20 documented error codes as retryable or permanent, with canonical descriptions. The table is used internally by `FlexClient.ClassifyPollResponse` but doesn't surface through the public `FlexQueryException` API. Callers who want to implement their own retry logic at a higher layer must either hardcode the table themselves or parse error code numbers by hand.

## Design

### Changes to `FlexQueryException`

```csharp
public class FlexQueryException : Exception
{
    public int ErrorCode { get; }

    /// <summary>
    /// Whether this error is documented as retryable (transient) or permanent.
    /// Populated from the FlexErrorCodes table. Unknown codes default to false
    /// as a conservative safety measure — callers should not blindly retry
    /// errors the library doesn't recognize.
    /// </summary>
    public bool IsRetryable { get; }

    /// <summary>
    /// The canonical description for this error code from IBKR's documentation,
    /// or null if the code is not in the known table. This is separate from
    /// <see cref="Exception.Message"/>, which carries whatever the server
    /// returned at runtime (typically the same text, but may differ).
    /// </summary>
    public string? CodeDescription { get; }

    public FlexQueryException(int errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
        var info = FlexErrorCodes.TryLookup(errorCode);
        IsRetryable = info?.IsRetryable ?? false;
        CodeDescription = info?.Description;
    }
}
```

### Safe defaults

Unknown codes get `IsRetryable = false` and `CodeDescription = null`. Rationale:
- If the library doesn't recognize a code, we can't guarantee retrying is safe.
- A consumer retrying an unknown error might loop forever on something like "account suspended" that we haven't seen before.
- Callers can always fall back to their own retry logic (e.g., attempt N times regardless).

### Backward compatibility

The existing constructor signature `(int errorCode, string message)` is preserved. Existing call sites don't need changes. The new properties are computed inside the constructor from the lookup table.

## Testing

- **Unit test: known retryable code** — `new FlexQueryException(1019, "...")` → `IsRetryable == true`, `CodeDescription` contains "in progress"
- **Unit test: known permanent code** — `new FlexQueryException(1015, "...")` → `IsRetryable == false`, `CodeDescription` contains "Token is invalid"
- **Unit test: unknown code** — `new FlexQueryException(9999, "...")` → `IsRetryable == false`, `CodeDescription == null`
- **Unit test: exception message unchanged** — the `Message` property still returns what was passed to the constructor

## Files

| Path | Change |
|------|--------|
| `src/IbkrConduit/Flex/FlexQueryException.cs` | Add `IsRetryable` and `CodeDescription` properties, populate in constructor |
| `tests/IbkrConduit.Tests.Unit/Flex/FlexQueryExceptionTests.cs` | Add tests for the new properties (create file if not exists) |

## Scope Boundaries

### In Scope
- `IsRetryable` property computed from `FlexErrorCodes.TryLookup`
- `CodeDescription` property containing the canonical description
- Unit tests for known/unknown code behavior

### Out of Scope
- Automatic retry logic in consumer-facing APIs (callers still implement their own)
- Changes to `FlexClient` or the poll loop (the classification is already used internally)
- Changes to `FlexErrorCodes` table (no new codes)
- Making `FlexErrorCodes` public (the table stays internal; consumers get the classification via `FlexQueryException`)
