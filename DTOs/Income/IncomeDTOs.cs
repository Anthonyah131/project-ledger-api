using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Split;

namespace ProjectLedger.API.DTOs.Income;

// ── Requests ────────────────────────────────────────────────

/// <summary>
/// Request to create an income.
/// DOES NOT include ProjectId (comes from route) nor CreatedByUserId (comes from JWT).
/// </summary>
public class CreateIncomeRequest
{
    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    public Guid PaymentMethodId { get; set; }

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

    // ── Descriptive Data ────────────────────────────────────

    [Required]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 255 characters.")]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    [Required]
    public DateOnly IncomeDate { get; set; }

    [StringLength(100, ErrorMessage = "Receipt number cannot exceed 100 characters.")]
    public string? ReceiptNumber { get; set; }

    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    // ── Alternative currencies (optional) ───────────────────

    public List<CurrencyExchangeRequest>? CurrencyExchanges { get; set; }

    /// <summary>
    /// Splits among partners. Optional.
    /// If omitted or partners module is disabled → auto-split 100%.
    /// </summary>
    public List<SplitInputDto>? Splits { get; set; }
}

/// <summary>Request to update an income.</summary>
public class UpdateIncomeRequest
{
    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    public Guid PaymentMethodId { get; set; }

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

    [Required]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 255 characters.")]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    [Required]
    public DateOnly IncomeDate { get; set; }

    [StringLength(100, ErrorMessage = "Receipt number cannot exceed 100 characters.")]
    public string? ReceiptNumber { get; set; }

    public string? Notes { get; set; }
    public bool? IsActive { get; set; }

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
/// Request to activate/deactivate an income without sending the full payload.
/// </summary>
public class UpdateIncomeActiveStateRequest
{
    public bool IsActive { get; set; }
}

/// <summary>
/// Request to extract an income draft from an image/PDF using Azure Document Intelligence.
/// </summary>
public class ExtractIncomeFromDocumentRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;

    [RegularExpression("^(receipt|invoice)$", ErrorMessage = "Document type must be 'receipt' or 'invoice'.")]
    public string DocumentKind { get; set; } = "invoice";
}

// ── Responses ───────────────────────────────────────────────

/// <summary>Response with the data of an income.</summary>
public class IncomeResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public string? ProjectCurrency { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public Guid PaymentMethodId { get; set; }
    public Guid CreatedByUserId { get; set; }

    public decimal OriginalAmount { get; set; }
    public string OriginalCurrency { get; set; } = null!;
    public decimal ExchangeRate { get; set; }
    public decimal ConvertedAmount { get; set; }
    public decimal? AccountAmount { get; set; }
    public string? AccountCurrency { get; set; }

    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateOnly IncomeDate { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public List<CurrencyExchangeResponse>? CurrencyExchanges { get; set; }

    /// <summary>Indicates whether the movement has registered splits. Useful for showing an indicator in the list.</summary>
    public bool HasSplits { get; set; }

    /// <summary>Splits among partners. Present only when the project has partners_enabled = true.</summary>
    public List<SplitResponseDto>? Splits { get; set; }
}

/// <summary>
/// Response from the AI extraction endpoint to pre-fill the income form.
/// </summary>
public class ExtractIncomeFromDocumentResponse
{
    public string Provider { get; set; } = null!;
    public string DocumentKind { get; set; } = null!;
    public string ModelId { get; set; } = null!;
    public IncomeDocumentDraftResponse Draft { get; set; } = new();
    public SuggestedIncomeCategoryResponse? SuggestedCategory { get; set; }
    public SuggestedIncomePaymentMethodResponse? SuggestedPaymentMethod { get; set; }
    public List<IncomeCategoryOptionResponse> AvailableCategories { get; set; } = [];
    public List<IncomePaymentMethodOptionResponse> AvailablePaymentMethods { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public class IncomeDocumentDraftResponse
{
    public Guid? CategoryId { get; set; }
    public Guid? PaymentMethodId { get; set; }
    public decimal? OriginalAmount { get; set; }
    public string? OriginalCurrency { get; set; }
    public decimal? ExchangeRate { get; set; }
    public decimal? ConvertedAmount { get; set; }
    public decimal? AccountAmount { get; set; }
    public string? AccountCurrency { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateOnly IncomeDate { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? Notes { get; set; }
    public List<CurrencyExchangeRequest>? CurrencyExchanges { get; set; }
    public string? DetectedMerchantName { get; set; }
    public string? DetectedPaymentMethodText { get; set; }
}

public class SuggestedIncomeCategoryResponse
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = null!;
    public decimal Confidence { get; set; }
    public string Reason { get; set; } = null!;
}

public class SuggestedIncomePaymentMethodResponse
{
    public Guid PaymentMethodId { get; set; }
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public decimal Confidence { get; set; }
    public string Reason { get; set; } = null!;
}

public class IncomeCategoryOptionResponse
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = null!;
    public bool IsDefault { get; set; }
}

public class IncomePaymentMethodOptionResponse
{
    public Guid PaymentMethodId { get; set; }
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
}
