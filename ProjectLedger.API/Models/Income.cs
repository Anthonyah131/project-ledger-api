namespace ProjectLedger.API.Models;

/// <summary>
/// Ingreso financiero registrado en un proyecto.
/// Soporta multi-moneda con tipo de cambio manual.
/// Estructura similar a Expense pero sin plantillas ni obligaciones.
/// </summary>
public class Income
{
    public Guid IncId { get; set; }
    public Guid IncProjectId { get; set; }
    public Guid IncCategoryId { get; set; }
    public Guid IncPaymentMethodId { get; set; }
    public Guid IncCreatedByUserId { get; set; }

    // ── Montos y moneda ─────────────────────────────────────
    public decimal IncOriginalAmount { get; set; }
    public string IncOriginalCurrency { get; set; } = null!;   // ISO 4217
    public decimal IncExchangeRate { get; set; } = 1.000000m;
    public decimal IncConvertedAmount { get; set; }

    // ── Datos descriptivos ──────────────────────────────────
    public string IncTitle { get; set; } = null!;
    public string? IncDescription { get; set; }
    public DateOnly IncIncomeDate { get; set; }
    public string? IncReceiptNumber { get; set; }
    public string? IncNotes { get; set; }

    // ── Timestamps y soft delete ────────────────────────────
    public DateTime IncCreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime IncUpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IncIsDeleted { get; set; }
    public DateTime? IncDeletedAt { get; set; }
    public Guid? IncDeletedByUserId { get; set; }

    // ── Navigation properties ───────────────────────────────
    public Project Project { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public PaymentMethod PaymentMethod { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public User? DeletedByUser { get; set; }
    public Currency OriginalCurrencyNavigation { get; set; } = null!;

    // ── Exchange values para monedas alternativas ───────────
    public ICollection<TransactionCurrencyExchange> CurrencyExchanges { get; set; } = [];
}
