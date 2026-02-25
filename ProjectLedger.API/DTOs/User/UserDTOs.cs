using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.User;

// ── Requests ────────────────────────────────────────────────

/// <summary>Request para actualizar perfil. Solo campos que el usuario puede cambiar.</summary>
public class UpdateProfileRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "FullName must be between 1 and 255 characters.")]
    public string FullName { get; set; } = null!;

    [Url(ErrorMessage = "AvatarUrl must be a valid URL.")]
    public string? AvatarUrl { get; set; }
}

/// <summary>Request para cambiar contraseña.</summary>
public class ChangePasswordRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "CurrentPassword is required.")]
    public string CurrentPassword { get; set; } = null!;

    [Required]
    [MinLength(8, ErrorMessage = "NewPassword must be at least 8 characters.")]
    [StringLength(128, ErrorMessage = "NewPassword cannot exceed 128 characters.")]
    public string NewPassword { get; set; } = null!;
}

// ── Responses ───────────────────────────────────────────────

/// <summary>Respuesta pública del perfil (datos no sensibles).</summary>
public class UserProfileResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public bool IsActive { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public UserPlanSummaryDto Plan { get; set; } = null!;
}

/// <summary>Resumen del plan asignado al usuario.</summary>
public class UserPlanSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
}

/// <summary>Respuesta mínima de usuario (para listas, miembros, etc.).</summary>
public class UserSummaryResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
}
