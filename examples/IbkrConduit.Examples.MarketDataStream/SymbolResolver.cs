using IbkrConduit.Client;
using IbkrConduit.Contracts;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Examples.MarketDataStream;

/// <summary>
/// Resolves user-entered symbols (e.g. "AAPL", "EUR.USD") to IBKR conids.
/// US stocks use <see cref="IContractOperations.SearchBySymbolAsync"/>; forex
/// pairs (dot-syntax) use <see cref="IContractOperations.GetCurrencyPairsAsync"/>
/// because <c>SearchBySymbolAsync</c> only supports STK/IND/BOND.
/// </summary>
internal static class SymbolResolver
{
    // CA1848: static class cannot host [LoggerMessage] partial methods (no instance logger field).
    // LoggerMessage.Define satisfies the analyzer while keeping the delegates at class scope.
    private static readonly Action<ILogger, string, Exception?> _logNoContract =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, nameof(ResolveAsync)),
            "Could not resolve symbol '{Symbol}': no matching contract.");

    private static readonly Action<ILogger, string, string, Exception?> _logResolveError =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(2, nameof(ResolveAsync)),
            "Could not resolve symbol '{Symbol}': {Message}");

    /// <summary>A single resolved symbol with its IBKR conid.</summary>
    public sealed record ResolvedSymbol(string Symbol, int Conid);

    /// <summary>
    /// Resolves each input symbol. Logs a warning for any single failure and skips it.
    /// Throws <see cref="InvalidOperationException"/> only if zero symbols resolve.
    /// </summary>
    public static async Task<IReadOnlyList<ResolvedSymbol>> ResolveAsync(
        IIbkrClient client,
        IReadOnlyList<string> symbols,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var resolved = new List<ResolvedSymbol>(symbols.Count);
        foreach (var symbol in symbols)
        {
            try
            {
                var entry = symbol.Contains('.', StringComparison.Ordinal)
                    ? await ResolveForexAsync(client, symbol, cancellationToken)
                    : await ResolveStockAsync(client, symbol, cancellationToken);

                if (entry is not null)
                {
                    resolved.Add(entry);
                }
                else
                {
                    _logNoContract(logger, symbol, null);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logResolveError(logger, symbol, ex.Message, ex);
            }
        }

        if (resolved.Count == 0)
        {
            throw new InvalidOperationException(
                $"No symbols resolved. Tried: {string.Join(", ", symbols)}.");
        }

        return resolved;
    }

    private static async Task<ResolvedSymbol?> ResolveStockAsync(
        IIbkrClient client, string symbol, CancellationToken ct)
    {
        var result = await client.Contracts.SearchBySymbolAsync(
            symbol, secType: SecurityType.Stock, cancellationToken: ct);

        if (!result.IsSuccess || result.Value.Count == 0)
        {
            return null;
        }

        var first = result.Value[0];
        return new ResolvedSymbol(symbol, first.Conid);
    }

    private static async Task<ResolvedSymbol?> ResolveForexAsync(
        IIbkrClient client, string symbol, CancellationToken ct)
    {
        var upperSymbol = symbol.ToUpperInvariant();
        var parts = upperSymbol.Split('.', 2);
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
        {
            return null;
        }

        var baseCurrency = parts[0];
        var result = await client.Contracts.GetCurrencyPairsAsync(baseCurrency, ct);
        if (!result.IsSuccess)
        {
            return null;
        }

        // Result is keyed by the base currency we queried; values are CurrencyPair entries.
        if (!result.Value.TryGetValue(baseCurrency, out var pairs) || pairs is null)
        {
            return null;
        }

        var match = pairs.FirstOrDefault(
            p => string.Equals(p.Symbol, upperSymbol, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return null;
        }

        return new ResolvedSymbol(symbol, match.Conid);
    }
}
