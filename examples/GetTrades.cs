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
using var credentials = OAuthCredentialsFactory.FromEnvironment();

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddIbkrClient(credentials);

await using var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IIbkrClient>();

// Retrieve completed trades for the current session
var trades = await client.Orders.GetTradesAsync();

if (trades.Count == 0)
{
    Console.WriteLine("No trades in current session.");
    return;
}

Console.WriteLine($"{trades.Count} trade(s):");
Console.WriteLine(
    string.Format(CultureInfo.InvariantCulture,
        "{0,-14} {1,-6} {2,-6} {3,8} {4,12} {5,-10}",
        "Execution ID", "Symbol", "Side", "Size", "Price", "Submitter"));
Console.WriteLine(new string('-', 60));

foreach (var t in trades)
{
    Console.WriteLine(
        string.Format(CultureInfo.InvariantCulture,
            "{0,-14} {1,-6} {2,-6} {3,8:N0} {4,12:N2} {5,-10}",
            t.ExecutionId.Length > 14 ? t.ExecutionId[..14] : t.ExecutionId,
            t.Symbol, t.Side, t.Size, t.Price, t.Submitter));
}
