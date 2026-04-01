using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using IbkrConduit.Contracts;
using IbkrConduit.Diagnostics;

namespace IbkrConduit.Client;

/// <summary>
/// Contract lookup operations that delegate to the underlying Refit API.
/// </summary>
[ExcludeFromCodeCoverage]
public class ContractOperations : IContractOperations
{
    private readonly IIbkrContractApi _api;

    /// <summary>
    /// Creates a new <see cref="ContractOperations"/> instance.
    /// </summary>
    /// <param name="api">The Refit contract API client.</param>
    public ContractOperations(IIbkrContractApi api) => _api = api;

    /// <inheritdoc />
    public async Task<List<ContractSearchResult>> SearchBySymbolAsync(
        string symbol, CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.SearchBySymbol");
        activity?.SetTag(LogFields.Symbol, symbol);
        return await _api.SearchBySymbolAsync(symbol, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ContractDetails> GetContractDetailsAsync(
        string conid, CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetDetails");
        activity?.SetTag(LogFields.Conid, conid);
        return await _api.GetContractDetailsAsync(conid, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<SecurityDefinitionInfo>> GetSecurityDefinitionInfoAsync(
        string conid, string sectype, string month,
        string? exchange = null, string? strike = null, string? right = null, string? issuerId = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetSecDefInfo");
        activity?.SetTag(LogFields.Conid, conid);
        return await _api.GetSecurityDefinitionInfoAsync(conid, sectype, month, exchange, strike, right, issuerId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OptionStrikes> GetOptionStrikesAsync(
        string conid, string sectype, string month,
        string? exchange = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetOptionStrikes");
        activity?.SetTag(LogFields.Conid, conid);
        return await _api.GetOptionStrikesAsync(conid, sectype, month, exchange, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TradingRules> GetTradingRulesAsync(
        TradingRulesRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetTradingRules");
        activity?.SetTag(LogFields.Conid, request.Conid.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return await _api.GetTradingRulesAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SecurityDefinitionResponse> GetSecurityDefinitionsByConidAsync(
        string conids,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetSecDefsByConid");
        return await _api.GetSecurityDefinitionsByConidAsync(conids, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<ExchangeConid>> GetAllConidsByExchangeAsync(
        string exchange,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetAllConidsByExchange");
        return await _api.GetAllConidsByExchangeAsync(exchange, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, List<FutureContract>>> GetFuturesBySymbolAsync(
        string symbols,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetFuturesBySymbol");
        activity?.SetTag(LogFields.Symbol, symbols);
        return await _api.GetFuturesBySymbolAsync(symbols, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, List<StockContract>>> GetStocksBySymbolAsync(
        string symbols,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetStocksBySymbol");
        activity?.SetTag(LogFields.Symbol, symbols);
        return await _api.GetStocksBySymbolAsync(symbols, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<TradingSchedule>> GetTradingScheduleAsync(
        string assetClass, string symbol, string conid,
        string? exchange = null, string? exchangeFilter = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetTradingSchedule");
        activity?.SetTag(LogFields.Symbol, symbol);
        activity?.SetTag(LogFields.Conid, conid);
        return await _api.GetTradingScheduleAsync(assetClass, symbol, conid, exchange, exchangeFilter, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, List<CurrencyPair>>> GetCurrencyPairsAsync(
        string currency,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetCurrencyPairs");
        return await _api.GetCurrencyPairsAsync(currency, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ExchangeRateResponse> GetExchangeRateAsync(
        string source, string target,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Contract.GetExchangeRate");
        return await _api.GetExchangeRateAsync(source, target, cancellationToken);
    }
}
