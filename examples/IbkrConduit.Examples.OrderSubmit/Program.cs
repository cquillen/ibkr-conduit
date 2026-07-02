using System.Globalization;

namespace IbkrConduit.Examples.OrderSubmit;

/// <summary>Parsed, validated order details from the command line.</summary>
internal sealed record ParsedOrder(
    string Side, decimal Quantity, string Symbol,
    string OrderType, decimal? Price, string Tif,
    string? OrderRef, string? Account, bool AutoConfirm, bool WhatIf);

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Any(a => a is "-h" or "--help" or "/?"))
        {
            PrintHelp();
            return 0;
        }

        if (!TryParseArgs(args, out _, out var error))
        {
            Console.Error.WriteLine(error);
            return 2;
        }

        // Submission wiring is added in a later task.
        Console.WriteLine("OrderSubmit: arguments parsed. Submission wiring not yet implemented.");
        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Usage: ibkr-conduit-submit <BUY|SELL> <QTY> <SYMBOL> [--market | --limit <price>] [options]");
        Console.WriteLine();
        Console.WriteLine("Submits a single US-stock order to the paper account.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --market               Market order (default).");
        Console.WriteLine("  --limit <price>        Limit order at <price>. Exclusive with --market.");
        Console.WriteLine("  --tif <DAY|GTC|IOC>    Time in force (default DAY).");
        Console.WriteLine("  --order-ref <str>      Customer order id (cOID). Auto-generated if omitted.");
        Console.WriteLine("  --account <id>         Account to submit under (default: first discovered).");
        Console.WriteLine("  --yes                  Auto-confirm IBKR order warnings (no prompt).");
        Console.WriteLine("  --what-if              Preview commission/margin; do not submit.");
        Console.WriteLine("  -h, --help, /?         Show this help and exit.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ibkr-conduit-submit BUY 100 AAPL");
        Console.WriteLine("  ibkr-conduit-submit BUY 1 QQQ --limit 500");
        Console.WriteLine("  ibkr-conduit-submit SELL 2 SPY --tif GTC --yes");
    }

    /// <summary>
    /// Parses the submit CLI into a validated <see cref="ParsedOrder"/>. Returns false with a
    /// non-empty <paramref name="error"/> on bad input. Does no I/O and generates no cOID —
    /// callers substitute a generated cOID when <see cref="ParsedOrder.OrderRef"/> is null.
    /// </summary>
    internal static bool TryParseArgs(string[] args, out ParsedOrder? order, out string error)
    {
        order = null;
        error = string.Empty;

        var positionals = new List<string>();
        string? tif = null;
        string? orderRef = null;
        string? account = null;
        var market = false;
        decimal? limitPrice = null;
        var autoConfirm = false;
        var whatIf = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--market":
                    market = true;
                    continue;

                case "--limit":
                    if (i + 1 >= args.Length)
                    {
                        error = "Error: --limit requires a price (e.g. --limit 185.00).";
                        return false;
                    }

                    if (!decimal.TryParse(args[i + 1], NumberStyles.Number, CultureInfo.InvariantCulture, out var price) || price <= 0)
                    {
                        error = $"Error: --limit price must be a positive number (got '{args[i + 1]}').";
                        return false;
                    }

                    limitPrice = price;
                    i++;
                    continue;

                case "--tif":
                    if (i + 1 >= args.Length)
                    {
                        error = "Error: --tif requires a value (DAY, GTC, IOC).";
                        return false;
                    }

                    var tifValue = args[i + 1].ToUpperInvariant();
                    if (tifValue is not ("DAY" or "GTC" or "IOC"))
                    {
                        error = $"Error: --tif must be one of DAY, GTC, IOC (got '{args[i + 1]}').";
                        return false;
                    }

                    tif = tifValue;
                    i++;
                    continue;

                case "--order-ref":
                    if (i + 1 >= args.Length)
                    {
                        error = "Error: --order-ref requires a value.";
                        return false;
                    }

                    orderRef = args[i + 1];
                    i++;
                    continue;

                case "--account":
                    if (i + 1 >= args.Length)
                    {
                        error = "Error: --account requires a value.";
                        return false;
                    }

                    account = args[i + 1];
                    i++;
                    continue;

                case "--yes":
                    autoConfirm = true;
                    continue;

                case "--what-if":
                    whatIf = true;
                    continue;

                default:
                    if (arg.StartsWith("--", StringComparison.Ordinal))
                    {
                        error = $"Error: unknown flag '{arg}'.";
                        return false;
                    }

                    positionals.Add(arg);
                    continue;
            }
        }

        if (market && limitPrice is not null)
        {
            error = "Error: --market and --limit are mutually exclusive.";
            return false;
        }

        if (positionals.Count != 3)
        {
            error = "Error: expected exactly 3 positional arguments: <BUY|SELL> <QTY> <SYMBOL>.";
            return false;
        }

        var side = positionals[0].ToUpperInvariant();
        if (side is not ("BUY" or "SELL"))
        {
            error = $"Error: side must be BUY or SELL (got '{positionals[0]}').";
            return false;
        }

        if (!decimal.TryParse(positionals[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity) || quantity <= 0)
        {
            error = $"Error: quantity must be a positive number (got '{positionals[1]}').";
            return false;
        }

        var symbol = positionals[2].ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(symbol))
        {
            error = "Error: symbol must not be empty.";
            return false;
        }

        var orderType = limitPrice is not null ? "LMT" : "MKT";
        order = new ParsedOrder(
            side, quantity, symbol, orderType, limitPrice, tif ?? "DAY",
            orderRef, account, autoConfirm, whatIf);
        return true;
    }
}
