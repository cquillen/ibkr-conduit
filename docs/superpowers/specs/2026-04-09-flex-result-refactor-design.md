# Flex Result<T> Refactor + Layer Separation — Design Spec

## Goal

Bring `IFlexOperations` into the library's `Result<T>` pattern and separate transport concerns (in `FlexClient`) from orchestration concerns (in `FlexOperations`). Eliminate `FlexQueryException` in favor of `IbkrFlexError` + the standard `Result<T>.EnsureSuccess()` throw path.

## Background

`FlexOperations.ExecuteQueryAsync` is the only operations method in the library that returns a raw `Task<T>` instead of `Task<Result<T>>`. It also throws `FlexQueryException` on errors instead of surfacing them as `IbkrError` subtypes. This inconsistency means:

1. Consumers must use two different error-handling patterns depending on whether they're calling Flex or other operations.
2. `IbkrClientOptions.ThrowOnApiError` doesn't apply to Flex — it always throws.
3. The classification work in PR #114 (`FlexQueryException.IsRetryable`, `CodeDescription`) is partially wasted because consumers of the Result pattern never see it.

Additionally, `FlexClient` currently owns too much:
- HTTP plumbing (legitimate)
- Polling loop (orchestration)
- Response classification (domain logic)
- Retry delays and rate-limit handling (orchestration)
- Timeout enforcement (orchestration)
- All query lifecycle metrics (orchestration)

The right split is: `FlexClient` = transport, `FlexOperations` = everything else.

## Design

### Layer 1: `FlexClient` — thin HTTP wrapper

Only two public methods, each wrapping a single Flex endpoint call. No polling, no classification, no retry logic, no domain-level metrics.

```csharp
internal sealed partial class FlexClient
{
    private const string _defaultBaseUrl = "https://ndcdyn.interactivebrokers.com/AccountManagement/FlexWebService/";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _clientName;
    private readonly string _flexToken;
    private readonly string _baseUrl;
    private readonly ILogger<FlexClient> _logger;

    public FlexClient(
        IHttpClientFactory httpClientFactory,
        string clientName,
        string flexToken,
        ILogger<FlexClient> logger,
        string? baseUrl = null)
    {
        _httpClientFactory = httpClientFactory;
        _clientName = clientName;
        _flexToken = flexToken;
        _baseUrl = baseUrl ?? _defaultBaseUrl;
        _logger = logger;
    }

    /// <summary>
    /// Calls the SendRequest endpoint. Returns the raw XML response, or a transport error
    /// if the HTTP call failed or the response body could not be parsed as XML.
    /// Does not interpret the response content — that is the caller's responsibility.
    /// </summary>
    public async Task<Result<XDocument>> SendRequestAsync(
        string queryId, string? fromDate, string? toDate, CancellationToken cancellationToken)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Flex.SendRequest");
        activity?.SetTag(LogFields.QueryId, queryId);

        var url = $"{_baseUrl}SendRequest?t={_flexToken}&q={queryId}&v=3";
        if (!string.IsNullOrEmpty(fromDate) && !string.IsNullOrEmpty(toDate))
        {
            url += $"&fd={fromDate}&td={toDate}";
        }

        return await GetXmlAsync(url, $"flex/SendRequest?q={queryId}", cancellationToken);
    }

    /// <summary>
    /// Calls the GetStatement endpoint exactly once. Returns the raw XML response, or a
    /// transport error if the HTTP call failed or the response body could not be parsed
    /// as XML. Does not poll, classify, or retry — that is the caller's responsibility.
    /// </summary>
    public async Task<Result<XDocument>> GetStatementAsync(
        string referenceCode, CancellationToken cancellationToken)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Flex.GetStatement");
        activity?.SetTag("reference_code", referenceCode);

        var url = $"{_baseUrl}GetStatement?t={_flexToken}&q={referenceCode}&v=3";
        return await GetXmlAsync(url, $"flex/GetStatement?q={referenceCode}", cancellationToken);
    }

    private async Task<Result<XDocument>> GetXmlAsync(
        string url, string requestPath, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(_clientName);

        string responseStr;
        try
        {
            responseStr = await httpClient.GetStringAsync(url, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return Result<XDocument>.Failure(new IbkrApiError(
                StatusCode: ex.StatusCode,
                Message: ex.Message,
                RawBody: null,
                RequestPath: requestPath));
        }

        LogRawResponse(requestPath, responseStr);

        try
        {
            return Result<XDocument>.Success(XDocument.Parse(responseStr));
        }
        catch (XmlException ex)
        {
            return Result<XDocument>.Failure(new IbkrApiError(
                StatusCode: null,
                Message: $"Flex response was not valid XML: {ex.Message}",
                RawBody: responseStr,
                RequestPath: requestPath));
        }
    }

    [LoggerMessage(Level = LogLevel.Trace, Message = "Flex {RequestPath} response body: {ResponseBody}")]
    private partial void LogRawResponse(string requestPath, string responseBody);
}
```

