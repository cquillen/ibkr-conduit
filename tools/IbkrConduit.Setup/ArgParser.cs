namespace IbkrConduit.Setup;

/// <summary>
/// Simple argument parser for command-line options and flags.
/// </summary>
internal static class ArgParser
{
    /// <summary>
    /// Gets the value following a named option (e.g., --output /tmp returns "/tmp").
    /// Returns null if the option is not present.
    /// </summary>
    internal static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    /// <summary>
    /// Returns true if any of the specified flags are present in the arguments.
    /// </summary>
    internal static bool HasFlag(string[] args, params string[] names)
    {
        foreach (var arg in args)
        {
            foreach (var name in names)
            {
                if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
