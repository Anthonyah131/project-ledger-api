using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProjectLedger.API.DTOs.Project;

// ── Requests ────────────────────────────────────────────────

/// <summary>Request para crear un proyecto. NO incluye UserId (se toma del JWT).</summary>
public class CreateProjectRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 255 characters.")]
    public string Name { get; set; } = null!;

    [Required]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency code must be a 3-character ISO 4217 code.")]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Currency code must be uppercase ISO 4217 (e.g. USD, EUR, CRC).")]
    public string CurrencyCode { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>
    /// Workspace al que pertenece el proyecto. Opcional — si no se provee se asigna
    /// automáticamente al workspace "General" del usuario.
    /// Acepta tanto "workspace_id" (snake_case) como "workspaceId" (camelCase).
    /// </summary>
    [JsonPropertyName("workspace_id")]
    public Guid? WorkspaceId { get; set; }
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
    public Guid? WorkspaceId { get; set; }
    public string? WorkspaceName { get; set; }
    public bool PartnersEnabled { get; set; }
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

// ── Project Settings ─────────────────────────────────────────

/// <summary>Request para actualizar configuraciones del proyecto.</summary>
public class UpdateProjectSettingsRequest
{
    public bool? PartnersEnabled { get; set; }
}

// ── Split Defaults ────────────────────────────────────────────

/// <summary>Distribución equitativa de splits por partner para pre-llenar el formulario.</summary>
public class SplitDefaultsResponse
{
    public IReadOnlyList<PartnerSplitDefault> Partners { get; set; } = [];
}

/// <summary>Partner con su porcentaje por defecto en una distribución equitativa.</summary>
public class PartnerSplitDefault
{
    public Guid PartnerId { get; set; }
    public string Name { get; set; } = null!;
    public decimal DefaultPercentage { get; set; }
}

// ── Pinned Projects ──────────────────────────────────────────

/// <summary>Respuesta de proyecto fijado. Incluye pinnedAt además de los campos normales.</summary>
public class PinnedProjectResponse : ProjectResponse
{
    public DateTime PinnedAt { get; set; }
}

/// <summary>Respuesta de confirmación al fijar un proyecto.</summary>
public class PinProjectResponse
{
    public Guid ProjectId { get; set; }
    public DateTime PinnedAt { get; set; }
}

/// <summary>
/// Respuesta paginada de proyectos con sección de fijados.
/// pinned[] solo se incluye en la página 1; en páginas posteriores viene vacío.
/// total refleja únicamente proyectos no fijados.
/// </summary>
public class ProjectsPagedResponse
{
    public IReadOnlyList<PinnedProjectResponse> Pinned { get; set; } = [];
    /// <summary>Total de proyectos fijados por el usuario (de todos los proyectos, no solo los de este workspace).</summary>
    public int PinnedCount { get; set; }
    public IReadOnlyList<ProjectResponse> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

// ── Project Payment Methods ─────────────────────────────────

/// <summary>Request para vincular un método de pago a un proyecto.</summary>
public class LinkPaymentMethodRequest
{
    [Required]
    public Guid PaymentMethodId { get; set; }
}

/// <summary>Respuesta de un método de pago vinculado a un proyecto.</summary>
public class ProjectPaymentMethodResponse
{
    public Guid Id { get; set; }
    public Guid PaymentMethodId { get; set; }
    public string PaymentMethodName { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string OwnerUserName { get; set; } = null!;
    public Guid? PartnerId { get; set; }
    public string? PartnerName { get; set; }
    public DateTime LinkedAt { get; set; }
}
