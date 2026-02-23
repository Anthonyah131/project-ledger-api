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
}
