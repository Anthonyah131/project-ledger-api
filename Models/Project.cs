namespace ProjectLedger.API.Models;

/// <summary>
/// Financial project. Central axis of the multi-tenant system.
/// Each project has its own base currency, categories, expenses and budgets.
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

    // ── Workspace (Phase 2b) ──────────────────────────────────
    public Guid? PrjWorkspaceId { get; set; }
    public bool PrjPartnersEnabled { get; set; }

    // ── Soft delete ─────────────────────────────────────────
    public bool PrjIsDeleted { get; set; }
    public DateTime? PrjDeletedAt { get; set; }
    public Guid? PrjDeletedByUserId { get; set; }

    // ── Navigation properties ───────────────────────────────
    public User OwnerUser { get; set; } = null!;
    public User? DeletedByUser { get; set; }
    public Currency Currency { get; set; } = null!;
    public Workspace? Workspace { get; set; }

    public ICollection<ProjectMember> Members { get; set; } = [];
    public ICollection<Category> Categories { get; set; } = [];
    public ICollection<Expense> Expenses { get; set; } = [];
    public ICollection<Obligation> Obligations { get; set; } = [];
    public ICollection<ProjectBudget> Budgets { get; set; } = [];
    public ICollection<ProjectPaymentMethod> ProjectPaymentMethods { get; set; } = [];
    public ICollection<Income> Incomes { get; set; } = [];
    public ICollection<ProjectAlternativeCurrency> AlternativeCurrencies { get; set; } = [];
}
