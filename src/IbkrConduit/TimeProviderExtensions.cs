using System;
using System.Threading;
using System.Threading.Tasks;

namespace IbkrConduit;

internal static class TimeProviderExtensions
{
    internal static Task Delay(this TimeProvider timeProvider, int milliseconds, CancellationToken cancellationToken = default)
        => timeProvider.Delay(TimeSpan.FromMilliseconds(milliseconds), cancellationToken);

    internal static Task Delay(this TimeProvider timeProvider, TimeSpan delay, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        var timer = timeProvider.CreateTimer(
            _ => { tcs.TrySetResult(); registration.Dispose(); },
            null, delay, Timeout.InfiniteTimeSpan);
        tcs.Task.ContinueWith(
            _ => timer.Dispose(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return tcs.Task;
    }
}
