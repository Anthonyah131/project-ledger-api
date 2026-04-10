using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

/// <summary>
/// Service responsible for generating and validating JWT tokens.
/// Separated from IAuthService to be usable in other contexts (e.g., tests).
/// </summary>
public interface IJwtTokenService
{
    /// <summary>Generates a signed JWT access token for the given user.</summary>
    string GenerateAccessToken(User user);

    /// <summary>Generates a cryptographically random refresh token (base64url).</summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Retrieves the user's Guid from an expired access token.
    /// Used in the refresh flow to identify the user without validating expiration.
    /// </summary>
    Guid? GetUserIdFromExpiredToken(string token);
}
