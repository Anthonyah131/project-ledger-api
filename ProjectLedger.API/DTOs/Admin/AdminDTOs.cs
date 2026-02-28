using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.Admin;

// ── Requests ────────────────────────────────────────────────

/// <summary>Request para que el admin edite información de un usuario.</summary>
public class AdminUpdateUserRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "FullName must be between 1 and 255 characters.")]
    public string FullName { get; set; } = null!;

    [Url(ErrorMessage = "AvatarUrl must be a valid URL.")]
    public string? AvatarUrl { get; set; }

    public Guid? PlanId { get; set; }

    /// <summary>Si se envía, otorga o revoca permisos de administrador global.</summary>
    public bool? IsAdmin { get; set; }
}

// ── Responses ───────────────────────────────────────────────

/// <summary>Respuesta completa de usuario para el admin.</summary>
public class AdminUserResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public bool IsActive { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public AdminUserPlanDto? Plan { get; set; }
}

/// <summary>Resumen del plan en respuesta admin.</summary>
public class AdminUserPlanDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
}
