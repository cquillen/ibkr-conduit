namespace IbkrConduit.Setup.Commands;

/// <summary>
/// Runs the full setup wizard (generate keys, portal walkthrough, configure, validate).
/// </summary>
internal static class WizardCommand
{
    /// <summary>
    /// Runs the wizard (default command).
    /// </summary>
    internal static Task<int> RunAsync(string[] args)
    {
        // Implemented in Task 8
        Console.WriteLine("wizard: not yet implemented");
        return Task.FromResult(1);
    }
}
