using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.Common;

/// <summary>
/// Request para un valor de tipo de cambio a moneda alternativa.
/// Usado en la creación/actualización de gastos e ingresos.
/// </summary>
public class CurrencyExchangeRequest
{
    [Required]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "CurrencyCode must be a 3-character ISO 4217 code.")]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "CurrencyCode must be uppercase ISO 4217 (e.g. USD, EUR, CRC).")]
    public string CurrencyCode { get; set; } = null!;

    [Required]
    [Range(0.000001, 999999999999.999999, ErrorMessage = "ExchangeRate must be greater than 0.")]
    public decimal ExchangeRate { get; set; }

    [Required]
    [Range(0.01, 99999999999999.99, ErrorMessage = "ConvertedAmount must be greater than 0.")]
    public decimal ConvertedAmount { get; set; }
}

/// <summary>
/// Response con un valor de tipo de cambio a moneda alternativa.
/// </summary>
public class CurrencyExchangeResponse
{
    public Guid Id { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public decimal ExchangeRate { get; set; }
    public decimal ConvertedAmount { get; set; }
}
