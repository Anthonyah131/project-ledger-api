namespace ProjectLedger.API.Models;

/// <summary>
/// Financial expense registered in a project.
/// Supports multi-currency with manual exchange rate and alternative display currency.
/// Can be a template (exp_is_template) or an obligation payment (exp_obligation_id).
/// </summary>
public class Expense
{
    public Guid ExpId { get; set; }
    public Guid ExpProjectId { get; set; }
    public Guid ExpCategoryId { get; set; }
    public Guid ExpPaymentMethodId { get; set; }
    public Guid ExpCreatedByUserId { get; set; }
    public Guid? ExpObligationId { get; set; }                 // NULL = normal expense; NOT NULL = debt payment

    // ── Amounts and currency ─────────────────────────────────────
    public decimal ExpOriginalAmount { get; set; }
    public string ExpOriginalCurrency { get; set; } = null!;   // ISO 4217
    public decimal ExpExchangeRate { get; set; } = 1.000000m;
    public decimal ExpConvertedAmount { get; set; }
    public decimal? ExpAccountAmount { get; set; }             // Amount in the payment method's currency
    public string? ExpAccountCurrency { get; set; }            // Payment method's currency
    public decimal? ExpObligationEquivalentAmount { get; set; }

    // ── Descriptive data ──────────────────────────────────
    public string ExpTitle { get; set; } = null!;
    public string? ExpDescription { get; set; }
    public DateOnly ExpExpenseDate { get; set; }
    public string? ExpReceiptNumber { get; set; }
    public string? ExpNotes { get; set; }

    // ── Template ───────────────────────────────────────────
    public bool ExpIsTemplate { get; set; }

    // ── Accounting state ──────────────────────────────────────
    // false = reminder (does not count in totals/payments)
    public bool ExpIsActive { get; set; } = true;

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

    // ── Splits between partners ────────────────────────────────
    public ICollection<ExpenseSplit> Splits { get; set; } = [];

    // ── Conversions to alternative currencies ─────────────────
    public ICollection<TransactionCurrencyExchange> CurrencyExchanges { get; set; } = [];
}
