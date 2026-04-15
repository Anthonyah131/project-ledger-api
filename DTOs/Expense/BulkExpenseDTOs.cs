using System.ComponentModel.DataAnnotations;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Split;

namespace ProjectLedger.API.DTOs.Expense;

/// <summary>
/// Request for bulk import of up to 100 expenses.
/// Each item contains all its fields — the frontend fills them manually.
/// The backend does not calculate or derive any field.
/// All-or-nothing operation: if any item fails validation, none are created.
/// </summary>
public class BulkCreateExpenseRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one item is required.")]
    [MaxLength(100, ErrorMessage = "A maximum of 100 items can be imported at once.")]
    public List<BulkExpenseItemRequest> Items { get; set; } = [];
}

/// <summary>
/// An individual expense within the batch. Contains all fields that the user
/// filled manually in the quick-import view.
/// </summary>
public class BulkExpenseItemRequest
{
    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    public Guid PaymentMethodId { get; set; }

    /// <summary>FK → obligations. NULL = regular expense; value = debt payment.</summary>
    public Guid? ObligationId { get; set; }

    /// <summary>
    /// Equivalent amount in the obligation's currency.
    /// Required when ObligationId is present and OriginalCurrency differs from the obligation's currency.
    /// </summary>
    [Range(0.01, 999999999999.99, ErrorMessage = "Obligation equivalent amount must be between 0.01 and 999,999,999,999.99.")]
    public decimal? ObligationEquivalentAmount { get; set; }

    // ── Amounts ─────────────────────────────────────────────

    [Required]
    [Range(0.01, 999999999999.99, ErrorMessage = "Original amount must be between 0.01 and 999,999,999,999.99.")]
    public decimal OriginalAmount { get; set; }

    [Required]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Original currency must be a 3-character ISO 4217 code.")]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Original currency must be uppercase ISO 4217 (e.g. USD, EUR, CRC).")]
    public string OriginalCurrency { get; set; } = null!;

    [Range(0.000001, 999999999999.999999, ErrorMessage = "Exchange rate must be greater than 0.")]
    public decimal ExchangeRate { get; set; } = 1.000000m;

    [Required]
    [Range(0.01, 999999999999.99, ErrorMessage = "Converted amount must be between 0.01 and 999,999,999,999.99.")]
    public decimal ConvertedAmount { get; set; }

    [Range(0.01, 999999999999.99, ErrorMessage = "Account amount must be between 0.01 and 999,999,999,999.99.")]
    public decimal? AccountAmount { get; set; }

    // ── Descriptive data ────────────────────────────────────

    [Required]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 255 characters.")]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Currency conversions to the project's alternative currencies.
    /// The frontend calculates and sends the converted amount for each currency.
    /// </summary>
    public List<CurrencyExchangeRequest>? CurrencyExchanges { get; set; }

    /// <summary>
    /// Partner splits. If the project has partners_enabled = true
    /// and the user configured splits manually, they are sent here.
    /// </summary>
    public List<SplitInputDto>? Splits { get; set; }
}

// ── Responses ───────────────────────────────────────────────

/// <summary>Response returned after a bulk expense creation: total count and summary of each created expense.</summary>
public class BulkCreateExpenseResponse
{
    public int Created { get; set; }
    public List<BulkCreatedItemResponse> Items { get; set; } = [];
}

/// <summary>Lightweight summary of a single expense created during a bulk-create operation.</summary>
public class BulkCreatedItemResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public decimal OriginalAmount { get; set; }
    public decimal ConvertedAmount { get; set; }
    public DateOnly Date { get; set; }
}
