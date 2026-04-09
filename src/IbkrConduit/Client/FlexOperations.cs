using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Xml.Linq;
using IbkrConduit.Diagnostics;
using IbkrConduit.Errors;
using IbkrConduit.Flex;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Client;

/// <summary>
/// Default implementation of <see cref="IFlexOperations"/>. Owns the Flex query
/// orchestration: SendRequest → poll GetStatement until complete, classify
/// responses, honor retry/rate-limit schedules, and enforce the poll timeout.
/// Delegates raw HTTP calls to <see cref="FlexClient"/>.
/// </summary>
internal sealed partial class FlexOperations : IFlexOperations
{
    // Retry delay schedule — ramp up, then cap at 5s
    private static readonly int[] _pollDelaysMs =
        [1000, 2000, 3000, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000];

    // Rate limit code gets a longer fixed backoff to respect IBKR's 10 req/min/token limit
    private const int _rateLimitErrorCode = 1018;
    private const int _rateLimitDelayMs = 10000;

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

    /// <summary>
    /// Creates a new <see cref="FlexOperations"/> instance.
    /// </summary>
    /// <param name="flexClient">The Flex HTTP transport, or null if no Flex token is configured.</param>
    /// <param name="options">Client options — used for <see cref="IbkrClientOptions.FlexPollTimeout"/>
    /// and <see cref="IbkrClientOptions.ThrowOnApiError"/>.</param>
    /// <param name="logger">Logger for query lifecycle events.</param>
    public FlexOperations(
        FlexClient? flexClient,
        IbkrClientOptions options,
        ILogger<FlexOperations> logger)
    {
        _flexClient = flexClient;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<FlexQueryResult>> ExecuteQueryAsync(
        string queryId, CancellationToken cancellationToken = default) =>
        ExecuteInternalAsync(queryId, fromDate: null, toDate: null, cancellationToken);

    /// <inheritdoc />
    public Task<Result<FlexQueryResult>> ExecuteQueryAsync(
        string queryId, string fromDate, string toDate,
        CancellationToken cancellationToken = default) =>
        ExecuteInternalAsync(queryId, fromDate, toDate, cancellationToken);

    private async Task<Result<FlexQueryResult>> ExecuteInternalAsync(
        string queryId, string? fromDate, string? toDate, CancellationToken cancellationToken)
    {
        EnsureFlexConfigured();

        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Flex.ExecuteQuery");
        activity?.SetTag(LogFields.QueryId, queryId);
        _queryCount.Add(1);
        var sw = Stopwatch.StartNew();

        var sendResult = await _flexClient!.SendRequestAsync(queryId, fromDate, toDate, cancellationToken);
        if (!sendResult.IsSuccess)
        {
            return WithThrowSetting(Result<FlexQueryResult>.Failure(sendResult.Error!));
        }

        var referenceCodeResult = ExtractReferenceCode(sendResult.Value!, queryId);
        if (!referenceCodeResult.IsSuccess)
        {
            return WithThrowSetting(Result<FlexQueryResult>.Failure(referenceCodeResult.Error!));
        }

        var docResult = await PollForStatementAsync(referenceCodeResult.Value!, cancellationToken);
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
        var requestPath = $"flex/SendRequest?q={queryId}";
        var root = doc.Root!;
        var status = root.Element("Status")?.Value;

        if (!string.Equals(status, "Success", StringComparison.OrdinalIgnoreCase))
        {
            return Result<string>.Failure(BuildFlexErrorFromResponse(doc, requestPath));
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
                RequestPath: requestPath));
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
                return fetchResult;
            }

            var classification = ClassifyPollResponse(fetchResult.Value!, out var errorCode, out var errorMessage);

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
                    RawBody: fetchResult.Value!.ToString(),
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

        return Result<XDocument>.Failure(new IbkrFlexError(
            ErrorCode: 0,
            CodeDescription: null,
            IsRetryable: true,
            Message: $"Flex statement generation did not complete within {_options.FlexPollTimeout.TotalSeconds}s for reference code '{referenceCode}'.",
            RawBody: null,
            RequestPath: requestPath));
    }

    private enum FlexResponseClass
    {
        Success,
        Transient,
        Permanent,
    }

    private static FlexResponseClass ClassifyPollResponse(
        XDocument doc, out int errorCode, out string? errorMessage)
    {
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
