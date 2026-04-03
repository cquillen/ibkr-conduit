using System;
using System.Runtime.CompilerServices;

namespace IbkrConduit.Tests.Integration;

/// <summary>
/// A fact that only runs when the specified environment variable is set.
/// Skips with a descriptive message when the variable is missing.
/// </summary>
public sealed class EnvironmentFactAttribute : FactAttribute
{
    /// <summary>
    /// Creates a new environment-conditional fact.
    /// </summary>
    /// <param name="environmentVariable">The environment variable that must be set for the test to run.</param>
    /// <param name="sourceFilePath">Source file path (auto-populated by compiler).</param>
    /// <param name="sourceLineNumber">Source line number (auto-populated by compiler).</param>
    public EnvironmentFactAttribute(
        string environmentVariable,
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(environmentVariable)))
        {
            Skip = $"Requires environment variable '{environmentVariable}' to be set.";
        }
    }
}
