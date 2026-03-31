using Microsoft.Extensions.Logging;

namespace IbkrConduit.Session;

/// <summary>
/// Thread-safe implementation of <see cref="ISessionLifecycleNotifier"/> that manages
/// session refresh subscriptions and notifications.
/// </summary>
internal sealed partial class SessionLifecycleNotifier : ISessionLifecycleNotifier
{
    private readonly List<Func<CancellationToken, Task>> _subscribers = [];
    private readonly object _lock = new();
    private readonly ILogger<SessionLifecycleNotifier> _logger;

    /// <summary>
    /// Creates a new <see cref="SessionLifecycleNotifier"/>.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public SessionLifecycleNotifier(ILogger<SessionLifecycleNotifier> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public IDisposable Subscribe(Func<CancellationToken, Task> onSessionRefreshed)
    {
        lock (_lock)
        {
            _subscribers.Add(onSessionRefreshed);
        }

        return new Subscription(this, onSessionRefreshed);
    }

    /// <inheritdoc />
    public async Task NotifyAsync(CancellationToken cancellationToken)
    {
        Func<CancellationToken, Task>[] snapshot;
        lock (_lock)
        {
            snapshot = [.. _subscribers];
        }

        foreach (var subscriber in snapshot)
        {
            try
            {
                await subscriber(cancellationToken);
            }
            catch (Exception ex)
            {
                LogSubscriberError(ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Session lifecycle subscriber threw an exception")]
    private partial void LogSubscriberError(Exception exception);

    private void Remove(Func<CancellationToken, Task> callback)
    {
        lock (_lock)
        {
            _subscribers.Remove(callback);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly SessionLifecycleNotifier _notifier;
        private readonly Func<CancellationToken, Task> _callback;

        public Subscription(SessionLifecycleNotifier notifier, Func<CancellationToken, Task> callback)
        {
            _notifier = notifier;
            _callback = callback;
        }

        public void Dispose() => _notifier.Remove(_callback);
    }
}
