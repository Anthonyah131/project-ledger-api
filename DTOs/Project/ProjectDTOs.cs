using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProjectLedger.API.DTOs.Project;

// ── Requests ────────────────────────────────────────────────

/// <summary>Request to create a project. DOES NOT include UserId (taken from JWT).</summary>
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
    /// Workspace the project belongs to. Optional — if not provided it is automatically
    /// assigned to the user's "General" workspace.
    /// Accepts both "workspace_id" (snake_case) and "workspaceId" (camelCase).
    /// </summary>
    [JsonPropertyName("workspace_id")]
    public Guid? WorkspaceId { get; set; }
}

/// <summary>Request to update a project. DOES NOT include ProjectId (comes from route).</summary>
public class UpdateProjectRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 255 characters.")]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }
}

// ── Responses ───────────────────────────────────────────────

/// <summary>Response with project data.</summary>
public class ProjectResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public string? Description { get; set; }
    public Guid OwnerUserId { get; set; }
    public string UserRole { get; set; } = null!;               // Authenticated user's role
    public Guid? WorkspaceId { get; set; }
    public string? WorkspaceName { get; set; }
    public bool PartnersEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Response for project members.</summary>
public class ProjectMemberResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserFullName { get; set; } = null!;
    public string UserEmail { get; set; } = null!;
    public string Role { get; set; } = null!;
    public DateTime JoinedAt { get; set; }
}

/// <summary>Request to invite a member to a project.</summary>
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

/// <summary>Request to change a project member's role.</summary>
public class UpdateMemberRoleRequest
{
    [Required]
    [RegularExpression(@"^(editor|viewer)$", ErrorMessage = "Role must be 'editor' or 'viewer'.")]
    public string Role { get; set; } = null!;
}

// ── Project Settings ─────────────────────────────────────────

/// <summary>Request to update project settings.</summary>
public class UpdateProjectSettingsRequest
{
    public bool? PartnersEnabled { get; set; }
}

// ── Split Defaults ────────────────────────────────────────────

/// <summary>Equal distribution of splits per partner to pre-fill the form.</summary>
public class SplitDefaultsResponse
{
    public IReadOnlyList<PartnerSplitDefault> Partners { get; set; } = [];
}

/// <summary>Partner with their default percentage in an equal distribution.</summary>
public class PartnerSplitDefault
{
    public Guid PartnerId { get; set; }
    public string Name { get; set; } = null!;
    public decimal DefaultPercentage { get; set; }
}

// ── Pinned Projects ──────────────────────────────────────────

/// <summary>Pinned project response. Includes pinnedAt in addition to normal fields.</summary>
public class PinnedProjectResponse : ProjectResponse
{
    public DateTime PinnedAt { get; set; }
}

/// <summary>Confirmation response when pinning a project.</summary>
public class PinProjectResponse
{
    public Guid ProjectId { get; set; }
    public DateTime PinnedAt { get; set; }
}

/// <summary>
/// Paginated project response with pinned section.
/// pinned[] is only included on page 1; it is empty on subsequent pages.
/// totalCount reflects only unpinned projects.
/// </summary>
public class ProjectsPagedResponse
{
    public IReadOnlyList<PinnedProjectResponse> Pinned { get; set; } = [];
    /// <summary>Total projects pinned by the user (across all projects, not just this workspace).</summary>
    public int PinnedCount { get; set; }
    public IReadOnlyList<ProjectResponse> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

// ── Project Lookup ───────────────────────────────────────────

/// <summary>Minimal item for selectors, pickers, and command palette.</summary>
public class ProjectLookupItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public Guid? WorkspaceId { get; set; }
    public string? WorkspaceName { get; set; }
}

/// <summary>Pinned project item for lookup (includes pinnedAt).</summary>
public class PinnedProjectLookupItem : ProjectLookupItem
{
    public DateTime PinnedAt { get; set; }
}

/// <summary>
/// Project lookup response.
/// pinned[] is only included on page 1; pinnedCount is always returned.
/// items[] contains only unpinned projects from the current page.
/// totalCount excludes pinned projects.
/// </summary>
public class ProjectsLookupResponse
{
    public IReadOnlyList<PinnedProjectLookupItem> Pinned { get; set; } = [];
    public int PinnedCount { get; set; }
    public IReadOnlyList<ProjectLookupItem> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

// ── Project Payment Methods ─────────────────────────────────

/// <summary>Request to link a payment method to a project.</summary>
public class LinkPaymentMethodRequest
{
    [Required]
    public Guid PaymentMethodId { get; set; }
}

/// <summary>Response of a payment method linked to a project.</summary>
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
