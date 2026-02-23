namespace ProjectLedger.API.Models;

/// <summary>
/// Membresía de un usuario en un proyecto.
/// Roles: owner, editor, viewer.
/// </summary>
public class ProjectMember
{
    public Guid PrmId { get; set; }
    public Guid PrmProjectId { get; set; }
    public Guid PrmUserId { get; set; }
    public string PrmRole { get; set; } = null!;               // 'owner', 'editor', 'viewer'
    public DateTime PrmJoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime PrmCreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime PrmUpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Soft delete ─────────────────────────────────────────
    public bool PrmIsDeleted { get; set; }
    public DateTime? PrmDeletedAt { get; set; }
    public Guid? PrmDeletedByUserId { get; set; }

    // ── Navigation properties ───────────────────────────────
    public Project Project { get; set; } = null!;
    public User User { get; set; } = null!;
    public User? DeletedByUser { get; set; }
}
