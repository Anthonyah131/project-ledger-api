using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Split;

namespace ProjectLedger.API.DTOs.Expense;

// ── Requests ────────────────────────────────────────────────

/// <summary>
/// Request to create an expense.
/// DOES NOT include ProjectId (comes from route) nor CreatedByUserId (comes from JWT).
/// This prevents privilege escalation.
/// </summary>
public class CreateExpenseRequest
{
    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    public Guid PaymentMethodId { get; set; }

    /// <summary>FK → obligations. NULL = regular expense; value = debt payment.</summary>
    public Guid? ObligationId { get; set; }

    /// <summary>
    /// Equivalent amount in the obligation's currency.
    /// Used when ObligationId is present and OriginalCurrency differs from the obligation's currency.
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

    /// <summary>
    /// Amount converted to the project's currency. This is the value used for totals and calculations.
    /// The frontend is responsible for sending it pre-calculated.
    /// </summary>
    [Required]
    [Range(0.01, 999999999999.99, ErrorMessage = "Converted amount must be between 0.01 and 999,999,999,999.99.")]
    public decimal ConvertedAmount { get; set; }

    [Range(0.01, 999999999999.99, ErrorMessage = "Account amount must be between 0.01 and 999,999,999,999.99.")]
    public decimal? AccountAmount { get; set; }

    // ── Descriptive Data ────────────────────────────────────

    [Required]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 255 characters.")]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    [Required]
    public DateOnly ExpenseDate { get; set; }

    [StringLength(100, ErrorMessage = "Receipt number cannot exceed 100 characters.")]
    public string? ReceiptNumber { get; set; }

    public string? Notes { get; set; }
    public bool IsTemplate { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Conversions to the project's alternative currencies.
    /// The frontend calculates and sends the converted amount for each currency.
    /// </summary>
    public List<CurrencyExchangeRequest>? CurrencyExchanges { get; set; }

    /// <summary>
    /// Splits among partners. Optional.
    /// If omitted or partners module is disabled → auto-split 100% to the account owner.
    /// If provided: all must be 'percentage' summing to 100, or all 'fixed' summing to original_amount.
    /// </summary>
    public List<SplitInputDto>? Splits { get; set; }
}

/// <summary>Request to update an expense.</summary>
public class UpdateExpenseRequest
{
    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    public Guid PaymentMethodId { get; set; }

    /// <summary>FK → obligations. NULL = regular expense; value = debt payment.</summary>
    public Guid? ObligationId { get; set; }

    /// <summary>
    /// Equivalent amount in the obligation's currency.
    /// Used when the expense is linked to an obligation and the original currency differs.
    /// </summary>
    [Range(0.01, 999999999999.99, ErrorMessage = "Obligation equivalent amount must be between 0.01 and 999,999,999,999.99.")]
    public decimal? ObligationEquivalentAmount { get; set; }

    [Required]
    [Range(0.01, 999999999999.99, ErrorMessage = "Original amount must be between 0.01 and 999,999,999,999.99.")]
    public decimal OriginalAmount { get; set; }

    [Required]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Original currency must be a 3-character ISO 4217 code.")]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Original currency must be uppercase ISO 4217 (e.g. USD, EUR, CRC).")]
    public string OriginalCurrency { get; set; } = null!;

    [Range(0.000001, 999999999999.999999, ErrorMessage = "Exchange rate must be greater than 0.")]
    public decimal ExchangeRate { get; set; } = 1.000000m;

    /// <summary>
    /// Amount converted to the project's currency. This is the value used for totals and calculations.
    /// The frontend is responsible for sending it pre-calculated.
    /// </summary>
    [Required]
    [Range(0.01, 999999999999.99, ErrorMessage = "Converted amount must be between 0.01 and 999,999,999,999.99.")]
    public decimal ConvertedAmount { get; set; }

    [Range(0.01, 999999999999.99, ErrorMessage = "Account amount must be between 0.01 and 999,999,999,999.99.")]
    public decimal? AccountAmount { get; set; }

    [Required]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 255 characters.")]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    [Required]
    public DateOnly ExpenseDate { get; set; }

    [StringLength(100, ErrorMessage = "Receipt number cannot exceed 100 characters.")]
    public string? ReceiptNumber { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// If provided, updates the template state.
    /// If null, the current value is preserved.
    /// </summary>
    public bool? IsTemplate { get; set; }

    /// <summary>
    /// If provided, updates the accounting state.
    /// false = reminder (does not count towards totals/payments).
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>
    /// Conversions to the project's alternative currencies.
    /// If null, existing exchanges are not modified.
    /// If an empty list, all exchanges are deleted.
    /// </summary>
    public List<CurrencyExchangeRequest>? CurrencyExchanges { get; set; }

    /// <summary>
    /// Splits among partners. Optional.
    /// null → do not modify existing splits.
    /// [] → delete all splits.
    /// [...] → replace with the provided splits.
    /// </summary>
    public List<SplitInputDto>? Splits { get; set; }
}

