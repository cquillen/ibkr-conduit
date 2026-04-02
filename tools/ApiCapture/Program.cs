using ApiCapture;
using ApiCapture.Modules;

if (args.Length == 0)
{
    PrintUsage();
    return;
}

var command = args[0].ToLowerInvariant();
var outputDir = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "recordings"));

Console.WriteLine($"Output directory: {outputDir}");

await using var ctx = new CaptureContext();
await ctx.InitializeAsync(outputDir);

switch (command)
{
    case "spike":
        await SpikeCapture.RunAsync(ctx);
        break;
    default:
        Console.WriteLine($"Unknown command: {command}");
        PrintUsage();
        break;
}

static void PrintUsage()
{
    Console.WriteLine("ApiCapture tool — captures live IBKR API responses to WireMock JSON.");
    Console.WriteLine();
    Console.WriteLine("Usage: dotnet run --project tools/ApiCapture -- <command>");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  spike       Validate pipeline with GET /portfolio/accounts");
}
