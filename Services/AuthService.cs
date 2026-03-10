using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ProjectLedger.API.DTOs.Auth;
using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Implementación completa de autenticación: registro, login, refresh y revocación.
/// 
/// Seguridad:
/// - Passwords hasheados con BCrypt (work factor 12)
/// - Refresh tokens almacenados como SHA-256 hash (nunca en texto plano)
/// - Rotación obligatoria de refresh tokens en cada uso
/// - Detección de reutilización de tokens (revoca TODOS los del usuario)
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IRefreshTokenRepository _refreshTokenRepo;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepo;
    private readonly IPlanRepository _planRepo;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly JwtSettings _jwtSettings;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthService> _logger;

    // BCrypt work factor — 12 es buen balance entre seguridad y rendimiento
    private const int BcryptWorkFactor = 12;

    public AuthService(
        IUserRepository userRepo,
        IRefreshTokenRepository refreshTokenRepo,
        IPasswordResetTokenRepository passwordResetTokenRepo,
        IPlanRepository planRepo,
        IJwtTokenService jwtTokenService,
        IOptions<JwtSettings> jwtSettings,
        IEmailService emailService,
        ILogger<AuthService> logger)
    {
        _userRepo = userRepo;
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
        // 1. Verificar que el email no esté registrado
        var normalizedEmail = request.Email.ToLowerInvariant().Trim();
        if (await _userRepo.EmailExistsAsync(normalizedEmail, ct))
        {
            _logger.LogWarning("Registration attempt with existing email: {Email}", normalizedEmail);
            return null;
        }

        // 2. Obtener el plan gratuito por defecto
        var defaultPlan = await _planRepo.GetBySlugAsync("free", ct)
                          ?? (await _planRepo.GetActiveAsync(ct)).FirstOrDefault()
                          ?? throw new InvalidOperationException(
                              "No active plans found. Seed the database with at least one plan.");

        // 3. Crear usuario con password hasheado
        var user = new User
        {
            UsrId = Guid.NewGuid(),
            UsrEmail = normalizedEmail,
            UsrPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, BcryptWorkFactor),
            UsrFullName = request.FullName.Trim(),
            UsrPlanId = defaultPlan.PlnId,
            UsrIsActive = false,   // Nuevo usuario inicia DESACTIVADO — el admin lo activa
            UsrIsAdmin = false,
            UsrLastLoginAt = DateTime.UtcNow,
            UsrCreatedAt = DateTime.UtcNow,
            UsrUpdatedAt = DateTime.UtcNow
        };

        await _userRepo.AddAsync(user, ct);

        // 4. Generar tokens
        var accessToken = _jwtTokenService.GenerateAccessToken(user);
        var rawRefreshToken = _jwtTokenService.GenerateRefreshToken();

        // 5. Persistir refresh token (solo el hash SHA-256)
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

        // Enviar correos de notificación (fire-and-forget, no bloquean el registro)
        _ = _emailService.SendWelcomeEmailAsync(user.UsrEmail, user.UsrFullName, ct);
        _ = _emailService.SendNewUserNotificationToAdminAsync(user.UsrEmail, user.UsrFullName, ct);

        return BuildAuthResponse(user, accessToken, rawRefreshToken);
    }

    // ── Login ───────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = request.Email.ToLowerInvariant().Trim();

        // 1. Buscar usuario por email
        var user = await _userRepo.GetByEmailAsync(normalizedEmail, ct);
        if (user == null || user.UsrPasswordHash == null)
        {
            _logger.LogWarning("Login failed: user not found for email {Email}", normalizedEmail);
            return null;
        }

        // 2. Verificar password con BCrypt (timing-safe)
        //    Nota: usuarios desactivados SÍ pueden hacer login (solo lectura).
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.UsrPasswordHash))
        {
            _logger.LogWarning("Login failed: invalid password for {UserId}", user.UsrId);
            return null;
        }

        // 3. Actualizar last login
        user.UsrLastLoginAt = DateTime.UtcNow;
        user.UsrUpdatedAt = DateTime.UtcNow;
        _userRepo.Update(user);

        // 4. Generar tokens
        var accessToken = _jwtTokenService.GenerateAccessToken(user);
        var rawRefreshToken = _jwtTokenService.GenerateRefreshToken();

        // 5. Persistir refresh token
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

    // ── Refresh Token ───────────────────────────────────────

    /// <inheritdoc />
    public async Task<AuthResponse?> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var tokenHash = HashRefreshToken(refreshToken);

        // 1. Buscar el refresh token en DB por hash
        var storedToken = await _refreshTokenRepo.GetByTokenHashAsync(tokenHash, ct);

        if (storedToken == null)
        {
            // ⚠️ Posible robo de token: el token no existe o ya fue revocado.
            // Si fue revocado, intentar revocar TODOS los tokens del usuario.
            _logger.LogWarning("Refresh token not found or already revoked. Possible token theft.");
            return null;
        }

        // 2. Verificar que no haya expirado
        if (storedToken.RtkExpiresAt < DateTime.UtcNow)
        {
            storedToken.RtkRevokedAt = DateTime.UtcNow;
            await _refreshTokenRepo.SaveChangesAsync(ct);
            _logger.LogWarning("Expired refresh token used for user {UserId}", storedToken.RtkUserId);
            return null;
        }

        // 3. Obtener usuario (incluido en la validación de que siga activo)
        var user = await _userRepo.GetByIdAsync(storedToken.RtkUserId, ct);
        if (user == null || !user.UsrIsActive || user.UsrIsDeleted)
        {
            storedToken.RtkRevokedAt = DateTime.UtcNow;
            await _refreshTokenRepo.SaveChangesAsync(ct);
            return null;
        }

        // 4. ROTACIÓN: revocar el token actual
        storedToken.RtkRevokedAt = DateTime.UtcNow;

        // 5. Generar nuevos tokens
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

        // Verificar que el token pertenece al usuario correcto
        return token != null && token.PrtUserId == user.UsrId;
    }

    // ── Forgot Password ─────────────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        var normalizedEmail = email.ToLowerInvariant().Trim();
        var user = await _userRepo.GetByEmailAsync(normalizedEmail, ct);

        // Siempre retornar true: no revelar si el email existe
        if (user == null || user.UsrIsDeleted)
        {
            _logger.LogWarning("Password reset requested for unknown email: {Email}", normalizedEmail);
            return true;
        }

        // Invalidar códigos anteriores (uno activo a la vez)
        await _passwordResetTokenRepo.InvalidateAllByUserIdAsync(user.UsrId, ct);

        // Generar OTP de 6 dígitos
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

        // Enviar OTP por email (fire-and-forget)
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

        // Buscar token válido (no usado, no expirado) por hash del código
        var codeHash = HashCode(request.OtpCode);
        var token = await _passwordResetTokenRepo.GetActiveByCodeHashAsync(codeHash, ct);

        if (token == null || token.PrtUserId != user.UsrId)
        {
            _logger.LogWarning("Invalid or expired OTP for user {UserId}", user.UsrId);
            return false;
        }

        // Actualizar contraseña
        user.UsrPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, BcryptWorkFactor);
        user.UsrUpdatedAt    = DateTime.UtcNow;
        _userRepo.Update(user);

        // Marcar el token como usado e invalidar todos los demás
        await _passwordResetTokenRepo.InvalidateAllByUserIdAsync(user.UsrId, ct);
        await _passwordResetTokenRepo.SaveChangesAsync(ct);
        await _userRepo.SaveChangesAsync(ct);

        // Revocar todos los refresh tokens activos (invalida todas las sesiones abiertas)
        await _refreshTokenRepo.RevokeAllByUserIdAsync(user.UsrId, ct);
        await _refreshTokenRepo.SaveChangesAsync(ct);

        // Notificación por correo (fire-and-forget)
        _ = _emailService.SendPasswordChangedEmailAsync(user.UsrEmail, user.UsrFullName, ct);

        _logger.LogInformation("Password reset successful for user {UserId}", user.UsrId);
        return true;
    }
    // ── Private Helpers ─────────────────────────────────────

    /// <summary>
    /// Hash SHA-256 del refresh token para almacenamiento seguro.
    /// Nunca se guarda el token en texto plano en la base de datos.
    /// </summary>
    private static string HashRefreshToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Hash SHA-256 genérico para códigos OTP y otros tokens cortos.
    /// </summary>
    private static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Construye la respuesta de autenticación estándar.
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
