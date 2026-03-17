namespace ProjectLedger.API.Models;

/// <summary>
/// Membresía de un usuario en un workspace.
/// Los miembros de un workspace NO heredan acceso automático a sus proyectos.
/// </summary>
public class WorkspaceMember
{
    public Guid WkmId { get; set; }
    public Guid WkmWorkspaceId { get; set; }
    public Guid WkmUserId { get; set; }
    public string WkmRole { get; set; } = "member";
    public DateTime WkmJoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime WkmCreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime WkmUpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Soft delete ─────────────────────────────────────────
    public bool WkmIsDeleted { get; set; }
    public DateTime? WkmDeletedAt { get; set; }
    public Guid? WkmDeletedByUserId { get; set; }

    // ── Navigation properties ───────────────────────────────
    public Workspace Workspace { get; set; } = null!;
    public User User { get; set; } = null!;
    public User? DeletedByUser { get; set; }
}

/// <summary>Roles posibles de un miembro en un workspace.</summary>
public static class WorkspaceRoles
{
    public const string Owner = "owner";
    public const string Member = "member";
}
