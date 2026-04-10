#!/usr/bin/env dotnet
#:project ../src/IbkrConduit/IbkrConduit.csproj
#:package Microsoft.Extensions.Logging.Console
#:property PublishAot=false

using System.Globalization;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Flex;
using IbkrConduit.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Usage:
//   dotnet run examples/FlexReports.cs -- <cash-query-id> <trades-query-id> [from-date] [to-date]
//
// Arguments:
//   cash-query-id    Flex Query ID for Cash Transactions (from IBKR portal)
//   trades-query-id  Flex Query ID for Trade Confirmations (from IBKR portal)
//   from-date        Optional start date for trade confirmations (yyyy-MM-dd, default: 30 days ago)
//   to-date          Optional end date for trade confirmations (yyyy-MM-dd, default: today)
//
// Environment variables (set via: source tools/set-e2e-env.sh):
//   IBKR_CONSUMER_KEY, IBKR_ACCESS_TOKEN, IBKR_ACCESS_TOKEN_SECRET,
//   IBKR_SIGNATURE_KEY, IBKR_ENCRYPTION_KEY, IBKR_DH_PRIME, IBKR_FLEX_TOKEN

var verbose = args.Any(a => a is "--verbose" or "-v");
var logLevel = verbose ? LogLevel.Debug : LogLevel.Warning;
args = args.Where(a => a is not "--verbose" and not "-v").ToArray();

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: dotnet run examples/FlexReports.cs -- <cash-query-id> <trades-query-id> [from-date] [to-date]");
    return 1;
}

var cashQueryId = args[0];
var tradesQueryId = args[1];

var fromDate = args.Length >= 3
    ? DateOnly.Parse(args[2], CultureInfo.InvariantCulture)
    : DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));

var toDate = args.Length >= 4
    ? DateOnly.Parse(args[3], CultureInfo.InvariantCulture)
    : DateOnly.FromDateTime(DateTime.UtcNow);

var flexToken = Environment.GetEnvironmentVariable("IBKR_FLEX_TOKEN")
    ?? throw new InvalidOperationException("IBKR_FLEX_TOKEN environment variable is required.");

using var credentials = OAuthCredentialsFactory.FromEnvironment();

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(logLevel));
services.AddIbkrClient(opts =>
{
    opts.Credentials = credentials;
    opts.FlexToken = flexToken;
    opts.FlexQueries = new FlexQueryOptions
    {
        CashTransactionsQueryId = cashQueryId,
        TradeConfirmationsQueryId = tradesQueryId,
    };
});

await using var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IIbkrClient>();

// ──────────────────────────────────────────────
// 1. Cash Transactions (uses template period — no date override)
// ──────────────────────────────────────────────

Console.WriteLine($"Fetching cash transactions (query {cashQueryId})...");
var cashResult = (await client.Flex.GetCashTransactionsAsync()).EnsureSuccess().Value;

Console.WriteLine($"  Query:      {cashResult.QueryName}");
Console.WriteLine($"  Generated:  {cashResult.GeneratedAt}");
Console.WriteLine($"  Range:      {cashResult.FromDate} → {cashResult.ToDate}");
Console.WriteLine();

if (cashResult.CashTransactions.Count == 0)
{
    Console.WriteLine("  No cash transactions found.");
}
else
{
    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
        "  {0,-12} {1,-35} {2,-30} {3,14}",
        "Date", "Type", "Description", "Amount"));
    Console.WriteLine($"  {new string('-', 95)}");

    foreach (var tx in cashResult.CashTransactions)
    {
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "  {0,-12} {1,-35} {2,-30} {3,14:N2}",
            tx.SettleDate, tx.Type, Truncate(tx.Description, 30), tx.Amount));
    }
}

// ──────────────────────────────────────────────
// 2. Trade Confirmations (with date range)
// ──────────────────────────────────────────────

Console.WriteLine();
Console.WriteLine($"Fetching trade confirmations (query {tradesQueryId}, {fromDate} → {toDate})...");
var tradesResult = (await client.Flex.GetTradeConfirmationsAsync(fromDate, toDate)).EnsureSuccess().Value;

Console.WriteLine($"  Query:      {tradesResult.QueryName}");
Console.WriteLine($"  Generated:  {tradesResult.GeneratedAt}");
Console.WriteLine($"  Range:      {tradesResult.FromDate} → {tradesResult.ToDate}");
Console.WriteLine();

// Trade Confirmations (individual fills)
if (tradesResult.TradeConfirmations.Count == 0)
{
    Console.WriteLine("  No trade confirmations found.");
}
else
{
    Console.WriteLine($"  {tradesResult.TradeConfirmations.Count} trade confirmation(s):");
    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
        "  {0,-12} {1,-6} {2,-5} {3,8} {4,12} {5,12} {6,10}",
        "Date", "Symbol", "Side", "Qty", "Price", "Proceeds", "Commission"));
    Console.WriteLine($"  {new string('-', 70)}");

    foreach (var tc in tradesResult.TradeConfirmations)
    {
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "  {0,-12} {1,-6} {2,-5} {3,8:N0} {4,12:N2} {5,12:N2} {6,10:N2}",
            tc.TradeDate, tc.Symbol, tc.BuySell, tc.Quantity, tc.Price, tc.Proceeds, tc.Commission));
    }
}

// Symbol Summaries (per-symbol aggregates)
Console.WriteLine();
if (tradesResult.SymbolSummaries.Count > 0)
{
    Console.WriteLine($"  {tradesResult.SymbolSummaries.Count} symbol summary(s):");
    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
        "  {0,-6} {1,-5} {2,8} {3,12} {4,12} {5,10}",
        "Symbol", "Side", "Qty", "Avg Price", "Net Cash", "Commission"));
    Console.WriteLine($"  {new string('-', 58)}");

    foreach (var ss in tradesResult.SymbolSummaries)
    {
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "  {0,-6} {1,-5} {2,8:N0} {3,12:N2} {4,12:N2} {5,10:N2}",
            ss.Symbol, ss.BuySell, ss.Quantity, ss.Price, ss.NetCash, ss.Commission));
    }
}

// Orders
Console.WriteLine();
Console.WriteLine($"  {tradesResult.Orders.Count} order(s).");

Console.WriteLine();
Console.WriteLine("Done.");
return 0;

static string Truncate(string value, int maxLength) =>
    value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength - 3), "...");
