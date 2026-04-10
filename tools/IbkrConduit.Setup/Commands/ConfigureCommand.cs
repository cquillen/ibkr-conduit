namespace IbkrConduit.Setup.Commands;

/// <summary>
/// Collects portal credentials and writes the JSON credential file.
/// </summary>
internal static class ConfigureCommand
{
    /// <summary>
    /// Runs the configure subcommand.
    /// </summary>
    internal static Task<int> RunAsync(string[] args)
    {
        // Implemented in Task 6
        Console.WriteLine("configure: not yet implemented");
        return Task.FromResult(1);
    }
}
