using System.Globalization;
using System.Text.Json;
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
        _preflightCache = new MemoryCache(new MemoryCacheOptions());
    }

    /// <inheritdoc />
    public async Task<List<MarketDataSnapshot>> GetSnapshotAsync(int[] conids, string[] fields,
        CancellationToken cancellationToken = default)
    {
        var conidsStr = string.Join(",", conids);
        var fieldsStr = string.Join(",", fields);

        var rawSnapshots = await _api.GetSnapshotAsync(conidsStr, fieldsStr, cancellationToken);

        var preflightNeeded = GetConidsNeedingPreflight(rawSnapshots);

        if (preflightNeeded.Count > 0)
        {
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

        return rawSnapshots.Select(MapSnapshot).ToList();
    }

    /// <inheritdoc />
    public Task<HistoricalDataResponse> GetHistoryAsync(int conid, string period, string bar,
        bool? outsideRth = null, CancellationToken cancellationToken = default) =>
        _api.GetHistoryAsync(conid.ToString(CultureInfo.InvariantCulture), period, bar, outsideRth, cancellationToken);

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
}
