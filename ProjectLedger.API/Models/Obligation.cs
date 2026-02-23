namespace ProjectLedger.API.Models;

/// <summary>
/// Obligación/deuda financiera dentro de un proyecto.
/// El estado (open, partially_paid, paid, overdue) se calcula dinámicamente
/// en la aplicación a partir de los pagos asociados, NO se persiste en DB.
/// </summary>
public class Obligation
{
    public Guid OblId { get; set; }
    public Guid OblProjectId { get; set; }
    public Guid OblCreatedByUserId { get; set; }
    public string OblTitle { get; set; } = null!;
    public string? OblDescription { get; set; }
    public decimal OblTotalAmount { get; set; }
    public string OblCurrency { get; set; } = null!;           // ISO 4217
    public DateOnly? OblDueDate { get; set; }
    public DateTime OblCreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime OblUpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Soft delete ─────────────────────────────────────────
    public bool OblIsDeleted { get; set; }
    public DateTime? OblDeletedAt { get; set; }
    public Guid? OblDeletedByUserId { get; set; }

    // ── Navigation properties ───────────────────────────────
    public Project Project { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public User? DeletedByUser { get; set; }
    public Currency Currency { get; set; } = null!;

    public ICollection<Expense> Payments { get; set; } = [];   // Gastos asociados como pagos
}
