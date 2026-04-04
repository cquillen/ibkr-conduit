using IbkrConduit.Contracts;
using IbkrConduit.Errors;

namespace IbkrConduit.Client;

/// <summary>
/// Contract lookup operations on the IBKR API.
/// </summary>
public interface IContractOperations
{
    /// <summary>
    /// Searches for contracts matching the given symbol.
    /// </summary>
    Task<Result<List<ContractSearchResult>>> SearchBySymbolAsync(
        string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves detailed contract information by contract ID.
    /// </summary>
    Task<Result<ContractDetails>> GetContractDetailsAsync(
        string conid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves security definition info for derivatives (options, warrants, futures).
    /// </summary>
    Task<Result<List<SecurityDefinitionInfo>>> GetSecurityDefinitionInfoAsync(
        string conid, string sectype, string month,
        string? exchange = null, string? strike = null, string? right = null, string? issuerId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves available option strike prices.
    /// </summary>
    Task<Result<OptionStrikes>> GetOptionStrikesAsync(
        string conid, string sectype, string month,
        string? exchange = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves trading rules for a contract.
    /// </summary>
    Task<Result<TradingRules>> GetTradingRulesAsync(
        TradingRulesRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves security definitions by contract IDs.
    /// </summary>
    Task<Result<SecurityDefinitionResponse>> GetSecurityDefinitionsByConidAsync(
        string conids,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all contract IDs for a given exchange.
    /// </summary>
    Task<Result<List<ExchangeConid>>> GetAllConidsByExchangeAsync(
        string exchange,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves futures contracts by symbol.
    /// </summary>
    Task<Result<Dictionary<string, List<FutureContract>>>> GetFuturesBySymbolAsync(
        string symbols,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves stock contracts by symbol.
    /// </summary>
    Task<Result<Dictionary<string, List<StockContract>>>> GetStocksBySymbolAsync(
        string symbols,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves trading schedule for a contract.
    /// </summary>
    Task<Result<List<TradingSchedule>>> GetTradingScheduleAsync(
        string assetClass, string symbol, string conid,
        string? exchange = null, string? exchangeFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves currency pairs for a given currency.
    /// </summary>
    Task<Result<Dictionary<string, List<CurrencyPair>>>> GetCurrencyPairsAsync(
        string currency,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the exchange rate between two currencies.
    /// </summary>
    Task<Result<ExchangeRateResponse>> GetExchangeRateAsync(
        string source, string target,
        CancellationToken cancellationToken = default);
}
