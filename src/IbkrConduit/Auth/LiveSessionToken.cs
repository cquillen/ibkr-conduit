using System.Diagnostics.CodeAnalysis;

namespace IbkrConduit.Auth;

/// <summary>
/// Represents an acquired Live Session Token with its expiration time.
/// </summary>
/// <param name="Token">The raw LST bytes used as HMAC key for signing API requests.</param>
/// <param name="Expiry">When this token expires.</param>
[ExcludeFromCodeCoverage]
internal record LiveSessionToken(byte[] Token, DateTimeOffset Expiry);
