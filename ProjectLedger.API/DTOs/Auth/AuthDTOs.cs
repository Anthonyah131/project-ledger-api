using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.Auth;

/// <summary>Request para login con email y contraseña.</summary>
public class LoginRequest
{
    [Required]
    [EmailAddress(ErrorMessage = "A valid email address is required.")]
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
    public string Email { get; set; } = null!;

    [Required]
    [MinLength(1, ErrorMessage = "Password is required.")]
    public string Password { get; set; } = null!;
}

/// <summary>Request para registro de nuevo usuario.</summary>
public class RegisterRequest
{
    [Required]
    [EmailAddress(ErrorMessage = "A valid email address is required.")]
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
    public string Email { get; set; } = null!;

    [Required]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    [StringLength(128, ErrorMessage = "Password cannot exceed 128 characters.")]
    public string Password { get; set; } = null!;

    [Required]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "FullName must be between 1 and 255 characters.")]
    public string FullName { get; set; } = null!;
}

/// <summary>Request para renovar el access token usando un refresh token.</summary>
public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = null!;
}

/// <summary>Request para revocar el refresh token activo (logout).</summary>
public class RevokeTokenRequest
{
    [Required]
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

/// <summary>Request para iniciar el flujo de restablecimiento de contraseña.</summary>
public class ForgotPasswordRequest
{
    [Required]
    [EmailAddress(ErrorMessage = "A valid email address is required.")]
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
    public string Email { get; set; } = null!;
}

/// <summary>Request para verificar si un código OTP es válido sin consumirlo.</summary>
public class VerifyOtpRequest
{
    [Required]
    [EmailAddress(ErrorMessage = "A valid email address is required.")]
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
    public string Email { get; set; } = null!;

    [Required]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP code must be exactly 6 digits.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP code must contain only digits.")]
    public string OtpCode { get; set; } = null!;
}

/// <summary>Request para restablecer la contraseña con código OTP.</summary>
public class ResetPasswordRequest
{
    [Required]
    [EmailAddress(ErrorMessage = "A valid email address is required.")]
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
    public string Email { get; set; } = null!;

    [Required]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP code must be exactly 6 digits.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP code must contain only digits.")]
    public string OtpCode { get; set; } = null!;

    [Required]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    [StringLength(128, ErrorMessage = "Password cannot exceed 128 characters.")]
    public string NewPassword { get; set; } = null!;
}

/// <summary>Datos mínimos del usuario incluidos en la respuesta de auth.</summary>
public class UserAuthInfo
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public bool IsActive { get; set; }
    public bool IsAdmin { get; set; }
    public string? AvatarUrl { get; set; }
}
