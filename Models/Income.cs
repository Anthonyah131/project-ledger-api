namespace ProjectLedger.API.Models;

/// <summary>
/// Financial income registered in a project.
/// Supports multi-currency with manual exchange rate.
/// Structure similar to Expense but without templates or obligations.
/// </summary>
public class Income
{
    public Guid IncId { get; set; }
    public Guid IncProjectId { get; set; }
    public Guid IncCategoryId { get; set; }
    public Guid IncPaymentMethodId { get; set; }
    public Guid IncCreatedByUserId { get; set; }

    // ── Amounts and currency ─────────────────────────────────────
    public decimal IncOriginalAmount { get; set; }
    public string IncOriginalCurrency { get; set; } = null!;   // ISO 4217
    public decimal IncExchangeRate { get; set; } = 1.000000m;
    public decimal IncConvertedAmount { get; set; }
    public decimal? IncAccountAmount { get; set; }
    public string? IncAccountCurrency { get; set; }

    // ── Descriptive data ──────────────────────────────────
    public string IncTitle { get; set; } = null!;
    public string? IncDescription { get; set; }
    public DateOnly IncIncomeDate { get; set; }
    public string? IncReceiptNumber { get; set; }
    public string? IncNotes { get; set; }

    // ── Accounting state ──────────────────────────────────────
    // false = reminder (does not count in totals)
    public bool IncIsActive { get; set; } = true;

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

    // ── Splits between partners ────────────────────────────────
    public ICollection<IncomeSplit> Splits { get; set; } = [];

    // ── Exchange values for alternative currencies ───────────
    public ICollection<TransactionCurrencyExchange> CurrencyExchanges { get; set; } = [];
}
