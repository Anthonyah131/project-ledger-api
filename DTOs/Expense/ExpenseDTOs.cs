using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using ProjectLedger.API.DTOs.Common;

namespace ProjectLedger.API.DTOs.Expense;

// ── Requests ────────────────────────────────────────────────

/// <summary>
/// Request para crear un gasto.
/// NO incluye ProjectId (viene de la ruta) ni CreatedByUserId (viene del JWT).
/// Esto previene escalamiento de privilegios.
/// </summary>
public class CreateExpenseRequest
{
    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    public Guid PaymentMethodId { get; set; }

    /// <summary>FK → obligations. NULL = gasto normal; valor = pago de deuda.</summary>
    public Guid? ObligationId { get; set; }

    /// <summary>
    /// Monto equivalente en la moneda de la obligación.
    /// Se usa cuando ObligationId está presente y OriginalCurrency difiere de la moneda de la obligación.
    /// </summary>
    [Range(0.01, 99999999999999.99, ErrorMessage = "ObligationEquivalentAmount must be greater than 0.")]
    public decimal? ObligationEquivalentAmount { get; set; }

    // ── Montos ──────────────────────────────────────────────

    [Required]
    [Range(0.01, 99999999999999.99, ErrorMessage = "OriginalAmount must be greater than 0.")]
    public decimal OriginalAmount { get; set; }

    [Required]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "OriginalCurrency must be a 3-character ISO 4217 code.")]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "OriginalCurrency must be uppercase ISO 4217 (e.g. USD, EUR, CRC).")]
    public string OriginalCurrency { get; set; } = null!;

    [Range(0.000001, 999999999999.999999, ErrorMessage = "ExchangeRate must be greater than 0.")]
    public decimal ExchangeRate { get; set; } = 1.000000m;

    /// <summary>
    /// Monto convertido a la moneda del proyecto. Es el valor que se usa para totales y cálculos.
    /// El front es responsable de enviarlo calculado.
    /// </summary>
    [Required]
    [Range(0.01, 99999999999999.99, ErrorMessage = "ConvertedAmount must be greater than 0.")]
    public decimal ConvertedAmount { get; set; }

    // ── Datos descriptivos ──────────────────────────────────

    [Required]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 255 characters.")]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    [Required]
    public DateOnly ExpenseDate { get; set; }

    [StringLength(100, ErrorMessage = "ReceiptNumber cannot exceed 100 characters.")]
    public string? ReceiptNumber { get; set; }

    public string? Notes { get; set; }
    public bool IsTemplate { get; set; }

    /// <summary>
    /// Conversiones a monedas alternativas del proyecto.
    /// El front calcula y envía el monto convertido para cada moneda.
    /// </summary>
    public List<CurrencyExchangeRequest>? CurrencyExchanges { get; set; }
}

/// <summary>Request para actualizar un gasto.</summary>
public class UpdateExpenseRequest
{
    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    public Guid PaymentMethodId { get; set; }

    /// <summary>FK → obligations. NULL = gasto normal; valor = pago de deuda.</summary>
    public Guid? ObligationId { get; set; }

    /// <summary>
    /// Monto equivalente en la moneda de la obligación.
    /// Se usa cuando el gasto está vinculado a obligación y la moneda original difiere.
    /// </summary>
    [Range(0.01, 99999999999999.99, ErrorMessage = "ObligationEquivalentAmount must be greater than 0.")]
    public decimal? ObligationEquivalentAmount { get; set; }

    [Required]
    [Range(0.01, 99999999999999.99, ErrorMessage = "OriginalAmount must be greater than 0.")]
    public decimal OriginalAmount { get; set; }

    [Required]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "OriginalCurrency must be a 3-character ISO 4217 code.")]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "OriginalCurrency must be uppercase ISO 4217 (e.g. USD, EUR, CRC).")]
    public string OriginalCurrency { get; set; } = null!;

    [Range(0.000001, 999999999999.999999, ErrorMessage = "ExchangeRate must be greater than 0.")]
    public decimal ExchangeRate { get; set; } = 1.000000m;

    /// <summary>
    /// Monto convertido a la moneda del proyecto. Es el valor que se usa para totales y cálculos.
    /// El front es responsable de enviarlo calculado.
    /// </summary>
    [Required]
    [Range(0.01, 99999999999999.99, ErrorMessage = "ConvertedAmount must be greater than 0.")]
    public decimal ConvertedAmount { get; set; }

    [Required]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 255 characters.")]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    [Required]
    public DateOnly ExpenseDate { get; set; }

    [StringLength(100, ErrorMessage = "ReceiptNumber cannot exceed 100 characters.")]
    public string? ReceiptNumber { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Si viene con valor se actualiza el estado plantilla.
    /// Si viene null se conserva el valor actual.
    /// </summary>
    public bool? IsTemplate { get; set; }

    /// <summary>
    /// Conversiones a monedas alternativas del proyecto.
    /// Si es null, no se modifican los exchanges existentes.
    /// Si es lista vacía, se eliminan todos los exchanges.
    /// </summary>
    public List<CurrencyExchangeRequest>? CurrencyExchanges { get; set; }
}

/// <summary>
/// Request para crear un gasto real a partir de una plantilla.
/// Permite sobreescribir monto, fecha y obligación;
/// el resto se toma de la plantilla.
/// </summary>
public class CreateFromTemplateRequest
{
    [Range(0.01, 99999999999999.99, ErrorMessage = "OriginalAmount must be greater than 0.")]
    public decimal? OriginalAmount { get; set; }

    [Range(0.01, 99999999999999.99, ErrorMessage = "ConvertedAmount must be greater than 0.")]
    public decimal? ConvertedAmount { get; set; }

    public DateOnly? ExpenseDate { get; set; }
    public Guid? ObligationId { get; set; }
    [Range(0.01, 99999999999999.99, ErrorMessage = "ObligationEquivalentAmount must be greater than 0.")]
    public decimal? ObligationEquivalentAmount { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Request para extraer un borrador de gasto desde imagen/PDF con Azure Document Intelligence.
/// </summary>
public class ExtractExpenseFromDocumentRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;

    [RegularExpression("^(receipt|invoice)$", ErrorMessage = "DocumentKind must be 'receipt' or 'invoice'.")]
    public string DocumentKind { get; set; } = "receipt";
}

// ── Responses ───────────────────────────────────────────────

/// <summary>Respuesta con los datos de un gasto.</summary>
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

    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? Notes { get; set; }
    public bool IsTemplate { get; set; }

    public List<CurrencyExchangeResponse>? CurrencyExchanges { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // ── Soft delete info (solo visible con includeDeleted=true) ──
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }
}

/// <summary>
/// Respuesta del endpoint de extraccion IA para pre-llenar el formulario de gasto.
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
