#!/usr/bin/env dotnet
#:project ../src/IbkrConduit/IbkrConduit.csproj
#:package Microsoft.Extensions.Logging.Console
#:property PublishAot=false

using System.Globalization;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Load credentials from environment variables.
// See tools/set-e2e-env.sh for the required variables.
// Requires IBKR_FLEX_TOKEN and IBKR_FLEX_TRADE_CONFIRMATIONS_QUERY_ID to be set.
using var credentials = OAuthCredentialsFactory.FromEnvironment();

var flexToken = Environment.GetEnvironmentVariable("IBKR_FLEX_TOKEN")
    ?? throw new InvalidOperationException("IBKR_FLEX_TOKEN environment variable is required.");
var tradeConfirmationsQueryId = Environment.GetEnvironmentVariable("IBKR_FLEX_TRADE_CONFIRMATIONS_QUERY_ID")
    ?? throw new InvalidOperationException("IBKR_FLEX_TRADE_CONFIRMATIONS_QUERY_ID environment variable is required.");

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddIbkrClient(opts =>
{
    opts.Credentials = credentials;
    opts.FlexToken = flexToken;
    opts.FlexQueries = new IbkrConduit.Flex.FlexQueryOptions
    {
        TradeConfirmationsQueryId = tradeConfirmationsQueryId,
    };
});

await using var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IIbkrClient>();

// Execute the Trade Confirmations Flex Query.
// Usage: dotnet run GetTrades.cs                       (uses today as both fromDate and toDate)
//        dotnet run GetTrades.cs -- 2026-03-01 2026-04-02   (custom date range: fromDate toDate)
DateOnly fromDate;
DateOnly toDate;
if (args.Length >= 2)
{
    fromDate = DateOnly.Parse(args[0], CultureInfo.InvariantCulture);
    toDate = DateOnly.Parse(args[1], CultureInfo.InvariantCulture);
}
else
{
    fromDate = toDate = DateOnly.FromDateTime(DateTime.UtcNow);
}

Console.WriteLine($"Executing Trade Confirmations Flex Query {tradeConfirmationsQueryId} ({fromDate} to {toDate})...");
var result = (await client.Flex.GetTradeConfirmationsAsync(fromDate, toDate)).EnsureSuccess().Value;

if (result.TradeConfirmations.Count == 0)
{
    Console.WriteLine("No trade confirmations found.");
    return;
}

Console.WriteLine($"\n{result.TradeConfirmations.Count} trade confirmation(s):");
Console.WriteLine(
    string.Format(CultureInfo.InvariantCulture,
        "{0,-10} {1,-6} {2,-6} {3,8} {4,12} {5,12} {6,12}",
        "Date", "Symbol", "Side", "Qty", "Price", "Proceeds", "Commission"));
Console.WriteLine(new string('-', 70));

foreach (var t in result.TradeConfirmations)
{
    Console.WriteLine(
        string.Format(CultureInfo.InvariantCulture,
            "{0,-10} {1,-6} {2,-6} {3,8:N0} {4,12:N2} {5,12:N2} {6,12:N2}",
            t.TradeDate, t.Symbol, t.Side, t.Quantity, t.Price, t.Proceeds, t.Commission));
}
