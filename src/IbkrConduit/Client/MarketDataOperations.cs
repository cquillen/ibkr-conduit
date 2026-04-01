using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text.Json;
using IbkrConduit.Diagnostics;
using IbkrConduit.MarketData;
using IbkrConduit.Session;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Client;

/// <summary>
/// Market data operations that delegate to the underlying Refit API.
/// Handles IBKR pre-flight requirements transparently for snapshot requests.
/// </summary>
public partial class MarketDataOperations : IMarketDataOperations, IDisposable
{
    private const int _preflightDelayMs = 500;

    private static readonly Histogram<double> _snapshotDuration =
        IbkrConduitDiagnostics.Meter.CreateHistogram<double>("ibkr.conduit.marketdata.snapshot.duration", "ms");

    private static readonly Counter<long> _snapshotCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.marketdata.snapshot.count");

    private static readonly Counter<long> _preflightCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.marketdata.snapshot.preflight.count");

    private static readonly Histogram<double> _historyDuration =
        IbkrConduitDiagnostics.Meter.CreateHistogram<double>("ibkr.conduit.marketdata.history.duration", "ms");

    private static readonly Counter<long> _historyCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.marketdata.history.count");

    private static readonly HashSet<string> _nonDataKeys = new(StringComparer.Ordinal)
    {
        "conid", "conidEx", "server_id", "_updated", "6509",
    };

    private readonly IIbkrMarketDataApi _api;
    private readonly IbkrClientOptions _options;
    private readonly ILogger<MarketDataOperations> _logger;
    private readonly MemoryCache _preflightCache;

    /// <summary>
    /// Creates a new <see cref="MarketDataOperations"/> instance.
    /// </summary>
    /// <param name="api">The Refit market data API client.</param>
    /// <param name="options">Client options for pre-flight cache duration.</param>
    /// <param name="logger">Logger instance.</param>
    public MarketDataOperations(
        IIbkrMarketDataApi api,
        IbkrClientOptions options,
        ILogger<MarketDataOperations> logger)
    {
        _api = api;
        _options = options;
        _logger = logger;
        // MemoryCache doesn't support global default expiration — it's set per-entry
        // in GetSnapshotAsync using _options.PreflightCacheDuration
        _preflightCache = new MemoryCache(new MemoryCacheOptions());
    }

    /// <inheritdoc />
    public async Task<List<MarketDataSnapshot>> GetSnapshotAsync(int[] conids, string[] fields,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.MarketData.Snapshot");
        activity?.SetTag("conid_count", conids.Length);
        activity?.SetTag("field_count", fields.Length);

        _snapshotCount.Add(1);
        var sw = Stopwatch.StartNew();

        var conidsStr = string.Join(",", conids);
        var fieldsStr = string.Join(",", fields);

        var rawSnapshots = await _api.GetSnapshotAsync(conidsStr, fieldsStr, cancellationToken);

        var preflightNeeded = GetConidsNeedingPreflight(rawSnapshots);

        activity?.SetTag(LogFields.PreflightNeeded, preflightNeeded.Count > 0);

        if (preflightNeeded.Count > 0)
        {
            _preflightCount.Add(1);
            foreach (var conid in preflightNeeded)
            {
                _preflightCache.Set(conid, true, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _options.PreflightCacheDuration,
                });
            }

            var preflightConidsStr = string.Join(",", preflightNeeded);
            LogPreflightRetry(preflightConidsStr, _preflightDelayMs);

            await Task.Delay(_preflightDelayMs, cancellationToken);

            rawSnapshots = await _api.GetSnapshotAsync(conidsStr, fieldsStr, cancellationToken);
        }

        _snapshotDuration.Record(sw.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("preflight", preflightNeeded.Count > 0));

