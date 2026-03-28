using System.ComponentModel.DataAnnotations;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Split;

namespace ProjectLedger.API.DTOs.Income;

/// <summary>
/// Request para importación rápida de hasta 100 ingresos.
/// Cada item contiene todos sus campos — el frontend los llena manualmente.
/// El backend no calcula ni deriva ningún campo.
/// Operación all-or-nothing: si algún item falla validación, no se crea ninguno.
/// </summary>
public class BulkCreateIncomeRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one item is required.")]
    [MaxLength(100, ErrorMessage = "A maximum of 100 items can be imported at once.")]
    public List<BulkIncomeItemRequest> Items { get; set; } = [];
}

/// <summary>
/// Un ingreso individual dentro del lote. Contiene todos los campos que el usuario
/// llenó manualmente en la vista de importación rápida.
/// </summary>
public class BulkIncomeItemRequest
{
    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    public Guid PaymentMethodId { get; set; }

    // ── Montos ──────────────────────────────────────────────

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

    // ── Datos descriptivos ──────────────────────────────────

    [Required]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 255 characters.")]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Conversiones a monedas alternativas del proyecto.
    /// El frontend calcula y envía el monto convertido para cada moneda.
    /// </summary>
    public List<CurrencyExchangeRequest>? CurrencyExchanges { get; set; }

    /// <summary>
    /// Splits entre partners. Si el proyecto tiene partners_enabled = true
    /// y el usuario configuró splits manualmente, se envían aquí.
    /// </summary>
    public List<SplitInputDto>? Splits { get; set; }
}

// ── Responses ───────────────────────────────────────────────

public class BulkCreateIncomeResponse
{
    public int Created { get; set; }
    public List<BulkCreatedIncomeItemResponse> Items { get; set; } = [];
}

public class BulkCreatedIncomeItemResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public decimal OriginalAmount { get; set; }
    public decimal ConvertedAmount { get; set; }
    public DateOnly Date { get; set; }
}
