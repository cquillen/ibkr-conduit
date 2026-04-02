#!/usr/bin/env dotnet
#:project ../src/IbkrConduit/IbkrConduit.csproj
#:package Microsoft.Extensions.Logging.Console
#:property PublishAot=false

using System.Globalization;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using IbkrConduit.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Load credentials from environment variables.
// See tools/set-e2e-env.sh for the required variables.
// Requires IBKR_FLEX_TOKEN and IBKR_FLEX_QUERY_ID to be set.
using var credentials = OAuthCredentialsFactory.FromEnvironment();

var flexToken = Environment.GetEnvironmentVariable("IBKR_FLEX_TOKEN")
    ?? throw new InvalidOperationException("IBKR_FLEX_TOKEN environment variable is required.");
var flexQueryId = Environment.GetEnvironmentVariable("IBKR_FLEX_QUERY_ID")
    ?? throw new InvalidOperationException("IBKR_FLEX_QUERY_ID environment variable is required.");

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddIbkrClient(credentials, new IbkrClientOptions { FlexToken = flexToken });

await using var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IIbkrClient>();

// Execute the Flex Query to retrieve trades.
// Usage: dotnet run GetTrades.cs                       (uses query template default period)
//        dotnet run GetTrades.cs -- 20260301 20260402   (custom date range: fromDate toDate)
IbkrConduit.Flex.FlexQueryResult result;
if (args.Length >= 2)
{
    Console.WriteLine($"Executing Flex Query {flexQueryId} ({args[0]} to {args[1]})...");
    result = await client.Flex.ExecuteQueryAsync(flexQueryId, args[0], args[1]);
}
else
{
    Console.WriteLine($"Executing Flex Query {flexQueryId} (default period)...");
    result = await client.Flex.ExecuteQueryAsync(flexQueryId);
}

if (result.Trades.Count == 0)
{
    Console.WriteLine("No trades found.");
    return;
}

Console.WriteLine($"\n{result.Trades.Count} trade(s):");
Console.WriteLine(
    string.Format(CultureInfo.InvariantCulture,
        "{0,-10} {1,-6} {2,-6} {3,8} {4,12} {5,12} {6,12}",
        "Date", "Symbol", "Side", "Qty", "Price", "Proceeds", "Commission"));
Console.WriteLine(new string('-', 70));

foreach (var t in result.Trades)
{
    Console.WriteLine(
        string.Format(CultureInfo.InvariantCulture,
            "{0,-10} {1,-6} {2,-6} {3,8:N0} {4,12:N2} {5,12:N2} {6,12:N2}",
            t.TradeDate, t.Symbol, t.Side, t.Quantity, t.Price, t.Proceeds, t.Commission));
}