**What FlexClient no longer owns** (moves to `FlexOperations`):
- `PollForStatementAsync`
- `ClassifyPollResponse`, `FlexResponseClass` enum
- `_pollDelaysMs`, `_rateLimitErrorCode`, `_rateLimitDelayMs`
- `_maxTotalWaitMs`, `pollTimeout` constructor parameter
- `_pollCount`, `_lastPollCount`, `_queryDuration`, `_queryCount`, `_errorCount` metrics
- `FlexErrorCodes` lookups
- Log methods `LogFlexQueryExecuted`, `LogSendRequestSucceeded`, `LogGetStatementCompleted`, `LogTransientResponse`, `LogPermanentError`, `LogPollResponseBody`
- The combined `ExecuteQueryAsync` method (moves up to FlexOperations)

### Layer 2: `FlexOperations` — orchestration + domain logic

```csharp
internal sealed partial class FlexOperations : IFlexOperations
{
    // Retry delay schedule — ramp up, then cap at 5s
    private static readonly int[] _pollDelaysMs =
        [1000, 2000, 3000, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000];

    // Rate limit code gets a longer fixed backoff to respect IBKR's 10 req/min/token limit
    private const int _rateLimitErrorCode = 1018;
    private const int _rateLimitDelayMs = 10000;

    // Metrics (moved from FlexClient)
    private static readonly Histogram<double> _queryDuration =
        IbkrConduitDiagnostics.Meter.CreateHistogram<double>("ibkr.conduit.flex.query.duration", "ms");
    private static readonly Counter<long> _queryCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.flex.query.count");
    private static readonly Counter<long> _pollCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.flex.poll.count");
    private static readonly Counter<long> _errorCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.flex.error.count");

    private readonly FlexClient? _flexClient;
    private readonly IbkrClientOptions _options;
    private readonly ILogger<FlexOperations> _logger;

    public FlexOperations(
        FlexClient? flexClient,
        IbkrClientOptions options,
        ILogger<FlexOperations> logger)
    {
        _flexClient = flexClient;
        _options = options;
        _logger = logger;
    }

    public Task<Result<FlexQueryResult>> ExecuteQueryAsync(
        string queryId, CancellationToken cancellationToken = default) =>
        ExecuteInternalAsync(queryId, fromDate: null, toDate: null, cancellationToken);

    public Task<Result<FlexQueryResult>> ExecuteQueryAsync(
        string queryId, string fromDate, string toDate, CancellationToken cancellationToken = default) =>
        ExecuteInternalAsync(queryId, fromDate, toDate, cancellationToken);

    private async Task<Result<FlexQueryResult>> ExecuteInternalAsync(
        string queryId, string? fromDate, string? toDate, CancellationToken cancellationToken)
    {
        EnsureFlexConfigured();

        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Flex.ExecuteQuery");
        activity?.SetTag(LogFields.QueryId, queryId);
        _queryCount.Add(1);
        var sw = Stopwatch.StartNew();

        // Step 1: Send
        var sendResult = await _flexClient!.SendRequestAsync(queryId, fromDate, toDate, cancellationToken);
        if (!sendResult.IsSuccess)
        {
            return WithThrowSetting(Result<FlexQueryResult>.Failure(sendResult.Error));
        }

        var referenceCodeResult = ExtractReferenceCode(sendResult.Value, queryId);
        if (!referenceCodeResult.IsSuccess)
        {
            return WithThrowSetting(Result<FlexQueryResult>.Failure(referenceCodeResult.Error));
        }

        // Step 2: Poll
        var docResult = await PollForStatementAsync(referenceCodeResult.Value, cancellationToken);
        _queryDuration.Record(sw.Elapsed.TotalMilliseconds);

        var result = docResult.Map(doc => new FlexQueryResult(doc));
        if (result.IsSuccess)
        {
            LogFlexQueryExecuted(queryId);
        }
        return WithThrowSetting(result);
    }

    private Result<FlexQueryResult> WithThrowSetting(Result<FlexQueryResult> result) =>
        _options.ThrowOnApiError ? result.EnsureSuccess() : result;

    private static Result<string> ExtractReferenceCode(XDocument doc, string queryId)
    {
        var root = doc.Root!;
        var status = root.Element("Status")?.Value;

        if (!string.Equals(status, "Success", StringComparison.OrdinalIgnoreCase))
        {
            return Result<string>.Failure(BuildFlexErrorFromResponse(doc, $"flex/SendRequest?q={queryId}"));
        }

        var referenceCode = root.Element("ReferenceCode")?.Value;
        if (string.IsNullOrEmpty(referenceCode))
        {
            return Result<string>.Failure(new IbkrFlexError(
                ErrorCode: 0,
                CodeDescription: null,
                IsRetryable: false,
                Message: "SendRequest returned Success but no ReferenceCode.",
                RawBody: doc.ToString(),
                RequestPath: $"flex/SendRequest?q={queryId}"));
        }

        return Result<string>.Success(referenceCode);
    }

    private async Task<Result<XDocument>> PollForStatementAsync(
        string referenceCode, CancellationToken cancellationToken)
    {
        var requestPath = $"flex/GetStatement?q={referenceCode}";
        var maxWaitMs = (int)_options.FlexPollTimeout.TotalMilliseconds;
        var totalWaited = 0;
        var attempt = 0;

        while (totalWaited < maxWaitMs)
        {
            attempt++;
            _pollCount.Add(1);

            var fetchResult = await _flexClient!.GetStatementAsync(referenceCode, cancellationToken);
            if (!fetchResult.IsSuccess)
            {
                return fetchResult; // transport error — propagate as-is
            }

            var classification = ClassifyPollResponse(fetchResult.Value, out var errorCode, out var errorMessage);

            if (classification == FlexResponseClass.Success)
            {
                LogGetStatementCompleted(totalWaited);
                return fetchResult;
            }

            if (classification == FlexResponseClass.Permanent)
            {
                _errorCount.Add(1, new KeyValuePair<string, object?>(LogFields.ErrorCode, errorCode));
                LogPermanentError(attempt, errorCode, errorMessage ?? "(none)");
                var info = FlexErrorCodes.TryLookup(errorCode);
                return Result<XDocument>.Failure(new IbkrFlexError(
                    ErrorCode: errorCode,
                    CodeDescription: info?.Description,
                    IsRetryable: false,
                    Message: errorMessage ?? "Unknown Flex query error",
                    RawBody: fetchResult.Value.ToString(),
                    RequestPath: requestPath));
            }

            // Transient — choose delay and continue
            var delayMs = errorCode == _rateLimitErrorCode
                ? _rateLimitDelayMs
                : attempt <= _pollDelaysMs.Length
                    ? _pollDelaysMs[attempt - 1]
                    : 5000;

            var remaining = maxWaitMs - totalWaited;
            if (remaining <= 0)
            {
                break;
            }

            var actualDelay = Math.Min(delayMs, remaining);
            LogTransientResponse(attempt, errorCode, errorMessage ?? "(none)", actualDelay, totalWaited);
            await Task.Delay(actualDelay, cancellationToken);
            totalWaited += actualDelay;
        }

        // Timeout — synthetic IbkrFlexError with ErrorCode=0, IsRetryable=true
        return Result<XDocument>.Failure(new IbkrFlexError(
            ErrorCode: 0,
            CodeDescription: null,
            IsRetryable: true,
            Message: $"Flex statement generation did not complete within {_options.FlexPollTimeout.TotalSeconds}s for reference code '{referenceCode}'.",
            RawBody: null,
            RequestPath: requestPath));
    }

    private enum FlexResponseClass { Success, Transient, Permanent }

    private static FlexResponseClass ClassifyPollResponse(
        XDocument doc, out int errorCode, out string? errorMessage)
    {
        // Same logic as the current implementation in FlexClient —
        // root=FlexQueryResponse → Success; known codes use FlexErrorCodes table;
        // unknown codes fall back to Status element.
        errorCode = 0;
        errorMessage = null;

        var root = doc.Root;
        if (root is null)
        {
            errorMessage = "Flex response has no root element.";
            return FlexResponseClass.Permanent;
        }

        if (string.Equals(root.Name.LocalName, "FlexQueryResponse", StringComparison.Ordinal))
        {
            return FlexResponseClass.Success;
        }

        var status = root.Element("Status")?.Value;
        var errorCodeStr = root.Element("ErrorCode")?.Value;
        errorMessage = root.Element("ErrorMessage")?.Value;

        if (int.TryParse(errorCodeStr, out var parsedCode))
        {
            errorCode = parsedCode;
        }

        var info = FlexErrorCodes.TryLookup(errorCode);
        if (info is not null)
        {
            errorMessage ??= info.Description;
            return info.IsRetryable ? FlexResponseClass.Transient : FlexResponseClass.Permanent;
        }

        return status switch
        {
            "Success" => FlexResponseClass.Success,
            "Warn" => FlexResponseClass.Transient,
            _ => FlexResponseClass.Permanent,
        };
    }

    private static IbkrFlexError BuildFlexErrorFromResponse(XDocument doc, string requestPath)
    {
        var root = doc.Root!;
        var errorCodeStr = root.Element("ErrorCode")?.Value;
        var errorMessage = root.Element("ErrorMessage")?.Value;
        _ = int.TryParse(errorCodeStr, out var errorCode);
        var info = FlexErrorCodes.TryLookup(errorCode);

        return new IbkrFlexError(
            ErrorCode: errorCode,
            CodeDescription: info?.Description,
            IsRetryable: info?.IsRetryable ?? false,
            Message: errorMessage ?? info?.Description ?? "Unknown Flex query error",
            RawBody: doc.ToString(),
            RequestPath: requestPath);
    }

    private void EnsureFlexConfigured()
    {
        if (_flexClient == null)
        {
            throw new InvalidOperationException(
                "Flex operations require a FlexToken to be configured in IbkrClientOptions. " +
                "Set IbkrClientOptions.FlexToken when calling AddIbkrClient().");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Flex query {QueryId} executed successfully")]
    private partial void LogFlexQueryExecuted(string queryId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Flex GetStatement completed after {TotalWaited}ms")]
    private partial void LogGetStatementCompleted(int totalWaited);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Flex GetStatement poll #{Attempt} returned transient error {ErrorCode}: {ErrorMessage} — waiting {DelayMs}ms before retry (total waited: {TotalWaited}ms)")]
    private partial void LogTransientResponse(int attempt, int errorCode, string errorMessage, int delayMs, int totalWaited);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Flex GetStatement poll #{Attempt} returned permanent error {ErrorCode}: {ErrorMessage}")]
    private partial void LogPermanentError(int attempt, int errorCode, string errorMessage);
}
```

