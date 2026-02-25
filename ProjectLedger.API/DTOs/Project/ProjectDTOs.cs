using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.Project;

// ── Requests ────────────────────────────────────────────────

/// <summary>Request para crear un proyecto. NO incluye UserId (se toma del JWT).</summary>
public class CreateProjectRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 255 characters.")]
    public string Name { get; set; } = null!;

    [Required]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "CurrencyCode must be a 3-character ISO 4217 code.")]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "CurrencyCode must be uppercase ISO 4217 (e.g. USD, EUR, CRC).")]
    public string CurrencyCode { get; set; } = null!;

    public string? Description { get; set; }
}

/// <summary>Request para actualizar un proyecto. NO incluye ProjectId (viene de la ruta).</summary>
public class UpdateProjectRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 255 characters.")]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }
}

// ── Responses ───────────────────────────────────────────────

/// <summary>Respuesta con los datos de un proyecto.</summary>
public class ProjectResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public string? Description { get; set; }
    public Guid OwnerUserId { get; set; }
    public string UserRole { get; set; } = null!;               // Rol del usuario autenticado
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Respuesta para miembros de un proyecto.</summary>
public class ProjectMemberResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserFullName { get; set; } = null!;
    public string UserEmail { get; set; } = null!;
    public string Role { get; set; } = null!;
    public DateTime JoinedAt { get; set; }
}

/// <summary>Request para invitar un miembro a un proyecto.</summary>
public class AddProjectMemberRequest
{
    [Required]
    [EmailAddress(ErrorMessage = "A valid email address is required.")]
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
    public string Email { get; set; } = null!;

    [Required]
    [RegularExpression(@"^(editor|viewer)$", ErrorMessage = "Role must be 'editor' or 'viewer'.")]
    public string Role { get; set; } = null!;
}

/// <summary>Request para cambiar el rol de un miembro del proyecto.</summary>
public class UpdateMemberRoleRequest
{
    [Required]
    [RegularExpression(@"^(editor|viewer)$", ErrorMessage = "Role must be 'editor' or 'viewer'.")]
    public string Role { get; set; } = null!;
}
