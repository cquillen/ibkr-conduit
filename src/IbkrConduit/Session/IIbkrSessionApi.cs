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
        [Body] SsodhInitRequest request);

    /// <summary>
    /// Sends a tickle to keep the session alive and check auth status.
    /// </summary>
    [Post("/v1/api/tickle")]
    Task<TickleResponse> TickleAsync();

    /// <summary>
    /// Suppresses specified question message IDs to avoid interactive prompts.
    /// </summary>
    [Post("/v1/api/iserver/questions/suppress")]
    Task<SuppressResponse> SuppressQuestionsAsync(
        [Body] SuppressRequest request);

    /// <summary>
    /// Logs out and terminates the brokerage session.
    /// </summary>
    [Post("/v1/api/logout")]
    Task<LogoutResponse> LogoutAsync();
}
