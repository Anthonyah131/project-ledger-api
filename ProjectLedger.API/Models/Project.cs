namespace ProjectLedger.API.Models;

/// <summary>
/// Proyecto financiero. Eje central del sistema multi-tenant.
/// Cada proyecto tiene su propia moneda base, categorías, gastos y presupuestos.
/// </summary>
public class Project
{
    public Guid PrjId { get; set; }
    public string PrjName { get; set; } = null!;
    public Guid PrjOwnerUserId { get; set; }
    public string PrjCurrencyCode { get; set; } = null!;       // ISO 4217
    public string? PrjDescription { get; set; }
    public DateTime PrjCreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime PrjUpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Soft delete ─────────────────────────────────────────
    public bool PrjIsDeleted { get; set; }
    public DateTime? PrjDeletedAt { get; set; }
    public Guid? PrjDeletedByUserId { get; set; }

    // ── Navigation properties ───────────────────────────────
    public User OwnerUser { get; set; } = null!;
    public User? DeletedByUser { get; set; }
    public Currency Currency { get; set; } = null!;

    public ICollection<ProjectMember> Members { get; set; } = [];
    public ICollection<Category> Categories { get; set; } = [];
    public ICollection<Expense> Expenses { get; set; } = [];
    public ICollection<Obligation> Obligations { get; set; } = [];
    public ICollection<ProjectBudget> Budgets { get; set; } = [];
}