### Layer 3: New error type — `IbkrFlexError`

Add to `src/IbkrConduit/Errors/IbkrError.cs`:

```csharp
/// <summary>
/// Error from a Flex Web Service query. Carries the numeric error code,
/// the canonical description from IBKR's documentation, and whether the
/// error is retryable. StatusCode is null because Flex returns all logical
/// errors with HTTP 200 — the actual error information is in the XML body.
/// </summary>
[ExcludeFromCodeCoverage]
public record IbkrFlexError(
    int ErrorCode,
    string? CodeDescription,
    bool IsRetryable,
    string? Message,
    string? RawBody,
    string? RequestPath)
    : IbkrError(null, Message, RawBody, RequestPath);
```

### Public interface change

```csharp
public interface IFlexOperations
{
    /// <summary>
    /// Executes a Flex Query using the template's configured period.
    /// </summary>
    Task<Result<FlexQueryResult>> ExecuteQueryAsync(
        string queryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a Flex Query with a runtime date range override.
    /// Date format: yyyyMMdd.
    /// </summary>
    Task<Result<FlexQueryResult>> ExecuteQueryAsync(
        string queryId, string fromDate, string toDate,
        CancellationToken cancellationToken = default);
}
```

### Files deleted

- `src/IbkrConduit/Flex/FlexQueryException.cs`
- `tests/IbkrConduit.Tests.Unit/Flex/FlexQueryExceptionTests.cs`

