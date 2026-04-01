using Refit;

namespace IbkrConduit.Contracts;

/// <summary>
/// Refit interface for IBKR contract lookup endpoints.
/// </summary>
public interface IIbkrContractApi
{
    /// <summary>
    /// Searches for contracts by symbol name.
    /// </summary>
    [Get("/v1/api/iserver/secdef/search")]
    Task<List<ContractSearchResult>> SearchBySymbolAsync([Query] string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves detailed contract information by contract ID.
    /// </summary>
    [Get("/v1/api/iserver/contract/{conid}/info")]
    Task<ContractDetails> GetContractDetailsAsync(string conid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves security definition info for derivatives (options, warrants, futures).
    /// </summary>
    [Get("/v1/api/iserver/secdef/info")]
    Task<List<SecurityDefinitionInfo>> GetSecurityDefinitionInfoAsync(
        [Query] string conid,
        [Query] string sectype,
        [Query] string month,
        [Query] string? exchange = null,
        [Query] string? strike = null,
        [Query] string? right = null,
        [Query] string? issuerId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves available option strike prices.
    /// </summary>
    [Get("/v1/api/iserver/secdef/strikes")]
    Task<OptionStrikes> GetOptionStrikesAsync(
        [Query] string conid,
        [Query] string sectype,
        [Query] string month,
        [Query] string? exchange = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves trading rules for a contract.
    /// </summary>
    [Post("/v1/api/iserver/contract/rules")]
    Task<TradingRules> GetTradingRulesAsync(
        [Body] TradingRulesRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves security definitions by contract IDs.
    /// </summary>
    [Get("/v1/api/trsrv/secdef")]
    Task<SecurityDefinitionResponse> GetSecurityDefinitionsByConidAsync(
        [Query] string conids,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all contract IDs for a given exchange.
    /// </summary>
    [Get("/v1/api/trsrv/all-conids")]
    Task<List<ExchangeConid>> GetAllConidsByExchangeAsync(
        [Query] string exchange,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves futures contracts by symbol.
    /// </summary>
    [Get("/v1/api/trsrv/futures")]
    Task<Dictionary<string, List<FutureContract>>> GetFuturesBySymbolAsync(
        [Query] string symbols,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves stock contracts by symbol.
    /// </summary>
    [Get("/v1/api/trsrv/stocks")]
    Task<Dictionary<string, List<StockContract>>> GetStocksBySymbolAsync(
        [Query] string symbols,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves trading schedule for a contract.
    /// </summary>
    [Get("/v1/api/trsrv/secdef/schedule")]
    Task<List<TradingSchedule>> GetTradingScheduleAsync(
        [Query] string assetClass,
        [Query] string symbol,
        [Query] string conid,
        [Query] string? exchange = null,
        [Query] string? exchangeFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves currency pairs for a given currency.
    /// </summary>
    [Get("/v1/api/iserver/currency/pairs")]
    Task<Dictionary<string, List<CurrencyPair>>> GetCurrencyPairsAsync(
        [Query] string currency,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the exchange rate between two currencies.
    /// </summary>
    [Get("/v1/api/iserver/exchangerate")]
    Task<ExchangeRateResponse> GetExchangeRateAsync(
        [Query] string source,
        [Query] string target,
        CancellationToken cancellationToken = default);
}
