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

var verbose = args.Any(a => a is "--verbose" or "-v");
var logLevel = verbose ? LogLevel.Debug : LogLevel.Warning;

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(logLevel));
services.AddIbkrClient(opts => opts.Credentials = credentials);

await using var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IIbkrClient>();

// Discover accounts
var accounts = (await client.Portfolio.GetAccountsAsync()).EnsureSuccess().Value;
if (accounts.Count == 0)
{
    Console.WriteLine("No accounts found.");
    return;
}

Console.WriteLine($"Found {accounts.Count} account(s):");
foreach (var account in accounts)
{
    Console.WriteLine($"  {account.Id} — {account.AccountTitle} ({account.Type})");
}

// Pull positions for the first account
var accountId = accounts[0].Id;
var positions = (await client.Portfolio.GetPositionsAsync(accountId)).EnsureSuccess().Value;

if (positions.Count == 0)
{
    Console.WriteLine($"\nNo positions in {accountId}.");
    return;
}

Console.WriteLine($"\nPositions in {accountId}:");
Console.WriteLine(
    string.Format(CultureInfo.InvariantCulture,
        "{0,-10} {1,8} {2,12} {3,14} {4,16}", "Symbol", "Qty", "Mkt Price", "Mkt Value", "Unrealized P&L"));
Console.WriteLine(new string('-', 64));

foreach (var p in positions)
{
    Console.WriteLine(
        string.Format(CultureInfo.InvariantCulture,
            "{0,-10} {1,8:N0} {2,12:N2} {3,14:N2} {4,16:N2}",
            p.Ticker ?? p.ContractDescription, p.Quantity, p.MarketPrice, p.MarketValue, p.UnrealizedPnl));
}
