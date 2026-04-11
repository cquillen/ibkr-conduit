namespace IbkrConduit.Setup;

/// <summary>
/// Console I/O helpers for colored output and interactive prompts.
/// </summary>
internal static class ConsoleHelper
{
    /// <summary>
    /// Writes a line with the specified color, then resets.
    /// </summary>
    internal static void WriteColored(string text, ConsoleColor color)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = previous;
    }

    /// <summary>
    /// Writes a success message in green.
    /// </summary>
    internal static void WriteSuccess(string text) =>
        WriteColored(text, ConsoleColor.Green);

    /// <summary>
    /// Writes a warning message in yellow.
    /// </summary>
    internal static void WriteWarning(string text) =>
        WriteColored(text, ConsoleColor.Yellow);

    /// <summary>
    /// Writes an error message in red.
    /// </summary>
    internal static void WriteError(string text) =>
        WriteColored(text, ConsoleColor.Red);

    /// <summary>
    /// Writes a step header (e.g., "Step 1: Generate Keys").
    /// </summary>
    internal static void WriteStep(int step, int total, string description) =>
        WriteColored($"\n[Step {step}/{total}] {description}", ConsoleColor.Cyan);

    /// <summary>
    /// Prompts for input with validation. Repeats until validator returns null (success).
    /// </summary>
    /// <param name="prompt">The prompt text shown to the user.</param>
    /// <param name="validate">Returns null if valid, or an error message if invalid.</param>
    /// <returns>The validated input string.</returns>
    internal static string PromptWithValidation(string prompt, Func<string, string?> validate)
    {
        while (true)
        {
            Console.Write(prompt);
            var input = Console.ReadLine()?.Trim() ?? string.Empty;
            var error = validate(input);
            if (error is null)
            {
                return input;
            }

            WriteError(error);
        }
    }

    /// <summary>
    /// Prompts for yes/no confirmation. Returns true for 'y' or 'Y'.
    /// </summary>
    internal static bool Confirm(string prompt)
    {
        Console.Write($"{prompt} [y/N] ");
        var input = Console.ReadLine()?.Trim() ?? string.Empty;
        return string.Equals(input, "y", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Pauses until the user presses Enter.
    /// </summary>
    internal static void WaitForEnter(string message = "Press Enter to continue...")
    {
        Console.WriteLine();
        Console.Write(message);
        Console.ReadLine();
    }
}
