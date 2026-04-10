using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.Auth;

/// <summary>Request for login with email and password.</summary>
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

/// <summary>Request for a new user registration.</summary>
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
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Full name must be between 1 and 255 characters.")]
    public string FullName { get; set; } = null!;
}

/// <summary>Request to renew the access token using a refresh token.</summary>
public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = null!;
}

/// <summary>Request to revoke the active refresh token (logout).</summary>
public class RevokeTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = null!;
}

/// <summary>Successful authentication response.</summary>
public class AuthResponse
{
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
    public DateTime AccessTokenExpiresAt { get; set; }
    public UserAuthInfo User { get; set; } = null!;
}

/// <summary>Request to initiate the password reset flow.</summary>
public class ForgotPasswordRequest
{
    [Required]
    [EmailAddress(ErrorMessage = "A valid email address is required.")]
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
    public string Email { get; set; } = null!;
}

/// <summary>Request to verify if an OTP code is valid without consuming it.</summary>
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

/// <summary>Request to reset the password with an OTP code.</summary>
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

/// <summary>Minimal user data included in the auth response.</summary>
public class UserAuthInfo
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public bool IsActive { get; set; }
    public bool IsAdmin { get; set; }
    public string? AvatarUrl { get; set; }
}
