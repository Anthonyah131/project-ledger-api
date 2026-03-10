using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

/// <summary>
/// Servicio responsable de generar y validar tokens JWT.
/// Separado de IAuthService para poder usarse en otros contextos (ej: tests).
/// </summary>
public interface IJwtTokenService
{
    /// <summary>Genera un access token JWT firmado para el usuario dado.</summary>
    string GenerateAccessToken(User user);

    /// <summary>Genera un refresh token criptográficamente aleatorio (base64url).</summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Obtiene el Guid del usuario desde un access token expirado.
    /// Utilizado en el flujo de refresh para identificar al usuario sin validar la expiración.
    /// </summary>
    Guid? GetUserIdFromExpiredToken(string token);
}
