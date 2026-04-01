using System.Diagnostics.CodeAnalysis;

namespace IbkrConduit.Streaming;

/// <summary>
/// An <see cref="IDisposable"/> that cancels a <see cref="CancellationTokenSource"/> on dispose.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class CancellationDisposable : IDisposable
{
    private readonly CancellationTokenSource _cts;

    /// <summary>
    /// Creates a new <see cref="CancellationDisposable"/>.
    /// </summary>
    /// <param name="cts">The cancellation token source to cancel on dispose.</param>
    public CancellationDisposable(CancellationTokenSource cts) => _cts = cts;

    /// <inheritdoc />
    public void Dispose() => _cts.Cancel();
}
