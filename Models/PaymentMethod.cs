namespace ProjectLedger.API.Models;

/// <summary>
/// User's payment method (bank, cash, card).
/// Belongs to the user, not the project — allows viewing movements across projects.
/// </summary>
public class PaymentMethod
{
    public Guid PmtId { get; set; }
    public Guid PmtOwnerUserId { get; set; }
    public Guid? PmtOwnerPartnerId { get; set; }
    public string PmtName { get; set; } = null!;
    public string PmtType { get; set; } = null!;               // 'bank', 'cash', 'card'
    public string PmtCurrency { get; set; } = null!;           // ISO 4217
    public string? PmtBankName { get; set; }
    public string? PmtAccountNumber { get; set; }
    public string? PmtDescription { get; set; }
    public DateTime PmtCreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime PmtUpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Soft delete ─────────────────────────────────────────
    public bool PmtIsDeleted { get; set; }
    public DateTime? PmtDeletedAt { get; set; }
    public Guid? PmtDeletedByUserId { get; set; }

    // ── Navigation properties ───────────────────────────────
    public User OwnerUser { get; set; } = null!;
    public Partner? OwnerPartner { get; set; }
    public User? DeletedByUser { get; set; }
    public Currency Currency { get; set; } = null!;

    public ICollection<Expense> Expenses { get; set; } = [];
    public ICollection<Income> Incomes { get; set; } = [];
    public ICollection<ProjectPaymentMethod> ProjectPaymentMethods { get; set; } = [];
}
