namespace ProjectLedger.API.DTOs.Auth;

/// <summary>Request para login con email y contraseña.</summary>
public class LoginRequest
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
}

/// <summary>Request para registro de nuevo usuario.</summary>
public class RegisterRequest
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string FullName { get; set; } = null!;
}

/// <summary>Request para renovar el access token usando un refresh token.</summary>
public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = null!;
}

/// <summary>Request para revocar el refresh token activo (logout).</summary>
public class RevokeTokenRequest
{
    public string RefreshToken { get; set; } = null!;
}

/// <summary>Respuesta de autenticación exitosa.</summary>
public class AuthResponse
{
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
    public DateTime AccessTokenExpiresAt { get; set; }
    public UserAuthInfo User { get; set; } = null!;
}

/// <summary>Datos mínimos del usuario incluidos en la respuesta de auth.</summary>
public class UserAuthInfo
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public bool IsAdmin { get; set; }
    public string? AvatarUrl { get; set; }
}
