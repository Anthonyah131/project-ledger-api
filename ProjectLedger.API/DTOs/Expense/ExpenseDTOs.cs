using System.ComponentModel.DataAnnotations;

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

    // ── Moneda alternativa (opcional) ───────────────────────

    [StringLength(3, MinimumLength = 3, ErrorMessage = "AltCurrency must be a 3-character ISO 4217 code.")]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "AltCurrency must be uppercase ISO 4217 (e.g. USD, EUR, CRC).")]
    public string? AltCurrency { get; set; }

    [Range(0.000001, 999999999999.999999, ErrorMessage = "AltExchangeRate must be greater than 0.")]
    public decimal? AltExchangeRate { get; set; }

    [Range(0.01, 99999999999999.99, ErrorMessage = "AltAmount must be greater than 0.")]
    public decimal? AltAmount { get; set; }
}

/// <summary>Request para actualizar un gasto.</summary>
public class UpdateExpenseRequest
{
    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    public Guid PaymentMethodId { get; set; }

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

    [StringLength(3, MinimumLength = 3, ErrorMessage = "AltCurrency must be a 3-character ISO 4217 code.")]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "AltCurrency must be uppercase ISO 4217 (e.g. USD, EUR, CRC).")]
    public string? AltCurrency { get; set; }

    [Range(0.000001, 999999999999.999999, ErrorMessage = "AltExchangeRate must be greater than 0.")]
    public decimal? AltExchangeRate { get; set; }

    [Range(0.01, 99999999999999.99, ErrorMessage = "AltAmount must be greater than 0.")]
    public decimal? AltAmount { get; set; }
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

    [Range(0.01, 99999999999999.99, ErrorMessage = "AltAmount must be greater than 0.")]
    public decimal? AltAmount { get; set; }

    public DateOnly? ExpenseDate { get; set; }
    public Guid? ObligationId { get; set; }
    public string? Notes { get; set; }
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

    public string? AltCurrency { get; set; }
    public decimal? AltExchangeRate { get; set; }
    public decimal? AltAmount { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // ── Soft delete info (solo visible con includeDeleted=true) ──
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }
}
