using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

/// <summary>
/// Concrete implementation of IJwtTokenService.
/// Generates access tokens signed with HS256 and random refresh tokens.
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;

    public JwtTokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    /// <summary>
    /// Resolves the ${VAR} placeholder just like SecurityExtensions — ensures
    /// that signature and validation use the exact same key bytes.
    /// </summary>
    private string ResolveSecretKey()
    {
        var raw = _settings.SecretKey;
        if (!string.IsNullOrEmpty(raw) && raw.StartsWith("${") && raw.EndsWith("}"))
            raw = Environment.GetEnvironmentVariable(raw[2..^1]) ?? raw;
        return raw;
    }

    /// <inheritdoc />
    public string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ResolveSecretKey()))
        {
            KeyId = "ProjectLedger-HS256"
        };
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.UsrId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.UsrEmail),
            new Claim(JwtRegisteredClaimNames.Name,  user.UsrFullName),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new Claim("plan_id",   user.UsrPlanId.ToString()),
            new Claim("is_admin",  user.UsrIsAdmin.ToString().ToLower()),
            new Claim("is_active", user.UsrIsActive.ToString().ToLower())
        };

        var token = new JwtSecurityToken(
            issuer:             _settings.Issuer,
            audience:           _settings.Audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <inheritdoc />
    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    /// <inheritdoc />
    public Guid? GetUserIdFromExpiredToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ResolveSecretKey()));

        var validationParams = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = _settings.Issuer,
            ValidAudience            = _settings.Audience,
            IssuerSigningKey         = key,
            ValidateLifetime         = false     // Allow expired token
        };

        try
        {
            var principal = tokenHandler.ValidateToken(token, validationParams, out _);
            var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return Guid.TryParse(sub, out var userId) ? userId : null;
        }
        catch
        {
            return null;
        }
    }
}