        return rawSnapshots.Select(MapSnapshot).ToList();
    }

    /// <inheritdoc />
    public async Task<HistoricalDataResponse> GetHistoryAsync(int conid, string period, string bar,
        bool? outsideRth = null, CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.MarketData.History");
        activity?.SetTag(LogFields.Conid, conid);
        activity?.SetTag("period", period);
        activity?.SetTag("bar", bar);

        _historyCount.Add(1);
        var sw = Stopwatch.StartNew();
        var result = await _api.GetHistoryAsync(conid.ToString(CultureInfo.InvariantCulture), period, bar, outsideRth, cancellationToken);
        _historyDuration.Record(sw.Elapsed.TotalMilliseconds);
        return result;
    }

    /// <inheritdoc />
    public async Task<MarketDataSnapshot> GetRegulatorySnapshotAsync(int conid,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.MarketData.RegulatorySnapshot");
        activity?.SetTag(LogFields.Conid, conid);

        LogRegulatorySnapshotWarning(conid);

        var raw = await _api.GetRegulatorySnapshotAsync(conid, cancellationToken);
        return MapSnapshot(raw);
    }

    /// <inheritdoc />
    public async Task<UnsubscribeResponse> UnsubscribeAsync(int conid,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.MarketData.Unsubscribe");
        activity?.SetTag(LogFields.Conid, conid);
        return await _api.UnsubscribeAsync(new UnsubscribeRequest(conid), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UnsubscribeAllResponse> UnsubscribeAllAsync(
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.MarketData.UnsubscribeAll");
        return await _api.UnsubscribeAllAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ScannerResponse> RunScannerAsync(ScannerRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.MarketData.RunScanner");
        return await _api.RunScannerAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ScannerParameters> GetScannerParametersAsync(
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.MarketData.GetScannerParams");
        return await _api.GetScannerParametersAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<HmdsScannerResponse> RunHmdsScannerAsync(HmdsScannerRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.MarketData.RunHmdsScanner");
        return await _api.RunHmdsScannerAsync(request, cancellationToken);
    }

    /// <summary>
    /// Disposes the pre-flight memory cache.
    /// </summary>
    public void Dispose()
    {
        _preflightCache.Dispose();
        GC.SuppressFinalize(this);
    }

    private List<int> GetConidsNeedingPreflight(List<MarketDataSnapshotRaw> snapshots)
    {
        var result = new List<int>();

        foreach (var snapshot in snapshots)
        {
            if (_preflightCache.TryGetValue(snapshot.Conid, out _))
            {
                continue;
            }

            if (!HasFieldData(snapshot))
            {
                result.Add(snapshot.Conid);
            }
        }

        return result;
    }

    private static bool HasFieldData(MarketDataSnapshotRaw snapshot)
    {
        if (snapshot.Fields == null || snapshot.Fields.Count == 0)
        {
            return false;
        }

        foreach (var key in snapshot.Fields.Keys)
        {
            if (!_nonDataKeys.Contains(key))
            {
                return true;
            }
        }

        return false;
    }

    private static MarketDataSnapshot MapSnapshot(MarketDataSnapshotRaw raw)
    {
        var allFields = BuildAllFieldsDictionary(raw);

        return new MarketDataSnapshot
        {
            Conid = raw.Conid,
            Updated = raw.Updated,
            MarketDataAvailability = raw.MarketDataAvailability ?? TryGetField(allFields, "6509"),
            LastPrice = TryGetField(allFields, "31"),
            BidPrice = TryGetField(allFields, "84"),
            AskPrice = TryGetField(allFields, "86"),
            AskSize = TryGetField(allFields, "85"),
            BidSize = TryGetField(allFields, "88"),
            LastSize = TryGetField(allFields, "7059"),
            High = TryGetField(allFields, "70"),
            Low = TryGetField(allFields, "71"),
            Open = TryGetField(allFields, "7295"),
            Close = TryGetField(allFields, "7296"),
            PriorClose = TryGetField(allFields, "7741"),
            Volume = TryGetField(allFields, "87"),
            VolumeLong = TryGetField(allFields, "7762"),
            Change = TryGetField(allFields, "82"),
            ChangePercent = TryGetField(allFields, "83"),
            MarketValue = TryGetField(allFields, "73"),
            AvgPrice = TryGetField(allFields, "74"),
            UnrealizedPnl = TryGetField(allFields, "75"),
            RealizedPnl = TryGetField(allFields, "79"),
            DailyPnl = TryGetField(allFields, "78"),
            ImpliedVolatility = TryGetField(allFields, "7633"),
            AllFields = allFields,
        };
    }

    private static Dictionary<string, string> BuildAllFieldsDictionary(MarketDataSnapshotRaw raw)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);

        if (raw.MarketDataAvailability != null)
        {
            dict["6509"] = raw.MarketDataAvailability;
        }

        if (raw.Fields == null)
        {
            return dict;
        }

        foreach (var kvp in raw.Fields)
        {
            if (_nonDataKeys.Contains(kvp.Key))
            {
                continue;
            }

            dict[kvp.Key] = kvp.Value.ValueKind == JsonValueKind.String
                ? kvp.Value.GetString()!
                : kvp.Value.GetRawText();
        }

        return dict;
    }

    private static string? TryGetField(Dictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) ? value : null;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pre-flight needed for conids: {Conids}. Waiting {DelayMs}ms before retry.")]
    private partial void LogPreflightRetry(string conids, int delayMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Regulatory snapshot requested for conid {Conid}. This incurs a $0.01 USD fee per request.")]
    private partial void LogRegulatorySnapshotWarning(int conid);
}
