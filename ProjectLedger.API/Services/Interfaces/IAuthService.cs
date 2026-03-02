using ProjectLedger.API.DTOs.Auth;

namespace ProjectLedger.API.Services;

/// <summary>
/// Servicio de autenticación: registro, login, refresh y revocación de tokens.
/// </summary>
public interface IAuthService
{
    /// <summary>Registra un nuevo usuario. Retorna null si el email ya existe.</summary>
    Task<AuthResponse?> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

    /// <summary>Autentica al usuario con email/password. Retorna null si las credenciales son inválidas.</summary>
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default);

    /// <summary>Genera un nuevo access token a partir de un refresh token válido.</summary>
    Task<AuthResponse?> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Revoca el refresh token (logout).</summary>
    Task<bool> RevokeTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Revoca todos los refresh tokens del usuario (logout de todos los dispositivos).</summary>
    Task RevokeAllTokensAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Verifica si un código OTP de restablecimiento es válido sin consumirlo.
    /// Permite al frontend avanzar al paso de nueva contraseña solo si el código es válido.
    /// Retorna false si el código es inválido, expirado o ya fue usado.
    /// </summary>
    Task<bool> VerifyOtpAsync(string email, string otpCode, CancellationToken ct = default);

    /// <summary>
    /// Inicia el flujo de restablecimiento de contraseña.
    /// Genera un OTP de 6 dígitos, lo guarda hasheado y envía el correo.
    /// Siempre retorna true para no revelar si el email existe.
    /// </summary>
    Task<bool> ForgotPasswordAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Restablece la contraseña usando el código OTP recibido por correo.
    /// Retorna false si el código es inválido, expirado o ya fue usado.
    /// </summary>
    Task<bool> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
}
