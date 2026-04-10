namespace IbkrConduit.Setup.Commands;

/// <summary>
/// Generates RSA key pairs and DH parameters for IBKR OAuth 1.0a.
/// </summary>
internal static class GenerateKeysCommand
{
    /// <summary>
    /// Runs the generate-keys subcommand.
    /// </summary>
    internal static Task<int> RunAsync(string[] args)
    {
        // Implemented in Task 5
        Console.WriteLine("generate-keys: not yet implemented");
        return Task.FromResult(1);
    }
}
