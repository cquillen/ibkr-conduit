using Microsoft.Extensions.Logging;

namespace IbkrConduit.Session;

/// <summary>
/// Thread-safe implementation of <see cref="ISessionLifecycleNotifier"/> that manages
/// session refresh subscriptions and notifications.
/// </summary>
internal sealed partial class SessionLifecycleNotifier : ISessionLifecycleNotifier
{
    private readonly List<Func<CancellationToken, Task>> _subscribers = [];
    private readonly List<Func<CancellationToken, Task>> _tickleSubscribers = [];
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

    /// <inheritdoc />
    public IDisposable SubscribeTickleSucceeded(Func<CancellationToken, Task> onTickleSucceeded)
    {
        lock (_lock)
        {
            _tickleSubscribers.Add(onTickleSucceeded);
        }

        return new TickleSubscription(this, onTickleSucceeded);
    }

    /// <inheritdoc />
    public async Task NotifyTickleSucceededAsync(CancellationToken cancellationToken)
    {
        Func<CancellationToken, Task>[] snapshot;
        lock (_lock)
        {
            snapshot = [.. _tickleSubscribers];
        }

        foreach (var subscriber in snapshot)
        {
            try
            {
                await subscriber(cancellationToken);
            }
            catch (Exception ex)
            {
                LogTickleSubscriberError(ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Session lifecycle subscriber threw an exception")]
    private partial void LogSubscriberError(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tickle-succeeded subscriber threw an exception")]
    private partial void LogTickleSubscriberError(Exception exception);

    private void Remove(Func<CancellationToken, Task> callback)
    {
        lock (_lock)
        {
            _subscribers.Remove(callback);
        }
    }

    private void RemoveTickle(Func<CancellationToken, Task> callback)
    {
        lock (_lock)
        {
            _tickleSubscribers.Remove(callback);
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

    private sealed class TickleSubscription : IDisposable
    {
        private readonly SessionLifecycleNotifier _notifier;
        private readonly Func<CancellationToken, Task> _callback;

        public TickleSubscription(SessionLifecycleNotifier notifier, Func<CancellationToken, Task> callback)
        {
            _notifier = notifier;
            _callback = callback;
        }

        public void Dispose() => _notifier.RemoveTickle(_callback);
    }
}
