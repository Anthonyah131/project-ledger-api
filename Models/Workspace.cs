namespace ProjectLedger.API.Models;

/// <summary>
/// Workspace that groups related projects.
/// A user can have multiple workspaces ("Home", "Company ABC", etc.).
/// </summary>
public class Workspace
{
    public Guid WksId { get; set; }
    public string WksName { get; set; } = null!;
    public Guid WksOwnerUserId { get; set; }
    public string? WksDescription { get; set; }
    public string? WksColor { get; set; }
    public string? WksIcon { get; set; }
    public DateTime WksCreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime WksUpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Soft delete ─────────────────────────────────────────
    public bool WksIsDeleted { get; set; }
    public DateTime? WksDeletedAt { get; set; }
    public Guid? WksDeletedByUserId { get; set; }

    // ── Navigation properties ───────────────────────────────
    public User OwnerUser { get; set; } = null!;
    public User? DeletedByUser { get; set; }

    public ICollection<WorkspaceMember> Members { get; set; } = [];
    public ICollection<Project> Projects { get; set; } = [];
}
