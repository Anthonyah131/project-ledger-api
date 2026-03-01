namespace ProjectLedger.API.Models;

/// <summary>
/// Método de pago del usuario (banco, efectivo, tarjeta).
/// Pertenece al usuario, no al proyecto — permite ver movimientos cruzando proyectos.
/// </summary>
public class PaymentMethod
{
    public Guid PmtId { get; set; }
    public Guid PmtOwnerUserId { get; set; }
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
    public User? DeletedByUser { get; set; }
    public Currency Currency { get; set; } = null!;

    public ICollection<Expense> Expenses { get; set; } = [];
    public ICollection<ProjectPaymentMethod> ProjectPaymentMethods { get; set; } = [];
}
