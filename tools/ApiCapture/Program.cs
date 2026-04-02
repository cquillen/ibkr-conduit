using ApiCapture;

if (args.Length == 0)
{
    PrintUsage();
    return;
}

var category = args[0].ToLowerInvariant();
var namePattern = args.Length > 1 ? args[1] : null;

var outputDir = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "recordings"));

Console.WriteLine($"Output directory: {outputDir}");

await using var ctx = new CaptureContext();
await ctx.InitializeAsync(outputDir);

// Filter endpoints
var allEntries = EndpointTable.Entries;
var entries = category == "all"
    ? allEntries.ToList()
    : CaptureRunner.Filter(allEntries, category, namePattern);

if (entries.Count == 0)
{
    Console.WriteLine($"\nNo endpoints match filter: category='{category}'" +
        (namePattern is not null ? $" name='{namePattern}'" : ""));
    Console.WriteLine("\nAvailable categories:");
    foreach (var cat in allEntries.Select(e => e.Category).Distinct().Order())
    {
        var count = allEntries.Count(e => e.Category == cat);
        Console.WriteLine($"  {cat.ToLowerInvariant(),-20} ({count} endpoints)");
    }

    return;
}

Console.WriteLine($"\nRunning {entries.Count} endpoint(s)...");

var result = await CaptureRunner.RunAsync(ctx, entries);

Console.WriteLine($"\n{new string('=', 60)}");
Console.WriteLine($"Done: {result.Passed} passed, {result.Failed} failed, {result.Errors} errors");

static void PrintUsage()
{
    Console.WriteLine("ApiCapture — captures live IBKR API responses to WireMock JSON.");
    Console.WriteLine();
    Console.WriteLine("Usage: dotnet run --project tools/ApiCapture -- <category> [name-filter]");
    Console.WriteLine();
    Console.WriteLine("  all                   Run all endpoints");
    Console.WriteLine("  <category>            Run all endpoints in a category");
    Console.WriteLine("  <category> <name>     Run endpoints matching name (case-insensitive contains)");
    Console.WriteLine();
    Console.WriteLine("Categories:");

    foreach (var cat in EndpointTable.Entries.Select(e => e.Category).Distinct().Order())
    {
        var count = EndpointTable.Entries.Count(e => e.Category == cat);
        Console.WriteLine($"  {cat.ToLowerInvariant(),-20} ({count} endpoints)");
    }
}
