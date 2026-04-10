using ProjectLedger.API.DTOs.Auth;

namespace ProjectLedger.API.Services;

/// <summary>
/// Authentication service: registration, login, token refresh and revocation.
/// </summary>
public interface IAuthService
{
    /// <summary>Registers a new user. Returns null if the email already exists.</summary>
    Task<AuthResponse?> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

    /// <summary>Authenticates the user with email/password. Returns null if credentials are invalid.</summary>
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default);

    /// <summary>Authenticates or links a user using Google identity and returns a JWT.</summary>
    Task<string?> LoginWithGoogleAsync(string providerUserId, string email, string fullName, string? avatarUrl, CancellationToken ct = default);

    /// <summary>Generates a new access token from a valid refresh token.</summary>
    Task<AuthResponse?> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Revokes the refresh token (logout).</summary>
    Task<bool> RevokeTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Revokes all user refresh tokens (logout from all devices).</summary>
    Task RevokeAllTokensAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Verifies if a reset OTP code is valid without consuming it.
    /// Allows the frontend to proceed to the new password step only if the code is valid.
    /// Returns false if the code is invalid, expired, or already used.
    /// </summary>
    Task<bool> VerifyOtpAsync(string email, string otpCode, CancellationToken ct = default);

    /// <summary>
    /// Initiates the password reset flow.
    /// Generates a 6-digit OTP, stores its hash, and sends the email.
    /// Always returns true to not reveal if the email exists.
    /// </summary>
    Task<bool> ForgotPasswordAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Resets the password using the OTP code received by email.
    /// Returns false if the code is invalid, expired, or already used.
    /// </summary>
    Task<bool> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
}
