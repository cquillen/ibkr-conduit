namespace IbkrConduit.Setup.Commands;

/// <summary>
/// Validates credentials against the IBKR API.
/// </summary>
internal static class ValidateCommand
{
    /// <summary>
    /// Runs the validate subcommand.
    /// </summary>
    internal static Task<int> RunAsync(string[] args)
    {
        // Implemented in Task 7
        Console.WriteLine("validate: not yet implemented");
        return Task.FromResult(1);
    }
}