### Files moved/renamed

- `tests/IbkrConduit.Tests.Unit/Flex/FlexClientPollingTests.cs` → `FlexOperationsPollingTests.cs`
  - Namespace and class rename
  - Test helper `CreateClient` → `CreateOperations` (constructs `FlexOperations` with a stubbed `FlexClient`)
  - All test fixtures and assertions stay the same — the behavior is unchanged, just relocated

### DI registration

`src/IbkrConduit/Http/StreamingAndFlexRegistration.cs`:

```csharp
services.AddSingleton(sp => new FlexClient(
    sp.GetRequiredService<IHttpClientFactory>(),
    flexClientName,
    flexToken,
    sp.GetRequiredService<ILogger<FlexClient>>()));
// No more pollTimeout parameter

services.AddSingleton<IFlexOperations>(sp =>
    new FlexOperations(
        sp.GetRequiredService<FlexClient>(),
        sp.GetRequiredService<IbkrClientOptions>(),
        sp.GetRequiredService<ILogger<FlexOperations>>()));
```

### Caller updates

- `tools/CaptureFlexQuery/Program.cs` — current code calls `await client.Flex.ExecuteQueryAsync(queryId)` and accesses `result.RawXml`, `result.Trades.Count`, `result.OpenPositions.Count`. After refactor: needs `.EnsureSuccess().Value.RawXml` or pattern matching on the Result.
- Any E2E integration tests using Flex
- Any examples

