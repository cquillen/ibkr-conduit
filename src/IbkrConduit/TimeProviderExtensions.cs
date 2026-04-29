namespace IbkrConduit;

internal static class TimeProviderExtensions
{
    internal static Task Delay(this TimeProvider timeProvider, TimeSpan delay, CancellationToken cancellationToken = default) =>
        Task.Delay(delay, timeProvider, cancellationToken);

    internal static Task Delay(this TimeProvider timeProvider, int milliseconds, CancellationToken cancellationToken = default) =>
        timeProvider.Delay(TimeSpan.FromMilliseconds(milliseconds), cancellationToken);
}