/// <summary>
/// Request to create an actual expense from a template.
/// Allows overriding amount, date, and obligation;
/// the rest is taken from the template.
/// </summary>
public class CreateFromTemplateRequest
{
    [Range(0.01, 999999999999.99, ErrorMessage = "Original amount must be between 0.01 and 999,999,999,999.99.")]
    public decimal? OriginalAmount { get; set; }

    [Range(0.01, 999999999999.99, ErrorMessage = "Converted amount must be between 0.01 and 999,999,999,999.99.")]
    public decimal? ConvertedAmount { get; set; }

    public DateOnly? ExpenseDate { get; set; }
    public Guid? ObligationId { get; set; }
    [Range(0.01, 999999999999.99, ErrorMessage = "Obligation equivalent amount must be between 0.01 and 999,999,999,999.99.")]
    public decimal? ObligationEquivalentAmount { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Request to activate/deactivate an expense without sending the full payload.
/// </summary>
public class UpdateExpenseActiveStateRequest
{
    public bool IsActive { get; set; }
}

/// <summary>
/// Request to extract an expense draft from an image/PDF using Azure Document Intelligence.
/// </summary>
public class ExtractExpenseFromDocumentRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;

    [RegularExpression("^(receipt|invoice)$", ErrorMessage = "Document type must be 'receipt' or 'invoice'.")]
    public string DocumentKind { get; set; } = "receipt";
}

// ── Responses ───────────────────────────────────────────────

/// <summary>Response with the data of an expense.</summary>
public class ExpenseResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public Guid PaymentMethodId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? ObligationId { get; set; }
    public decimal? ObligationEquivalentAmount { get; set; }

    public decimal OriginalAmount { get; set; }
    public string OriginalCurrency { get; set; } = null!;
    public decimal ExchangeRate { get; set; }
    public decimal ConvertedAmount { get; set; }
    public decimal? AccountAmount { get; set; }
    public string? AccountCurrency { get; set; }

    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? Notes { get; set; }
    public bool IsTemplate { get; set; }
    public bool IsActive { get; set; }

    public List<CurrencyExchangeResponse>? CurrencyExchanges { get; set; }

    /// <summary>Indicates whether the movement has registered splits. Useful for showing an indicator in the list.</summary>
    public bool HasSplits { get; set; }

    /// <summary>Splits among partners. Present only when the project has partners_enabled = true.</summary>
    public List<SplitResponseDto>? Splits { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // ── Soft delete info (only visible with includeDeleted=true) ─
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }
}

/// <summary>
/// Response from the AI extraction endpoint to pre-fill the expense form.
/// </summary>
public class ExtractExpenseFromDocumentResponse
{
    public string Provider { get; set; } = null!;
    public string DocumentKind { get; set; } = null!;
    public string ModelId { get; set; } = null!;
    public ExpenseDocumentDraftResponse Draft { get; set; } = new();
    public SuggestedExpenseCategoryResponse? SuggestedCategory { get; set; }
    public SuggestedExpensePaymentMethodResponse? SuggestedPaymentMethod { get; set; }
    public List<ExpenseCategoryOptionResponse> AvailableCategories { get; set; } = [];
    public List<ExpensePaymentMethodOptionResponse> AvailablePaymentMethods { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public class DocumentReadQuotaResponse
{
    public Guid ProjectOwnerUserId { get; set; }
    public string PlanName { get; set; } = null!;
    public string PlanSlug { get; set; } = null!;
    public bool CanUseOcr { get; set; }
    public int UsedThisMonth { get; set; }
    public int? MonthlyLimit { get; set; }
    public int? RemainingThisMonth { get; set; }
    public bool IsUnlimited { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
}

public class ExpenseDocumentDraftResponse
{
    public Guid? CategoryId { get; set; }
    public Guid? PaymentMethodId { get; set; }
    public Guid? ObligationId { get; set; }
    public decimal? ObligationEquivalentAmount { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal? OriginalAmount { get; set; }
    public string? OriginalCurrency { get; set; }
    public decimal? ExchangeRate { get; set; }
    public decimal? ConvertedAmount { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? Notes { get; set; }
    public bool IsTemplate { get; set; }
    public List<CurrencyExchangeRequest>? CurrencyExchanges { get; set; }
    public string? DetectedMerchantName { get; set; }
    public string? DetectedPaymentMethodText { get; set; }
}

public class SuggestedExpenseCategoryResponse
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = null!;
    public decimal Confidence { get; set; }
    public string Reason { get; set; } = null!;
}

public class SuggestedExpensePaymentMethodResponse
{
    public Guid PaymentMethodId { get; set; }
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public decimal Confidence { get; set; }
    public string Reason { get; set; } = null!;
}

public class ExpenseCategoryOptionResponse
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = null!;
    public bool IsDefault { get; set; }
}

public class ExpensePaymentMethodOptionResponse
{
    public Guid PaymentMethodId { get; set; }
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
}