## Testing

### Unit tests

**FlexClientTests (shrinks):**
- `SendRequestAsync` with success XML → returns `Result<XDocument>.Success` with parsed document
- `SendRequestAsync` with HttpRequestException → returns `Result<XDocument>.Failure(IbkrApiError)` with status code if available
- `SendRequestAsync` with invalid XML response → returns `Result<XDocument>.Failure(IbkrApiError)` with raw body
- `SendRequestAsync` includes `fd`/`td` query params when both dates provided
- `SendRequestAsync` does NOT include `fd`/`td` when dates are null
- `GetStatementAsync` with success XML → returns `Result<XDocument>.Success`
- `GetStatementAsync` with HttpRequestException → returns `Result<XDocument>.Failure`
- `GetStatementAsync` with invalid XML → returns `Result<XDocument>.Failure`
- `SendRequestAsync`/`GetStatementAsync` pass cancellation token to HttpClient
- `SendRequestAsync`/`GetStatementAsync` do not retry, poll, or classify — a single HTTP call per invocation

**FlexOperationsPollingTests (renamed from FlexClientPollingTests):**
- `ExecuteQueryAsync` with successful send and immediate success on first poll → returns `Result<FlexQueryResult>.Success`
- `ExecuteQueryAsync` with transient 1019 then success → polls multiple times, eventually succeeds
- `ExecuteQueryAsync` with permanent error 1015 on poll → returns `Result<FlexQueryResult>.Failure(IbkrFlexError)` with code 1015, IsRetryable=false
- `ExecuteQueryAsync` with retryable 1004 code → polls until success
- `ExecuteQueryAsync` with rate limit 1018 → uses longer backoff
- `ExecuteQueryAsync` with unknown code + Status=Warn → treated as transient
- `ExecuteQueryAsync` with unknown code + Status=Fail → treated as permanent
- `ExecuteQueryAsync` with SendRequest returning Status=Fail → returns `Result.Failure(IbkrFlexError)` without polling
- `ExecuteQueryAsync` with SendRequest returning Status=Success but no ReferenceCode → returns `Result.Failure`
- `ExecuteQueryAsync` with poll loop exhausting timeout → returns `Result.Failure(IbkrFlexError)` with ErrorCode=0 and IsRetryable=true
- `ExecuteQueryAsync` with cancellation during poll → throws `OperationCanceledException`
- `ExecuteQueryAsync` with transport error from GetStatement → returns `Result.Failure(IbkrApiError)` without retrying (transport errors are not Flex-level retry decisions)
- `ExecuteQueryAsync` with `ThrowOnApiError = true` and error → throws `IbkrApiException` wrapping the `IbkrFlexError`
- `ExecuteQueryAsync` with `ThrowOnApiError = false` and error → returns `Result.Failure`
- `ExecuteQueryAsync` without Flex token configured → throws `InvalidOperationException` (not wrapped in Result — it's a configuration error)
- `ExecuteQueryAsync` with date range → passes `fd`/`td` through to `FlexClient.SendRequestAsync`

**FlexErrorCodesTests:**
- No changes needed — the table itself is unchanged

**New: IbkrFlexErrorTests** (small):
- Can construct with all fields
- Inherits from IbkrError correctly

### Integration tests

Existing `FlexIntegrationTests` uses WireMock. The only change needed is updating the assertions from `(await client.Flex.ExecuteQueryAsync(...)).Trades` to `(await client.Flex.ExecuteQueryAsync(...)).Value.Trades` — or adding `.EnsureSuccess()`, depending on whether the test wants to verify success or error behavior.

Add at least one new integration test:
- Strict mode off: query with error response returns `Result.Failure(IbkrFlexError)` instead of throwing

## Scope Boundaries

### In Scope
- New `IbkrFlexError` error subtype
- `FlexClient` stripped down to transport only
- `FlexOperations` gains polling loop, classification, metrics, timeout enforcement
- `IFlexOperations` signatures return `Task<Result<FlexQueryResult>>`
- `FlexQueryException` and its tests deleted
- `FlexClientPollingTests` moved to `FlexOperationsPollingTests`
- DI registration simplified
- `CaptureFlexQuery` tool updated for new return type
- E2E tests and examples updated

### Out of Scope
- Making `FlexErrorCodes` public (stays internal per earlier decision)
- Tightening `FlexClient?` nullable in `FlexOperations` (stays as-is per earlier decision)
- Changes to `FlexQueryResult` or the typed `FlexTrade`/`FlexOpenPosition` parsing
- Adding `FlexCashTransaction` typed support (noted as a separate future item)
- Adding a generic `IbkrTimeoutError` subtype — timeout is modeled as `IbkrFlexError` with `ErrorCode=0`
- Rate-limiter at the HTTP layer (separate suggestion in the safety report)
