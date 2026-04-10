using IbkrConduit.Contracts;
using IbkrConduit.Diagnostics;
using IbkrConduit.Errors;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Client;

/// <summary>
/// Contract lookup operations that delegate to the underlying Refit API.
/// </summary>
internal partial class ContractOperations : IContractOperations
{
    private readonly IIbkrContractApi _api;
    private readonly IbkrClientOptions _options;
    private readonly ILogger<ContractOperations> _logger;

    /// <summary>
    /// Creates a new <see cref="ContractOperations"/> instance.
    /// </summary>
    /// <param name="api">The Refit contract API client.</param>
    /// <param name="options">Client options.</param>
    /// <param name="logger">Logger instance.</param>
    public ContractOperations(IIbkrContractApi api, IbkrClientOptions options, ILogger<ContractOperations> logger)
    {
        _api = api;
        _options = options;
        _logger = logger;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "{Operation} completed with status {StatusCode}")]
    private static partial void LogOperationCompleted(ILogger logger, string operation, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Operation} failed: {ErrorType} (status {StatusCode})")]
    private static partial void LogOperationFailed(ILogger logger, string operation, string errorType, int? statusCode);

    /// <inheritdoc />
    public async Task<Result<List<ContractSearchResult>>> SearchBySymbolAsync(
        string symbol,
        SecurityType? secType = null, bool? name = null, bool? more = null,
        bool? fund = null, string? fundFamilyConidEx = null,
        bool? pattern = null, string? referrer = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.SearchBySymbol");
        activity?.SetTag(LogFields.Symbol, symbol);
        var response = await _api.SearchBySymbolAsync(symbol, secType, name, more, fund, fundFamilyConidEx, pattern, referrer, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "SearchBySymbol");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<ContractDetails>> GetContractDetailsAsync(
        string conid, CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetDetails");
        activity?.SetTag(LogFields.Conid, conid);
        var response = await _api.GetContractDetailsAsync(conid, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetContractDetails");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<List<SecurityDefinitionInfo>>> GetSecurityDefinitionInfoAsync(
        string conid, SecurityType sectype, ExpiryMonth month,
        string? exchange = null, decimal? strike = null, OptionRight? right = null, string? issuerId = null,
        string? filters = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetSecDefInfo");
        activity?.SetTag(LogFields.Conid, conid);
        var strikeStr = strike?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var response = await _api.GetSecurityDefinitionInfoAsync(conid, sectype, month.ToString(), exchange, strikeStr, right, issuerId, filters, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetSecurityDefinitionInfo");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<OptionStrikes>> GetOptionStrikesAsync(
        string conid, SecurityType sectype, ExpiryMonth month,
        string? exchange = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetOptionStrikes");
        activity?.SetTag(LogFields.Conid, conid);
        var response = await _api.GetOptionStrikesAsync(conid, sectype, month.ToString(), exchange, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetOptionStrikes");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<TradingRules>> GetTradingRulesAsync(
        TradingRulesRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetTradingRules");
        activity?.SetTag(LogFields.Conid, request.Conid.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var response = await _api.GetTradingRulesAsync(request, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetTradingRules");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<SecurityDefinitionResponse>> GetSecurityDefinitionsByConidAsync(
        string conids,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetSecDefsByConid");
        activity?.SetTag(LogFields.Conid, conids);
        var response = await _api.GetSecurityDefinitionsByConidAsync(conids, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetSecurityDefinitionsByConid");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<List<ExchangeConid>>> GetAllConidsByExchangeAsync(
        string exchange, string? assetClass = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetAllConidsByExchange");
        activity?.SetTag("ibkr.exchange", exchange);
        var response = await _api.GetAllConidsByExchangeAsync(exchange, assetClass, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetAllConidsByExchange");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<Dictionary<string, List<FutureContract>>>> GetFuturesBySymbolAsync(
        string symbols, string? exchange = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetFuturesBySymbol");
        activity?.SetTag(LogFields.Symbol, symbols);
        var response = await _api.GetFuturesBySymbolAsync(symbols, exchange, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetFuturesBySymbol");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<Dictionary<string, List<StockContract>>>> GetStocksBySymbolAsync(
        string symbols,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetStocksBySymbol");
        activity?.SetTag(LogFields.Symbol, symbols);
        var response = await _api.GetStocksBySymbolAsync(symbols, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetStocksBySymbol");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<List<TradingSchedule>>> GetTradingScheduleAsync(
        string assetClass, string symbol, string conid,
        string? exchange = null, string? exchangeFilter = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetTradingSchedule");
        activity?.SetTag(LogFields.Symbol, symbol);
        activity?.SetTag(LogFields.Conid, conid);
        var response = await _api.GetTradingScheduleAsync(assetClass, symbol, conid, exchange, exchangeFilter, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetTradingSchedule");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<Dictionary<string, List<CurrencyPair>>>> GetCurrencyPairsAsync(
        string currency,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetCurrencyPairs");
        activity?.SetTag("ibkr.currency", currency);
        var response = await _api.GetCurrencyPairsAsync(currency, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetCurrencyPairs");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<ExchangeRateResponse>> GetExchangeRateAsync(
        string source, string target,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetExchangeRate");
        activity?.SetTag("ibkr.source_currency", source);
        activity?.SetTag("ibkr.target_currency", target);
        var response = await _api.GetExchangeRateAsync(source, target, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetExchangeRate");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<ContractInfoAndRules>> GetContractInfoAndRulesAsync(
        string conid, bool? isBuy = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetInfoAndRules");
        activity?.SetTag(LogFields.Conid, conid);
        var response = await _api.GetContractInfoAndRulesAsync(conid, isBuy, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetContractInfoAndRules");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<AlgoListResponse>> GetAlgosAsync(
        string conid, string? algos = null, int? addDescription = null, int? addParams = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetAlgos");
        activity?.SetTag(LogFields.Conid, conid);
        var response = await _api.GetAlgosAsync(conid, algos, addDescription, addParams, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetAlgos");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<BondFilterResponse>> GetBondFiltersAsync(
        string symbol, string issuerId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetBondFilters");
        activity?.SetTag(LogFields.Symbol, symbol);
        var response = await _api.GetBondFiltersAsync(symbol, issuerId, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetBondFilters");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<List<ContractSearchResult>>> SearchBySymbolPostAsync(
        ContractSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.SearchBySymbolPost");
        activity?.SetTag(LogFields.Symbol, request.Symbol);
        var response = await _api.SearchBySymbolPostAsync(request, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "SearchBySymbolPost");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<TradingScheduleResponse>> GetTradingScheduleNewAsync(
        string conid, string? exchange = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetTradingScheduleNew");
        activity?.SetTag(LogFields.Conid, conid);
        var response = await _api.GetTradingScheduleNewAsync(conid, exchange, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetTradingScheduleNew");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    private void LogResult<T>(Result<T> result, string operation)
    {
        if (result.IsSuccess)
        {
            LogOperationCompleted(_logger, operation, 200);
        }
        else
        {
            LogOperationFailed(_logger, operation, result.Error.GetType().Name, (int?)result.Error.StatusCode);
        }
    }
}
