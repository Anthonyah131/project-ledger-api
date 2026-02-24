using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ProjectLedger.API.Extensions;

/// <summary>
/// Extensiones para extraer claims del usuario autenticado.
/// REGLA DE ORO: NUNCA aceptar UserId desde el body del request — siempre del JWT.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Obtiene el UserId (Guid) desde el claim "sub" del JWT.
    /// Retorna null si no existe o no es un Guid válido.
    /// </summary>
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        // Con MapInboundClaims = false, el claim mantiene su nombre JWT original "sub"
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(sub, out var id) ? id : null;
    }

    /// <summary>
    /// Obtiene el UserId o lanza UnauthorizedAccessException.
    /// Usar solo en endpoints protegidos con [Authorize].
    /// </summary>
    public static Guid GetRequiredUserId(this ClaimsPrincipal principal)
    {
        return principal.GetUserId()
               ?? throw new UnauthorizedAccessException("User ID not found in JWT claims.");
    }

    /// <summary>
    /// Obtiene el email desde el claim "email" del JWT.
    /// </summary>
    public static string? GetEmail(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(JwtRegisteredClaimNames.Email)
               ?? principal.FindFirstValue(ClaimTypes.Email);
    }
}
