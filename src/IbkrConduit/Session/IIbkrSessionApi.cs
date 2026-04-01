using Refit;

namespace IbkrConduit.Session;

/// <summary>
/// Refit interface for IBKR session management endpoints.
/// Used internally to initialize, maintain, and tear down brokerage sessions.
/// </summary>
public interface IIbkrSessionApi
{
    /// <summary>
    /// Initializes a brokerage session via SSO/DH.
    /// </summary>
    [Post("/v1/api/iserver/auth/ssodh/init")]
    Task<SsodhInitResponse> InitializeBrokerageSessionAsync(
        [Body] SsodhInitRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a tickle to keep the session alive and check auth status.
    /// </summary>
    [Post("/v1/api/tickle")]
    Task<TickleResponse> TickleAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Suppresses specified question message IDs to avoid interactive prompts.
    /// </summary>
    [Post("/v1/api/iserver/questions/suppress")]
    Task<SuppressResponse> SuppressQuestionsAsync(
        [Body] SuppressRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs out and terminates the brokerage session.
    /// </summary>
    [Post("/v1/api/logout")]
    Task<LogoutResponse> LogoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets all suppressed question messages.
    /// </summary>
    [Post("/v1/api/iserver/questions/suppress/reset")]
    Task<SuppressResetResponse> ResetSuppressedQuestionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the current authentication status of the brokerage session.
    /// </summary>
    [Get("/v1/api/iserver/auth/status")]
    Task<AuthStatusResponse> GetAuthStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers reauthentication of the brokerage session.
    /// </summary>
    [Obsolete("Deprecated by IBKR. Prefer using /iserver/auth/ssodh/init instead.")]
    [Post("/v1/api/iserver/reauthenticate")]
    Task<ReauthenticateResponse> ReauthenticateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the current SSO session.
    /// </summary>
    [Get("/v1/api/sso/validate")]
    Task<SsoValidateResponse> ValidateSsoAsync(CancellationToken cancellationToken = default);
}
