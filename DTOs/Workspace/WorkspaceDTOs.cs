using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.Workspace;

// ── Requests ────────────────────────────────────────────────

/// <summary>Request para crear un workspace.</summary>
public class CreateWorkspaceRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 255 characters.")]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    [StringLength(7, ErrorMessage = "Color must be a valid hex color (e.g. #RRGGBB).")]
    public string? Color { get; set; }

    [StringLength(50, ErrorMessage = "Icon cannot exceed 50 characters.")]
    public string? Icon { get; set; }
}

/// <summary>Request para asignar un proyecto a un workspace.</summary>
public class AssignProjectRequest
{
    [Required]
    public Guid ProjectId { get; set; }
}

/// <summary>Request para actualizar un workspace.</summary>
public class UpdateWorkspaceRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 255 characters.")]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    [StringLength(7, ErrorMessage = "Color must be a valid hex color (e.g. #RRGGBB).")]
    public string? Color { get; set; }

    [StringLength(50, ErrorMessage = "Icon cannot exceed 50 characters.")]
    public string? Icon { get; set; }
}

// ── Responses ───────────────────────────────────────────────

/// <summary>Respuesta resumida de un workspace.</summary>
public class WorkspaceResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public string Role { get; set; } = null!;
    public int ProjectCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Detalle de un workspace con proyectos y miembros.</summary>
public class WorkspaceDetailResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public string Role { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public IReadOnlyList<WorkspaceProjectItem> Projects { get; set; } = [];
    public IReadOnlyList<WorkspaceMemberItem> Members { get; set; } = [];
}

/// <summary>Proyecto dentro del contexto de un workspace.</summary>
public class WorkspaceProjectItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Miembro dentro del contexto de un workspace.</summary>
public class WorkspaceMemberItem
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string? Email { get; set; }
    public string Role { get; set; } = null!;
    public DateTime JoinedAt { get; set; }
}
