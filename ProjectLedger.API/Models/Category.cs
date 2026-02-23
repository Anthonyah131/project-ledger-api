namespace ProjectLedger.API.Models;

/// <summary>
/// Categoría de gastos dentro de un proyecto.
/// cat_is_default marca la categoría "General" creada automáticamente.
/// Soporta presupuesto opcional por categoría.
/// </summary>
public class Category
{
    public Guid CatId { get; set; }
    public Guid CatProjectId { get; set; }
    public string CatName { get; set; } = null!;
    public string? CatDescription { get; set; }
    public bool CatIsDefault { get; set; }
    public decimal? CatBudgetAmount { get; set; }              // NULL = sin presupuesto
    public DateTime CatCreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime CatUpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Soft delete ─────────────────────────────────────────
    public bool CatIsDeleted { get; set; }
    public DateTime? CatDeletedAt { get; set; }
    public Guid? CatDeletedByUserId { get; set; }

    // ── Navigation properties ───────────────────────────────
    public Project Project { get; set; } = null!;
    public User? DeletedByUser { get; set; }

    public ICollection<Expense> Expenses { get; set; } = [];
}
