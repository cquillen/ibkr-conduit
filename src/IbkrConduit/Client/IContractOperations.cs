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
    /// <param name="symbol">The symbol to search for.</param>
    /// <param name="secType">Security type filter.</param>
    /// <param name="name">Search by name instead of symbol.</param>
    /// <param name="more">Request more results.</param>
    /// <param name="fund">Filter to funds only.</param>
    /// <param name="fundFamilyConidEx">Fund family conid filter.</param>
    /// <param name="pattern">Enable pattern matching.</param>
    /// <param name="referrer">Referrer context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<List<ContractSearchResult>>> SearchBySymbolAsync(
        string symbol,
        SecurityType? secType = null, bool? name = null, bool? more = null,
        bool? fund = null, string? fundFamilyConidEx = null,
        bool? pattern = null, string? referrer = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves detailed contract information by contract ID.
    /// </summary>
    Task<Result<ContractDetails>> GetContractDetailsAsync(
        string conid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves security definition info for derivatives (options, warrants, futures).
    /// </summary>
    Task<Result<List<SecurityDefinitionInfo>>> GetSecurityDefinitionInfoAsync(
        string conid, SecurityType sectype, ExpiryMonth month,
        string? exchange = null, decimal? strike = null, OptionRight? right = null, string? issuerId = null,
        string? filters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves available option strike prices.
    /// </summary>
    Task<Result<OptionStrikes>> GetOptionStrikesAsync(
        string conid, SecurityType sectype, ExpiryMonth month,
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
    /// <param name="exchange">The exchange name.</param>
    /// <param name="assetClass">Optional asset class filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<List<ExchangeConid>>> GetAllConidsByExchangeAsync(
        string exchange, string? assetClass = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves futures contracts by symbol.
    /// </summary>
    /// <param name="symbols">The symbol(s) to look up.</param>
    /// <param name="exchange">Optional exchange filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<Dictionary<string, List<FutureContract>>>> GetFuturesBySymbolAsync(
        string symbols, string? exchange = null,
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

    /// <summary>
    /// Retrieves combined contract info and trading rules.
    /// </summary>
    Task<Result<ContractInfoAndRules>> GetContractInfoAndRulesAsync(
        string conid, bool? isBuy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves available algorithms for a contract.
    /// </summary>
    Task<Result<AlgoListResponse>> GetAlgosAsync(
        string conid, string? algos = null, int? addDescription = null, int? addParams = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves bond filter options.
    /// </summary>
    Task<Result<BondFilterResponse>> GetBondFiltersAsync(
        string symbol, string issuerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for contracts by symbol using a POST request body.
    /// </summary>
    Task<Result<List<ContractSearchResult>>> SearchBySymbolPostAsync(
        ContractSearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves trading schedule for a contract (new-style endpoint).
    /// </summary>
    Task<Result<TradingScheduleResponse>> GetTradingScheduleNewAsync(
        string conid, string? exchange = null,
        CancellationToken cancellationToken = default);
}
