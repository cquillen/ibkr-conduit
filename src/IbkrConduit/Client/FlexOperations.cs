using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
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

    private static readonly Histogram<int> _pollAttempts =
        IbkrConduitDiagnostics.Meter.CreateHistogram<int>(
            "ibkr.conduit.flex.poll.attempts", "attempts",
            "Number of GetStatement polls per query execution");

    private readonly FlexClient? _flexClient;
    private readonly IbkrClientOptions _options;
    private readonly ILogger<FlexOperations> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates a new <see cref="FlexOperations"/> instance.
    /// </summary>
    /// <param name="flexClient">The Flex HTTP transport, or null if no Flex token is configured.</param>
    /// <param name="options">Client options — used for <see cref="IbkrClientOptions.FlexPollTimeout"/>
    /// and <see cref="IbkrClientOptions.ThrowOnApiError"/>.</param>
    /// <param name="logger">Logger for query lifecycle events.</param>
    /// <param name="timeProvider">Time provider for delay operations. Defaults to <see cref="TimeProvider.System"/> when null.</param>
    public FlexOperations(
        FlexClient? flexClient,
        IbkrClientOptions options,
        ILogger<FlexOperations> logger,
        TimeProvider? timeProvider = null)
    {
        _flexClient = flexClient;
        _options = options;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<Result<CashTransactionsFlexResult>> GetCashTransactionsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureFlexConfigured();
        var queryId = _options.FlexQueries.CashTransactionsQueryId
            ?? throw new InvalidOperationException(
                "Cash Transactions queries require IbkrClientOptions.FlexQueries.CashTransactionsQueryId. " +
                "Create an Activity Flex query with the Cash Transactions section enabled in the IBKR portal " +
                "(Reports → Flex Queries), then set the numeric query ID in AddIbkrClient options.");

        var docResult = await ExecuteInternalAsync(queryId, fromDate: null, toDate: null, cancellationToken);
        var result = docResult.Map(FlexResultParser.ParseCashTransactions);
        return WithThrowSetting(result);
    }

    /// <inheritdoc />
    public async Task<Result<TradeConfirmationsFlexResult>> GetTradeConfirmationsAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        EnsureFlexConfigured();
        var queryId = _options.FlexQueries.TradeConfirmationsQueryId
            ?? throw new InvalidOperationException(
                "Trade Confirmations queries require IbkrClientOptions.FlexQueries.TradeConfirmationsQueryId. " +
                "Create a Trade Confirmation Flex query in the IBKR portal (Reports → Flex Queries), " +
                "then set the numeric query ID in AddIbkrClient options.");

        var fromStr = fromDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var toStr = toDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var docResult = await ExecuteInternalAsync(queryId, fromStr, toStr, cancellationToken);
        var result = docResult.Map(FlexResultParser.ParseTradeConfirmations);
        return WithThrowSetting(result);
    }

    /// <inheritdoc />
    public async Task<Result<FlexGenericResult>> ExecuteQueryAsync(
        string queryId, CancellationToken cancellationToken = default)
    {
        EnsureFlexConfigured();
        var docResult = await ExecuteInternalAsync(queryId, fromDate: null, toDate: null, cancellationToken);
        var result = docResult.Map(FlexResultParser.ParseGeneric);
        return WithThrowSetting(result);
    }

    /// <inheritdoc />
    public async Task<Result<FlexGenericResult>> ExecuteQueryAsync(
        string queryId, string fromDate, string toDate, CancellationToken cancellationToken = default)
    {
        EnsureFlexConfigured();
        var docResult = await ExecuteInternalAsync(queryId, fromDate, toDate, cancellationToken);
        var result = docResult.Map(FlexResultParser.ParseGeneric);
        return WithThrowSetting(result);
    }

    private async Task<Result<XDocument>> ExecuteInternalAsync(
        string queryId, string? fromDate, string? toDate, CancellationToken cancellationToken)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Flex.ExecuteQuery");
        activity?.SetTag(LogFields.QueryId, queryId);
        _queryCount.Add(1);
        var sw = Stopwatch.StartNew();

        const int maxSendAttempts = 3;
        var sendAttempt = 0;
        IbkrFlexError? lastSendError = null;
        var referenceCode = (string?)null;

        while (sendAttempt < maxSendAttempts)
        {
            sendAttempt++;
            var sendResult = await _flexClient!.SendRequestAsync(queryId, fromDate, toDate, cancellationToken);
            if (!sendResult.IsSuccess)
            {
                // Transport error — don't retry network errors
                return Result<XDocument>.Failure(sendResult.Error!);
            }

            var referenceCodeResult = ExtractReferenceCode(sendResult.Value!, queryId);
            if (referenceCodeResult.IsSuccess)
            {
                referenceCode = referenceCodeResult.Value;
                break;
            }

            if (referenceCodeResult.Error is IbkrFlexError flexError)
            {
                if (!flexError.IsRetryable)
                {
                    return Result<XDocument>.Failure(flexError);
                }

                lastSendError = flexError;
                LogTransientSendResponse(sendAttempt, flexError.ErrorCode, flexError.Message ?? "(none)");

                if (sendAttempt < maxSendAttempts)
                {
                    var baseDelay = flexError.ErrorCode == _rateLimitErrorCode
                        ? _rateLimitDelayMs
                        : sendAttempt * 1000;
                    await Task.Delay(TimeSpan.FromMilliseconds(ApplyJitter(baseDelay)), _timeProvider, cancellationToken);
                    continue;
                }
            }

            // All attempts exhausted or non-flex error
            var errorMessage = lastSendError is not null
                ? $"Flex SendRequest did not succeed after {maxSendAttempts} attempts for query '{queryId}'. " +
                  $"Last response: code {lastSendError.ErrorCode} ({lastSendError.Message})."
                : referenceCodeResult.Error!.Message;
            _errorCount.Add(1, new KeyValuePair<string, object?>(LogFields.ErrorCode, lastSendError?.ErrorCode ?? 0));
            return Result<XDocument>.Failure(new IbkrFlexError(
                ErrorCode: lastSendError?.ErrorCode ?? 0,
                CodeDescription: lastSendError?.CodeDescription,
                IsRetryable: false,
                Message: errorMessage,
                RawBody: lastSendError?.RawBody,
                RequestPath: $"flex/SendRequest?q={queryId}"));
        }

        if (referenceCode is null)
        {
            return Result<XDocument>.Failure(new IbkrFlexError(
                ErrorCode: 0,
                CodeDescription: null,
                IsRetryable: false,
                Message: "SendRequest failed unexpectedly.",
                RawBody: null,
                RequestPath: $"flex/SendRequest?q={queryId}"));
        }

        var docResult = await PollForStatementAsync(referenceCode, cancellationToken);
        _queryDuration.Record(sw.Elapsed.TotalMilliseconds);

        var pollStatus = docResult.IsSuccess ? "success"
            : docResult.Error is IbkrFlexError fe && fe.ErrorCode == 0 && fe.IsRetryable ? "timeout"
            : "error";
        _pollAttempts.Record(_lastPollAttemptCount, new KeyValuePair<string, object?>("status", pollStatus));

        if (docResult.IsSuccess)
        {
            LogFlexQueryExecuted(queryId);
        }
        return docResult;
    }

    // Tracks the last poll attempt count for metric recording in ExecuteInternalAsync.
    // This is set by PollForStatementAsync before returning. Thread-safety is not a concern
    // because each FlexOperations call is sequential within a single execution.
    private int _lastPollAttemptCount;

    private Result<T> WithThrowSetting<T>(Result<T> result) =>
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
        var lastErrorCode = 0;
        var lastErrorMessage = "(none)";

        while (totalWaited < maxWaitMs)
        {
            attempt++;
            _pollCount.Add(1);

            var fetchResult = await _flexClient!.GetStatementAsync(referenceCode, cancellationToken);
            if (!fetchResult.IsSuccess)
            {
                _lastPollAttemptCount = attempt;
                return fetchResult;
            }

            var classification = ClassifyPollResponse(fetchResult.Value!, out var errorCode, out var errorMessage);

            if (classification == FlexResponseClass.Success)
            {
                _lastPollAttemptCount = attempt;
                LogGetStatementCompleted(totalWaited);
                return fetchResult;
            }

            lastErrorCode = errorCode;
            lastErrorMessage = errorMessage ?? "(none)";

            if (classification == FlexResponseClass.Permanent)
            {
                _lastPollAttemptCount = attempt;
                _errorCount.Add(1, new KeyValuePair<string, object?>(LogFields.ErrorCode, errorCode));
                LogPermanentError(attempt, errorCode, lastErrorMessage);
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

            var actualDelay = Math.Min(ApplyJitter(delayMs), remaining);
            LogTransientResponse(attempt, errorCode, lastErrorMessage, actualDelay, totalWaited);
            await Task.Delay(TimeSpan.FromMilliseconds(actualDelay), _timeProvider, cancellationToken);
            totalWaited += actualDelay;
        }

        _lastPollAttemptCount = attempt;
        var timeoutSeconds = _options.FlexPollTimeout.TotalSeconds;
        return Result<XDocument>.Failure(new IbkrFlexError(
            ErrorCode: 0,
            CodeDescription: null,
            IsRetryable: true,
            Message: $"Flex statement generation did not complete within {timeoutSeconds}s for reference code '{referenceCode}'. " +
                     $"Attempted {attempt} polls; last response: code {lastErrorCode} ({lastErrorMessage}). " +
                     "If using an Activity Flex query with 'Breakout by Day' enabled, disabling it in the IBKR portal may significantly reduce generation time.",
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

    /// <summary>
    /// Applies ±20% random jitter to a base delay to avoid thundering-herd effects.
    /// Returns at least 100ms to avoid zero/negative delays.
    /// </summary>
    internal static int ApplyJitter(int baseDelayMs)
    {
        var jitter = (int)(baseDelayMs * 0.2 * (Random.Shared.NextDouble() * 2 - 1));
        return Math.Max(100, baseDelayMs + jitter);
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

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Flex SendRequest attempt #{Attempt} returned transient error {ErrorCode}: {ErrorMessage}")]
    private partial void LogTransientSendResponse(int attempt, int errorCode, string errorMessage);
}
