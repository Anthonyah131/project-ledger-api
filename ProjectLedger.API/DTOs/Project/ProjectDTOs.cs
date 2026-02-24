namespace ProjectLedger.API.DTOs.Project;

// ── Requests ────────────────────────────────────────────────

/// <summary>Request para crear un proyecto. NO incluye UserId (se toma del JWT).</summary>
public class CreateProjectRequest
{
    public string Name { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;          // ISO 4217
    public string? Description { get; set; }
}

/// <summary>Request para actualizar un proyecto. NO incluye ProjectId (viene de la ruta).</summary>
public class UpdateProjectRequest
{
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
    public string Email { get; set; } = null!;
    public string Role { get; set; } = null!;                   // editor, viewer
}

/// <summary>Request para cambiar el rol de un miembro del proyecto.</summary>
public class UpdateMemberRoleRequest
{
    public string Role { get; set; } = null!;                   // editor, viewer
}
