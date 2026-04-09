using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Usage:
//   dotnet run --project tools/CaptureFlexQuery -- <queryId> [options]
//
// Options:
//   --from <yyyyMMdd>   Override query start date (must be used with --to)
//   --to <yyyyMMdd>     Override query end date (must be used with --from)
//   --output <path>     Output file path (default: recordings/flex/{timestamp}-{queryId}.xml)
//
// Examples:
//   dotnet run --project tools/CaptureFlexQuery -- 1464458
//   dotnet run --project tools/CaptureFlexQuery -- 1464458 --from 20260101 --to 20260409
//   dotnet run --project tools/CaptureFlexQuery -- 1464458 --output /tmp/result.xml

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

var queryId = args[0];
string? fromDate = null;
string? toDate = null;
string? outputPathArg = null;
var pollTimeoutSeconds = 60;

for (var i = 1; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--from" when i + 1 < args.Length:
            fromDate = args[++i];
            break;
        case "--to" when i + 1 < args.Length:
            toDate = args[++i];
            break;
        case "--output" when i + 1 < args.Length:
            outputPathArg = args[++i];
            break;
        case "--poll-timeout" when i + 1 < args.Length:
            if (!int.TryParse(args[++i], out pollTimeoutSeconds) || pollTimeoutSeconds <= 0)
            {
                Console.Error.WriteLine($"Invalid --poll-timeout value '{args[i]}'. Expected positive integer seconds.");
                return 1;
            }

            break;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            PrintUsage();
            return 1;
    }
}

if ((fromDate is null) != (toDate is null))
{
    Console.Error.WriteLine("--from and --to must be used together.");
    return 1;
}

if (fromDate is not null && !IsValidFlexDate(fromDate))
{
    Console.Error.WriteLine($"Invalid --from date '{fromDate}'. Expected yyyyMMdd format.");
    return 1;
}

if (toDate is not null && !IsValidFlexDate(toDate))
{
    Console.Error.WriteLine($"Invalid --to date '{toDate}'. Expected yyyyMMdd format.");
    return 1;
}

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var dateSuffix = fromDate is not null ? $"-{fromDate}-{toDate}" : string.Empty;
var outputPath = outputPathArg is not null
    ? Path.GetFullPath(outputPathArg)
    : Path.Combine(repoRoot, "recordings", "flex",
        $"{DateTime.UtcNow:yyyy-MM-ddTHHmmss}-{queryId}{dateSuffix}.xml");

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

Console.WriteLine($"Flex Query ID: {queryId}");
if (fromDate is not null)
{
    Console.WriteLine($"Date range:    {fromDate} → {toDate}");
}
Console.WriteLine($"Output path:   {outputPath}");
Console.WriteLine();

using var credentials = OAuthCredentialsFactory.FromEnvironment();
var flexToken = Environment.GetEnvironmentVariable("IBKR_FLEX_TOKEN");
if (string.IsNullOrWhiteSpace(flexToken))
{
    Console.Error.WriteLine("IBKR_FLEX_TOKEN environment variable is not set.");
    return 1;
}

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
services.AddIbkrClient(opts =>
{
    opts.Credentials = credentials;
    opts.FlexToken = flexToken;
    opts.FlexPollTimeout = TimeSpan.FromSeconds(pollTimeoutSeconds);
});

await using var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IIbkrClient>();

Console.WriteLine("Executing Flex query (two-step: SendRequest → poll GetStatement)...");
var result = fromDate is not null
    ? await client.Flex.ExecuteQueryAsync(queryId, fromDate, toDate!)
    : await client.Flex.ExecuteQueryAsync(queryId);

var xml = result.RawXml.ToString();
await File.WriteAllTextAsync(outputPath, xml);

var sizeKb = (new FileInfo(outputPath).Length / 1024.0).ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
Console.WriteLine();
Console.WriteLine($"✓ Saved {sizeKb} KB to {outputPath}");
Console.WriteLine($"  Trades:        {result.Trades.Count}");
Console.WriteLine($"  OpenPositions: {result.OpenPositions.Count}");

return 0;

static bool IsValidFlexDate(string date) =>
    date.Length == 8 &&
    DateTime.TryParseExact(date, "yyyyMMdd",
        System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.None, out _);

static void PrintUsage()
{
    Console.Error.WriteLine("Usage: CaptureFlexQuery <queryId> [options]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Options:");
    Console.Error.WriteLine("  --from <yyyyMMdd>     Override query start date (requires --to)");
    Console.Error.WriteLine("  --to <yyyyMMdd>       Override query end date (requires --from)");
    Console.Error.WriteLine("  --output <path>       Output file path");
    Console.Error.WriteLine("  --poll-timeout <sec>  Max seconds to wait for report generation (default: 60)");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Required environment variables:");
    Console.Error.WriteLine("  IBKR_CONSUMER_KEY, IBKR_ACCESS_TOKEN, IBKR_ACCESS_TOKEN_SECRET,");
    Console.Error.WriteLine("  IBKR_SIGNATURE_KEY, IBKR_ENCRYPTION_KEY, IBKR_DH_PRIME, IBKR_FLEX_TOKEN");
}
