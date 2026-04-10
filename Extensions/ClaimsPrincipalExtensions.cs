using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ProjectLedger.API.Extensions;

/// <summary>
/// Extensions for extracting claims from the authenticated user.
/// GOLDEN RULE: NEVER accept UserId from the request body — always from the JWT.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Gets the UserId (Guid) from the JWT "sub" claim.
    /// Returns null if it doesn't exist or is not a valid Guid.
    /// </summary>
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        // With MapInboundClaims = false, the claim keeps its original JWT name "sub"
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(sub, out var id) ? id : null;
    }

    /// <summary>
    /// Gets the UserId or throws UnauthorizedAccessException.
    /// Use only in endpoints protected with [Authorize].
    /// </summary>
    public static Guid GetRequiredUserId(this ClaimsPrincipal principal)
    {
        return principal.GetUserId()
               ?? throw new UnauthorizedAccessException("UnauthorizedJwt");
    }

    /// <summary>
    /// Gets the email from the JWT "email" claim.
    /// </summary>
    public static string? GetEmail(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(JwtRegisteredClaimNames.Email)
               ?? principal.FindFirstValue(ClaimTypes.Email);
    }
}
