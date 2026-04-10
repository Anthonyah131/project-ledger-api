using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ProjectLedger.API.DTOs.Auth;
using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Complete authentication implementation: registration, login, refresh and revocation.
/// 
/// Security:
/// - Passwords hashed with BCrypt (work factor 12)
/// - Refresh tokens stored as SHA-256 hash (never in plain text)
/// - Mandatory refresh token rotation on each use
/// - Token reuse detection (revokes ALL user tokens)
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IExternalAuthProviderRepository _externalAuthProviderRepo;
    private readonly IRefreshTokenRepository _refreshTokenRepo;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepo;
    private readonly IPlanRepository _planRepo;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly JwtSettings _jwtSettings;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthService> _logger;

    // BCrypt work factor — 12 is a good balance between security and performance
    private const int BcryptWorkFactor = 12;

    public AuthService(
        IUserRepository userRepo,
        IExternalAuthProviderRepository externalAuthProviderRepo,
        IRefreshTokenRepository refreshTokenRepo,
        IPasswordResetTokenRepository passwordResetTokenRepo,
        IPlanRepository planRepo,
        IJwtTokenService jwtTokenService,
        IOptions<JwtSettings> jwtSettings,
        IEmailService emailService,
        ILogger<AuthService> logger)
    {
        _userRepo = userRepo;
        _externalAuthProviderRepo = externalAuthProviderRepo;
        _refreshTokenRepo = refreshTokenRepo;
        _passwordResetTokenRepo = passwordResetTokenRepo;
        _planRepo = planRepo;
        _jwtTokenService = jwtTokenService;
        _jwtSettings = jwtSettings.Value;
        _emailService = emailService;
        _logger = logger;
    }

    // ── Register ────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        // 1. Verify that the email is not already registered
        var normalizedEmail = request.Email.ToLowerInvariant().Trim();
        if (await _userRepo.EmailExistsAsync(normalizedEmail, ct))
        {
            _logger.LogWarning("Registration attempt with existing email: {Email}", normalizedEmail);
            return null;
        }

        // 2. Get the default free plan
        var defaultPlan = await _planRepo.GetBySlugAsync("free", ct)
                          ?? (await _planRepo.GetActiveAsync(ct)).FirstOrDefault()
                          ?? throw new InvalidOperationException("NoDefaultPlanAvailable");

        // 3. Create user with hashed password
        var user = new User
        {
            UsrId = Guid.NewGuid(),
            UsrEmail = normalizedEmail,
            UsrPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, BcryptWorkFactor),
            UsrFullName = request.FullName.Trim(),
            UsrPlanId = defaultPlan.PlnId,
            UsrIsActive = false,   // New user starts INACTIVE — the admin activates it
            UsrIsAdmin = false,
            UsrLastLoginAt = DateTime.UtcNow,
            UsrCreatedAt = DateTime.UtcNow,
            UsrUpdatedAt = DateTime.UtcNow
        };

        await _userRepo.AddAsync(user, ct);

        // 4. Generate tokens
        var accessToken = _jwtTokenService.GenerateAccessToken(user);
        var rawRefreshToken = _jwtTokenService.GenerateRefreshToken();

        // 5. Persist refresh token (only the SHA-256 hash)
        var refreshTokenEntity = new RefreshToken
        {
            RtkId = Guid.NewGuid(),
            RtkUserId = user.UsrId,
            RtkTokenHash = HashRefreshToken(rawRefreshToken),
            RtkExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            RtkCreatedAt = DateTime.UtcNow
        };

        await _refreshTokenRepo.AddAsync(refreshTokenEntity, ct);
        await _userRepo.SaveChangesAsync(ct);

        _logger.LogInformation("New user registered (inactive): {UserId}", user.UsrId);

        // Send notification emails (fire-and-forget, they don't block registration)
        _ = _emailService.SendWelcomeEmailAsync(user.UsrEmail, user.UsrFullName, ct);
        _ = _emailService.SendNewUserNotificationToAdminAsync(user.UsrEmail, user.UsrFullName, ct);

        return BuildAuthResponse(user, accessToken, rawRefreshToken);
    }

    // ── Login ───────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = request.Email.ToLowerInvariant().Trim();

        // 1. Find user by email
        var user = await _userRepo.GetByEmailAsync(normalizedEmail, ct);
        if (user == null || user.UsrPasswordHash == null)
        {
            _logger.LogWarning("Login failed: user not found for email {Email}", normalizedEmail);
            return null;
        }

        // 2. Verify password with BCrypt (timing-safe)
        //    Note: deactivated users CAN log in (read-only mode).
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.UsrPasswordHash))
        {
            _logger.LogWarning("Login failed: invalid password for {UserId}", user.UsrId);
            return null;
        }

        // 3. Update last login
        user.UsrLastLoginAt = DateTime.UtcNow;
        user.UsrUpdatedAt = DateTime.UtcNow;
        _userRepo.Update(user);

        // 4. Generate tokens
        var accessToken = _jwtTokenService.GenerateAccessToken(user);
        var rawRefreshToken = _jwtTokenService.GenerateRefreshToken();

        // 5. Persist refresh token
        var refreshTokenEntity = new RefreshToken
        {
            RtkId = Guid.NewGuid(),
            RtkUserId = user.UsrId,
            RtkTokenHash = HashRefreshToken(rawRefreshToken),
            RtkExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            RtkCreatedAt = DateTime.UtcNow
        };

        await _refreshTokenRepo.AddAsync(refreshTokenEntity, ct);
        await _userRepo.SaveChangesAsync(ct);

        _logger.LogInformation("User logged in: {UserId}", user.UsrId);

        return BuildAuthResponse(user, accessToken, rawRefreshToken);
    }

    // ── Login With Google ──────────────────────────────────

    /// <inheritdoc />
    public async Task<string?> LoginWithGoogleAsync(
        string providerUserId,
        string email,
        string fullName,
        string? avatarUrl,
        CancellationToken ct = default)
    {
        const string provider = "google";

        var normalizedProviderUserId = providerUserId.Trim();
        var normalizedEmail = email.ToLowerInvariant().Trim();

        if (string.IsNullOrWhiteSpace(normalizedProviderUserId)
            || string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return null;
        }

        User? user = null;

        // Step 1: find existing link by provider/providerUserId.
        var providerLink = await _externalAuthProviderRepo
            .GetByProviderAndProviderUserIdAsync(provider, normalizedProviderUserId, ct);

        if (providerLink != null && !providerLink.EapIsDeleted)
        {
            user = await _userRepo.GetByIdAsync(providerLink.EapUserId, ct);
            if (user == null)
            {
                _logger.LogWarning(
                    "Google provider link exists without active user. ProviderUserId: {ProviderUserId}",
                    normalizedProviderUserId);
                return null;
            }
        }

        // Step 2 + 3: link to existing email or create new user.
        var isNewUser = false;

        if (user == null)
        {
            user = await _userRepo.GetByEmailAsync(normalizedEmail, ct);

            if (user != null)
            {
                var linked = await EnsureGoogleProviderLinkedAsync(user, normalizedProviderUserId, normalizedEmail, ct);
                if (!linked)
                    return null;
            }
            else
            {
                var defaultPlan = await _planRepo.GetBySlugAsync("free", ct)
                                  ?? (await _planRepo.GetActiveAsync(ct)).FirstOrDefault()
                                  ?? throw new InvalidOperationException("NoDefaultPlanAvailable");

                user = new User
                {
                    UsrId = Guid.NewGuid(),
                    UsrEmail = normalizedEmail,
                    UsrPasswordHash = null,
                    UsrFullName = string.IsNullOrWhiteSpace(fullName)
                        ? normalizedEmail
                        : fullName.Trim(),
                    UsrPlanId = defaultPlan.PlnId,
                    UsrIsActive = false,
                    UsrIsAdmin = false,
                    UsrAvatarUrl = NormalizeOptional(avatarUrl),
                    UsrLastLoginAt = DateTime.UtcNow,
                    UsrCreatedAt = DateTime.UtcNow,
                    UsrUpdatedAt = DateTime.UtcNow
                };

                await _userRepo.AddAsync(user, ct);

                var newProviderLink = new ExternalAuthProvider
                {
                    EapId = Guid.NewGuid(),
                    EapUserId = user.UsrId,
                    EapProvider = provider,
                    EapProviderUserId = normalizedProviderUserId,
                    EapProviderEmail = normalizedEmail,
                    EapCreatedAt = DateTime.UtcNow,
                    EapUpdatedAt = DateTime.UtcNow
                };

                await _externalAuthProviderRepo.AddAsync(newProviderLink, ct);
                isNewUser = true;
            }
        }

        if (!isNewUser)
        {
            // Existing user: update last-login and avatar in the database.
            user.UsrLastLoginAt = DateTime.UtcNow;
            user.UsrUpdatedAt   = DateTime.UtcNow;

            var normalizedAvatar = NormalizeOptional(avatarUrl);
            if (!string.IsNullOrWhiteSpace(normalizedAvatar))
                user.UsrAvatarUrl = normalizedAvatar;

            _userRepo.Update(user);
        }
        // New user: all fields set during construction — calling Update would change
        // EF Core state from Added to Modified and cause a DB error.

        await _userRepo.SaveChangesAsync(ct);

        _logger.LogInformation("Google login successful for user {UserId}", user.UsrId);

        return _jwtTokenService.GenerateAccessToken(user);
    }

    // ── Refresh Token ───────────────────────────────────────

    /// <inheritdoc />
    public async Task<AuthResponse?> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var tokenHash = HashRefreshToken(refreshToken);

        // 1. Find the refresh token in DB by hash
        var storedToken = await _refreshTokenRepo.GetByTokenHashAsync(tokenHash, ct);

        if (storedToken == null)
        {
            // ⚠️ Possible token theft: the token does not exist or was already revoked.
            // If it was revoked, attempt to revoke ALL tokens for the user.
            _logger.LogWarning("Refresh token not found or already revoked. Possible token theft.");
            return null;
        }

        // 2. Verify that it has not expired
        if (storedToken.RtkExpiresAt < DateTime.UtcNow)
        {
            storedToken.RtkRevokedAt = DateTime.UtcNow;
            await _refreshTokenRepo.SaveChangesAsync(ct);
            _logger.LogWarning("Expired refresh token used for user {UserId}", storedToken.RtkUserId);
            return null;
        }

        // 3. Get user (included in the active status validation)
        var user = await _userRepo.GetByIdAsync(storedToken.RtkUserId, ct);
        if (user == null || !user.UsrIsActive || user.UsrIsDeleted)
        {
            storedToken.RtkRevokedAt = DateTime.UtcNow;
            await _refreshTokenRepo.SaveChangesAsync(ct);
            return null;
        }

        // 4. ROTATION: revoke the current token
        storedToken.RtkRevokedAt = DateTime.UtcNow;

        // 5. Generate new tokens
        var newAccessToken = _jwtTokenService.GenerateAccessToken(user);
        var newRawRefreshToken = _jwtTokenService.GenerateRefreshToken();

        var newRefreshToken = new RefreshToken
        {
            RtkId = Guid.NewGuid(),
            RtkUserId = user.UsrId,
            RtkTokenHash = HashRefreshToken(newRawRefreshToken),
            RtkExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            RtkCreatedAt = DateTime.UtcNow
        };

        await _refreshTokenRepo.AddAsync(newRefreshToken, ct);
        await _refreshTokenRepo.SaveChangesAsync(ct);

        _logger.LogInformation("Refresh token rotated for user {UserId}", user.UsrId);

        return BuildAuthResponse(user, newAccessToken, newRawRefreshToken);
    }

    // ── Revoke Token ────────────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> RevokeTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var tokenHash = HashRefreshToken(refreshToken);
        var storedToken = await _refreshTokenRepo.GetByTokenHashAsync(tokenHash, ct);

        if (storedToken == null)
            return false;

        storedToken.RtkRevokedAt = DateTime.UtcNow;
        await _refreshTokenRepo.SaveChangesAsync(ct);

        _logger.LogInformation("Refresh token revoked for user {UserId}", storedToken.RtkUserId);
        return true;
    }

    // ── Revoke All Tokens ───────────────────────────────────

    /// <inheritdoc />
    public async Task RevokeAllTokensAsync(Guid userId, CancellationToken ct = default)
    {
        await _refreshTokenRepo.RevokeAllByUserIdAsync(userId, ct);
        await _refreshTokenRepo.SaveChangesAsync(ct);

        _logger.LogInformation("All refresh tokens revoked for user {UserId}", userId);
    }
    // ── Verify OTP ──────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> VerifyOtpAsync(string email, string otpCode, CancellationToken ct = default)
    {
        var normalizedEmail = email.ToLowerInvariant().Trim();
        var user = await _userRepo.GetByEmailAsync(normalizedEmail, ct);

        if (user == null || user.UsrIsDeleted)
            return false;

        var codeHash = HashCode(otpCode);
        var token = await _passwordResetTokenRepo.GetActiveByCodeHashAsync(codeHash, ct);

        // Verify that the token belongs to the correct user
        return token != null && token.PrtUserId == user.UsrId;
    }

    // ── Forgot Password ─────────────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        var normalizedEmail = email.ToLowerInvariant().Trim();
        var user = await _userRepo.GetByEmailAsync(normalizedEmail, ct);

        // Always return true: do not reveal if the email exists
        if (user == null || user.UsrIsDeleted)
        {
            _logger.LogWarning("Password reset requested for unknown email: {Email}", normalizedEmail);
            return true;
        }

        // Invalidate previous codes (only one active at a time)
        await _passwordResetTokenRepo.InvalidateAllByUserIdAsync(user.UsrId, ct);

        // Generate a 6-digit OTP
        var rawCode = Random.Shared.Next(100000, 999999).ToString("D6");
        var tokenEntity = new PasswordResetToken
        {
            PrtId       = Guid.NewGuid(),
            PrtUserId   = user.UsrId,
            PrtCodeHash = HashCode(rawCode),
            PrtExpiresAt = DateTime.UtcNow.AddMinutes(15),
            PrtCreatedAt = DateTime.UtcNow
        };

        await _passwordResetTokenRepo.AddAsync(tokenEntity, ct);
        await _passwordResetTokenRepo.SaveChangesAsync(ct);

        // Send OTP via email (fire-and-forget)
        _ = _emailService.SendPasswordResetEmailAsync(user.UsrEmail, user.UsrFullName, rawCode, ct);

        _logger.LogInformation("Password reset OTP generated for user {UserId}", user.UsrId);
        return true;
    }

    // ── Reset Password ──────────────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = request.Email.ToLowerInvariant().Trim();
        var user = await _userRepo.GetByEmailAsync(normalizedEmail, ct);

        if (user == null || user.UsrIsDeleted)
        {
            _logger.LogWarning("Password reset attempt for unknown email: {Email}", normalizedEmail);
            return false;
        }

        // Find a valid token (unused, not expired) by code hash
        var codeHash = HashCode(request.OtpCode);
        var token = await _passwordResetTokenRepo.GetActiveByCodeHashAsync(codeHash, ct);

        if (token == null || token.PrtUserId != user.UsrId)
        {
            _logger.LogWarning("Invalid or expired OTP for user {UserId}", user.UsrId);
            return false;
        }

        // Update password
        user.UsrPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, BcryptWorkFactor);
        user.UsrUpdatedAt    = DateTime.UtcNow;
        _userRepo.Update(user);

        await _userRepo.ExecuteInTransactionAsync(async (ct) =>
        {
            // Mark the token as used and invalidate all others
            await _passwordResetTokenRepo.InvalidateAllByUserIdAsync(user.UsrId, ct);
            await _passwordResetTokenRepo.SaveChangesAsync(ct);
            await _userRepo.SaveChangesAsync(ct);

            // Revoke all active refresh tokens (invalidates all open sessions)
            await _refreshTokenRepo.RevokeAllByUserIdAsync(user.UsrId, ct);
            await _refreshTokenRepo.SaveChangesAsync(ct);
        }, ct);

        // Email notification (fire-and-forget)
        _ = _emailService.SendPasswordChangedEmailAsync(user.UsrEmail, user.UsrFullName, ct);

        _logger.LogInformation("Password reset successful for user {UserId}", user.UsrId);
        return true;
    }

    private async Task<bool> EnsureGoogleProviderLinkedAsync(
        User user,
        string providerUserId,
        string providerEmail,
        CancellationToken ct)
    {
        const string provider = "google";

        var existingProviders = await _externalAuthProviderRepo.GetByUserIdAsync(user.UsrId, ct);
        var existingGoogleProvider = existingProviders.FirstOrDefault(e =>
            string.Equals(e.EapProvider, provider, StringComparison.OrdinalIgnoreCase));

        if (existingGoogleProvider != null)
        {
            if (!string.Equals(existingGoogleProvider.EapProviderUserId, providerUserId, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Google link conflict for user {UserId}. Existing ProviderUserId: {ExistingProviderUserId}",
                    user.UsrId,
                    existingGoogleProvider.EapProviderUserId);
                return false;
            }

            existingGoogleProvider.EapProviderEmail = providerEmail;
            existingGoogleProvider.EapIsDeleted = false;
            existingGoogleProvider.EapDeletedAt = null;
            existingGoogleProvider.EapDeletedByUserId = null;
            existingGoogleProvider.EapUpdatedAt = DateTime.UtcNow;

            _externalAuthProviderRepo.Update(existingGoogleProvider);
            return true;
        }

        var newProviderLink = new ExternalAuthProvider
        {
            EapId = Guid.NewGuid(),
            EapUserId = user.UsrId,
            EapProvider = provider,
            EapProviderUserId = providerUserId,
            EapProviderEmail = providerEmail,
            EapCreatedAt = DateTime.UtcNow,
            EapUpdatedAt = DateTime.UtcNow
        };

        await _externalAuthProviderRepo.AddAsync(newProviderLink, ct);
        return true;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    // ── Private Helpers ─────────────────────────────────────

    /// <summary>
    /// SHA-256 hash of the refresh token for secure storage.
    /// The token is never stored in plain text in the database.
    /// </summary>
    private static string HashRefreshToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Generic SHA-256 hash for OTP codes and other short tokens.
    /// </summary>
    private static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Builds the standard authentication response.
    /// </summary>
    private AuthResponse BuildAuthResponse(User user, string accessToken, string rawRefreshToken)
    {
        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            User = new UserAuthInfo
            {
                Id = user.UsrId,
                Email = user.UsrEmail,
                FullName = user.UsrFullName,
                IsActive = user.UsrIsActive,
                IsAdmin = user.UsrIsAdmin,
                AvatarUrl = user.UsrAvatarUrl
            }
        };
    }
}
