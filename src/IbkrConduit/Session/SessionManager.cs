using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Diagnostics;
using IbkrConduit.Errors;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Session;

/// <summary>
/// Orchestrates brokerage session lifecycle: lazy initialization, periodic tickle,
/// re-authentication on failure, and clean shutdown.
/// </summary>
internal sealed partial class SessionManager : ISessionManager
{
    private static readonly UpDownCounter<long> _activeSessionCount =
        IbkrConduitDiagnostics.Meter.CreateUpDownCounter<long>("ibkr.conduit.session.active");

    private static readonly Histogram<double> _initDuration =
        IbkrConduitDiagnostics.Meter.CreateHistogram<double>("ibkr.conduit.session.initialize.duration", "ms");

    private static readonly Counter<long> _refreshCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.session.refresh.count");

    private static readonly Histogram<double> _refreshDuration =
        IbkrConduitDiagnostics.Meter.CreateHistogram<double>("ibkr.conduit.session.refresh.duration", "ms");

    private readonly ISessionTokenProvider _sessionTokenProvider;
    private readonly ITickleTimerFactory _tickleTimerFactory;
    private readonly IIbkrSessionApi _sessionApi;
    private readonly IbkrClientOptions _options;
    private readonly ISessionLifecycleNotifier _notifier;
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
        ISessionLifecycleNotifier notifier,
        ILogger<SessionManager> logger)
    {
        _sessionTokenProvider = sessionTokenProvider;
        _tickleTimerFactory = tickleTimerFactory;
        _sessionApi = sessionApi;
        _options = options;
        _notifier = notifier;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_state == SessionState.Ready)
        {
            return;
        }

        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Session.Initialize");

        await _semaphore.WaitAsync(cancellationToken);
        var sw = Stopwatch.StartNew();
        try
        {
            if (_state == SessionState.Ready)
            {
                return;
            }

            _state = SessionState.Initializing;
            LogInitializing();

            try
            {
                _currentLst = await _sessionTokenProvider.GetLiveSessionTokenAsync(cancellationToken);

                await _sessionApi.InitializeBrokerageSessionAsync(
                    new SsodhInitRequest(Publish: true, Compete: _options.Compete), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw WrapCredentialException(ex);
            }

            if (_options.SuppressMessageIds.Count > 0)
            {
                await _sessionApi.SuppressQuestionsAsync(
                    new SuppressRequest(_options.SuppressMessageIds), cancellationToken);
            }

            _tickleTimer = _tickleTimerFactory.Create(_sessionApi, OnTickleFailureAsync);
            await _tickleTimer.StartAsync(cancellationToken);

            ScheduleProactiveRefresh();

            _state = SessionState.Ready;
            _activeSessionCount.Add(1);
            _initDuration.Record(sw.Elapsed.TotalMilliseconds);
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
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Session.Reauthenticate");

        await _semaphore.WaitAsync(cancellationToken);
        var sw = Stopwatch.StartNew();
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

            try
            {
                _currentLst = await _sessionTokenProvider.RefreshAsync(cancellationToken);

                await _sessionApi.InitializeBrokerageSessionAsync(
                    new SsodhInitRequest(Publish: true, Compete: _options.Compete), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw WrapCredentialException(ex);
            }

            if (_options.SuppressMessageIds.Count > 0)
            {
                await _sessionApi.SuppressQuestionsAsync(
                    new SuppressRequest(_options.SuppressMessageIds), cancellationToken);
            }

            _tickleTimer = _tickleTimerFactory.Create(_sessionApi, OnTickleFailureAsync);
            await _tickleTimer.StartAsync(cancellationToken);

            ScheduleProactiveRefresh();

            await _notifier.NotifyAsync(cancellationToken);

            _state = SessionState.Ready;
            _refreshCount.Add(1, new KeyValuePair<string, object?>(LogFields.Trigger, "reauth"));
            _refreshDuration.Record(sw.Elapsed.TotalMilliseconds);
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

        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Session.Shutdown");

        var wasInitialized = _state != SessionState.Uninitialized;
        _state = SessionState.ShuttingDown;

        if (wasInitialized)
        {
            _activeSessionCount.Add(-1);
        }

        if (_tickleTimer != null)
        {
            await _tickleTimer.StopAsync();
        }

        CancelProactiveRefresh();

        if (wasInitialized)
        {
            try
            {
                await _sessionApi.LogoutAsync(CancellationToken.None);
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

        var timeUntilRefresh = _currentLst.Expiry - DateTimeOffset.UtcNow - _options.ProactiveRefreshMargin;
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

    private static IbkrConfigurationException WrapCredentialException(Exception ex) =>
        ex switch
        {
            CryptographicException ce when ce.Message.Contains("decrypt", StringComparison.OrdinalIgnoreCase) =>
                new IbkrConfigurationException(
                    "Failed to decrypt access token secret — verify EncryptionPrivateKey matches the key registered in the IBKR portal",
                    "EncryptionPrivateKey", ce),

            CryptographicException ce when ce.Message.Contains("sign", StringComparison.OrdinalIgnoreCase) =>
                new IbkrConfigurationException(
                    "RSA signature failed — verify SignaturePrivateKey matches the key registered in the IBKR portal",
                    "SignaturePrivateKey", ce),

            CryptographicException ce =>
                new IbkrConfigurationException(
                    "Cryptographic operation failed during session initialization — verify SignaturePrivateKey and EncryptionPrivateKey",
                    "SignaturePrivateKey, EncryptionPrivateKey", ce),

            HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } he =>
                new IbkrConfigurationException(
                    "LST acquisition rejected by IBKR — verify ConsumerKey and AccessToken are correct and not expired",
                    "ConsumerKey, AccessToken", he),

            HttpRequestException { StatusCode: null } he =>
                new IbkrConfigurationException(
                    "Cannot reach IBKR API — check network connectivity and BaseUrl configuration",
                    "BaseUrl", he),

            HttpRequestException he =>
                new IbkrConfigurationException(
                    "LST acquisition rejected by IBKR — verify ConsumerKey and AccessToken are correct and not expired",
                    "ConsumerKey, AccessToken", he),

            FormatException fe =>
                new IbkrConfigurationException(
                    "Diffie-Hellman key exchange produced invalid data — verify DhPrime is the correct RFC 3526 Group 14 prime",
                    "DhPrime", fe),

            InvalidOperationException ioe =>
                new IbkrConfigurationException(
                    "Diffie-Hellman key exchange failed — verify DhPrime is the correct RFC 3526 Group 14 prime",
                    "DhPrime", ioe),

            JsonException je =>
                new IbkrConfigurationException(
                    "Unexpected response format from IBKR LST endpoint — the API may be experiencing issues or the endpoint URL may be incorrect",
                    "BaseUrl", je),

            _ => new IbkrConfigurationException(
                $"Session initialization failed: {ex.Message}",
                null, ex),
        };

    private enum SessionState
    {
        Uninitialized,
        Initializing,
        Ready,
        Reauthenticating,
        ShuttingDown,
    }
}
