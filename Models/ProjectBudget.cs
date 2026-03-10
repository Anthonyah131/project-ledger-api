namespace ProjectLedger.API.Models;

/// <summary>
/// Presupuesto global de un proyecto con umbral de alerta.
/// Solo un presupuesto activo por proyecto (partial UNIQUE index en DB).
/// </summary>
public class ProjectBudget
{
    public Guid PjbId { get; set; }
    public Guid PjbProjectId { get; set; }
    public decimal PjbTotalBudget { get; set; }
    public decimal PjbAlertPercentage { get; set; } = 80.00m;  // Umbral 1-100%
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
