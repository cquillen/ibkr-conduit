using System;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Session;

/// <summary>
/// Orchestrates brokerage session lifecycle: lazy initialization, periodic tickle,
/// re-authentication on failure, and clean shutdown.
/// </summary>
internal sealed partial class SessionManager : ISessionManager
{
    private readonly ISessionTokenProvider _sessionTokenProvider;
    private readonly ITickleTimerFactory _tickleTimerFactory;
    private readonly IIbkrSessionApi _sessionApi;
    private readonly IbkrClientOptions _options;
    private readonly ILogger<SessionManager> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();

    private SessionState _state = SessionState.Uninitialized;
    private ITickleTimer? _tickleTimer;
    private CancellationTokenSource? _proactiveRefreshCts;
    private LiveSessionToken? _currentLst;

    /// <summary>
    /// Creates a new session manager.
    /// </summary>
    public SessionManager(
        ISessionTokenProvider sessionTokenProvider,
        ITickleTimerFactory tickleTimerFactory,
        IIbkrSessionApi sessionApi,
        IbkrClientOptions options,
        ILogger<SessionManager> logger)
    {
        _sessionTokenProvider = sessionTokenProvider;
        _tickleTimerFactory = tickleTimerFactory;
        _sessionApi = sessionApi;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_state == SessionState.Ready)
        {
            return;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_state == SessionState.Ready)
            {
                return;
            }

            _state = SessionState.Initializing;
            LogInitializing();

            _currentLst = await _sessionTokenProvider.GetLiveSessionTokenAsync(cancellationToken);

            await _sessionApi.InitializeBrokerageSessionAsync(
                new SsodhInitRequest(Publish: true, Compete: _options.Compete), cancellationToken);

            if (_options.SuppressMessageIds.Count > 0)
            {
                await _sessionApi.SuppressQuestionsAsync(
                    new SuppressRequest(_options.SuppressMessageIds), cancellationToken);
            }

            _tickleTimer = _tickleTimerFactory.Create(_sessionApi, OnTickleFailureAsync);
            await _tickleTimer.StartAsync(cancellationToken);

            ScheduleProactiveRefresh();

            _state = SessionState.Ready;
            LogInitialized();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task ReauthenticateAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_state == SessionState.ShuttingDown)
            {
                return;
            }

            _state = SessionState.Reauthenticating;
            LogReauthenticating();

            if (_tickleTimer != null)
            {
                await _tickleTimer.StopAsync();
            }

            CancelProactiveRefresh();

            _currentLst = await _sessionTokenProvider.RefreshAsync(cancellationToken);

            await _sessionApi.InitializeBrokerageSessionAsync(
                new SsodhInitRequest(Publish: true, Compete: _options.Compete), cancellationToken);

            if (_options.SuppressMessageIds.Count > 0)
            {
                await _sessionApi.SuppressQuestionsAsync(
                    new SuppressRequest(_options.SuppressMessageIds), cancellationToken);
            }

            _tickleTimer = _tickleTimerFactory.Create(_sessionApi, OnTickleFailureAsync);
            await _tickleTimer.StartAsync(cancellationToken);

            ScheduleProactiveRefresh();

            _state = SessionState.Ready;
            LogReauthenticated();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_state == SessionState.ShuttingDown)
        {
            return;
        }

        var wasInitialized = _state != SessionState.Uninitialized;
        _state = SessionState.ShuttingDown;

        if (_tickleTimer != null)
        {
            await _tickleTimer.StopAsync();
        }

        CancelProactiveRefresh();

        if (wasInitialized)
        {
            try
            {
                await _sessionApi.LogoutAsync();
            }
            catch (Exception ex)
            {
                LogLogoutFailed(ex);
            }
        }

        _semaphore.Dispose();
        _disposeCts.Dispose();

        GC.SuppressFinalize(this);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Initializing brokerage session")]
    private partial void LogInitializing();

    [LoggerMessage(Level = LogLevel.Information, Message = "Brokerage session initialized successfully")]
    private partial void LogInitialized();

    [LoggerMessage(Level = LogLevel.Information, Message = "Re-authenticating brokerage session")]
    private partial void LogReauthenticating();

    [LoggerMessage(Level = LogLevel.Information, Message = "Brokerage session re-authenticated successfully")]
    private partial void LogReauthenticated();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Logout failed during shutdown — ignoring")]
    private partial void LogLogoutFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tickle failure detected — triggering re-authentication")]
    private partial void LogTickleFailure();

    [LoggerMessage(Level = LogLevel.Error, Message = "Proactive refresh failed")]
    private partial void LogProactiveRefreshFailed(Exception exception);

    private Task OnTickleFailureAsync(CancellationToken cancellationToken)
    {
        LogTickleFailure();
        return ReauthenticateAsync(cancellationToken);
    }

    private void ScheduleProactiveRefresh()
    {
        if (_currentLst == null)
        {
            return;
        }

        CancelProactiveRefresh();

        var timeUntilRefresh = _currentLst.Expiry - DateTimeOffset.UtcNow - TimeSpan.FromHours(1);
        if (timeUntilRefresh <= TimeSpan.Zero)
        {
            return;
        }

        _proactiveRefreshCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);
        var token = _proactiveRefreshCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(timeUntilRefresh, token);
                if (!token.IsCancellationRequested)
                {
                    await ReauthenticateAsync(token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
            catch (Exception ex)
            {
                LogProactiveRefreshFailed(ex);
            }
        }, token);
    }

    private void CancelProactiveRefresh()
    {
        if (_proactiveRefreshCts != null)
        {
            _proactiveRefreshCts.Cancel();
            _proactiveRefreshCts.Dispose();
            _proactiveRefreshCts = null;
        }
    }

    private enum SessionState
    {
        Uninitialized,
        Initializing,
        Ready,
        Reauthenticating,
        ShuttingDown,
    }
}
