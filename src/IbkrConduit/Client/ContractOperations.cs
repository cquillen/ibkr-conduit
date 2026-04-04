using IbkrConduit.Contracts;
using IbkrConduit.Diagnostics;
using IbkrConduit.Errors;
using IbkrConduit.Session;

namespace IbkrConduit.Client;

/// <summary>
/// Contract lookup operations that delegate to the underlying Refit API.
/// </summary>
public class ContractOperations : IContractOperations
{
    private readonly IIbkrContractApi _api;
    private readonly IbkrClientOptions _options;

    /// <summary>
    /// Creates a new <see cref="ContractOperations"/> instance.
    /// </summary>
    /// <param name="api">The Refit contract API client.</param>
    /// <param name="options">Client options.</param>
    public ContractOperations(IIbkrContractApi api, IbkrClientOptions options)
    {
        _api = api;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<Result<List<ContractSearchResult>>> SearchBySymbolAsync(
        string symbol, CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.SearchBySymbol");
        activity?.SetTag(LogFields.Symbol, symbol);
        var response = await _api.SearchBySymbolAsync(symbol, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
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
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<List<SecurityDefinitionInfo>>> GetSecurityDefinitionInfoAsync(
        string conid, string sectype, string month,
        string? exchange = null, string? strike = null, string? right = null, string? issuerId = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetSecDefInfo");
        activity?.SetTag(LogFields.Conid, conid);
        var response = await _api.GetSecurityDefinitionInfoAsync(conid, sectype, month, exchange, strike, right, issuerId, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<OptionStrikes>> GetOptionStrikesAsync(
        string conid, string sectype, string month,
        string? exchange = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetOptionStrikes");
        activity?.SetTag(LogFields.Conid, conid);
        var response = await _api.GetOptionStrikesAsync(conid, sectype, month, exchange, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
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
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<List<ExchangeConid>>> GetAllConidsByExchangeAsync(
        string exchange,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetAllConidsByExchange");
        activity?.SetTag("ibkr.exchange", exchange);
        var response = await _api.GetAllConidsByExchangeAsync(exchange, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<Dictionary<string, List<FutureContract>>>> GetFuturesBySymbolAsync(
        string symbols,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetFuturesBySymbol");
        activity?.SetTag(LogFields.Symbol, symbols);
        var response = await _api.GetFuturesBySymbolAsync(symbols, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
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
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }
}
