namespace ProjectLedger.API.Models;

/// <summary>
/// Global budget of a project with alert threshold.
/// Only one active budget per project (partial UNIQUE index in DB).
/// </summary>
public class ProjectBudget
{
    public Guid PjbId { get; set; }
    public Guid PjbProjectId { get; set; }
    public decimal PjbTotalBudget { get; set; }
    public decimal PjbAlertPercentage { get; set; } = 80.00m;  // Threshold 1-100%
    public DateTime PjbCreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime PjbUpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Soft delete ─────────────────────────────────────────
    public bool PjbIsDeleted { get; set; }
    public DateTime? PjbDeletedAt { get; set; }
    public Guid? PjbDeletedByUserId { get; set; }

    // ── Navigation properties ───────────────────────────────
    public Project Project { get; set; } = null!;
    public User? DeletedByUser { get; set; }
}
