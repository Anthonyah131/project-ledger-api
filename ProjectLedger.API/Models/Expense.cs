namespace ProjectLedger.API.Models;

/// <summary>
/// Gasto financiero registrado en un proyecto.
/// Soporta multi-moneda con tipo de cambio manual y moneda alternativa de visualización.
/// Puede ser plantilla (exp_is_template) o pago de obligación (exp_obligation_id).
/// </summary>
public class Expense
{
    public Guid ExpId { get; set; }
    public Guid ExpProjectId { get; set; }
    public Guid ExpCategoryId { get; set; }
    public Guid ExpPaymentMethodId { get; set; }
    public Guid ExpCreatedByUserId { get; set; }
    public Guid? ExpObligationId { get; set; }                 // NULL = gasto normal; NOT NULL = pago de deuda

    // ── Montos y moneda ─────────────────────────────────────
    public decimal ExpOriginalAmount { get; set; }
    public string ExpOriginalCurrency { get; set; } = null!;   // ISO 4217
    public decimal ExpExchangeRate { get; set; } = 1.000000m;
    public decimal ExpConvertedAmount { get; set; }

    // ── Datos descriptivos ──────────────────────────────────
    public string ExpTitle { get; set; } = null!;
    public string? ExpDescription { get; set; }
    public DateOnly ExpExpenseDate { get; set; }
    public string? ExpReceiptNumber { get; set; }
    public string? ExpNotes { get; set; }

    // ── Plantilla ───────────────────────────────────────────
    public bool ExpIsTemplate { get; set; }

    // ── Moneda alternativa (opcional) ───────────────────────
    public string? ExpAltCurrency { get; set; }
    public decimal? ExpAltExchangeRate { get; set; }
    public decimal? ExpAltAmount { get; set; }

    // ── Timestamps y soft delete ────────────────────────────
    public DateTime ExpCreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpUpdatedAt { get; set; } = DateTime.UtcNow;
    public bool ExpIsDeleted { get; set; }
    public DateTime? ExpDeletedAt { get; set; }
    public Guid? ExpDeletedByUserId { get; set; }

    // ── Navigation properties ───────────────────────────────
    public Project Project { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public PaymentMethod PaymentMethod { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public User? DeletedByUser { get; set; }
    public Obligation? Obligation { get; set; }
    public Currency OriginalCurrencyNavigation { get; set; } = null!;
    public Currency? AltCurrencyNavigation { get; set; }
}
