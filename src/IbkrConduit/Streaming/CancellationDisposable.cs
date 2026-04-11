namespace IbkrConduit.Streaming;

/// <summary>
/// An <see cref="IDisposable"/> that cancels and disposes a
/// <see cref="CancellationTokenSource"/> on dispose.
/// </summary>
internal sealed class CancellationDisposable : IDisposable
{
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Creates a new <see cref="CancellationDisposable"/>.
    /// </summary>
    /// <param name="cts">The cancellation token source to cancel and dispose.</param>
    public CancellationDisposable(CancellationTokenSource cts) => _cts = cts;

    /// <inheritdoc />
    public void Dispose()
    {
        var cts = Interlocked.Exchange(ref _cts, null);
        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
        }
    }
}
