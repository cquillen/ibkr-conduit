using System.IO;
using System.Reflection;

namespace IbkrConduit.Tests.Integration.Fixtures;

/// <summary>
/// Loads WireMock fixture JSON files from the Fixtures directory.
/// </summary>
public static class FixtureLoader
{
    private static readonly string _fixturesDir = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
        "Fixtures");

    /// <summary>
    /// Loads a fixture file's response body as a string.
    /// </summary>
    /// <param name="module">The module directory (e.g., "Portfolio").</param>
    /// <param name="name">The fixture file name without extension (e.g., "GET-portfolio-accounts").</param>
    /// <returns>The response body JSON string.</returns>
    public static string LoadBody(string module, string name)
    {
        var path = Path.Combine(_fixturesDir, module, $"{name}.json");
        var json = File.ReadAllText(path);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var body = doc.RootElement.GetProperty("Response").GetProperty("Body");
        return body.GetRawText();
    }

    /// <summary>
    /// Loads the full fixture file as a string.
    /// </summary>
    /// <param name="module">The module directory (e.g., "Portfolio").</param>
    /// <param name="name">The fixture file name without extension (e.g., "GET-portfolio-accounts").</param>
    /// <returns>The full fixture JSON string.</returns>
    public static string LoadFull(string module, string name)
    {
        var path = Path.Combine(_fixturesDir, module, $"{name}.json");
        return File.ReadAllText(path);
    }
}
