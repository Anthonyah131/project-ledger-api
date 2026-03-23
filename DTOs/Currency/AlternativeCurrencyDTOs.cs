using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.Currency;

/// <summary>Request para agregar una moneda alternativa a un proyecto.</summary>
public class AddAlternativeCurrencyRequest
{
    [Required]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency code must be a 3-character ISO 4217 code.")]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Currency code must be uppercase ISO 4217 (e.g. USD, EUR, CRC).")]
    public string CurrencyCode { get; set; } = null!;
}

/// <summary>Respuesta con una moneda alternativa de un proyecto.</summary>
public class ProjectAlternativeCurrencyResponse
{
    public Guid Id { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public string CurrencyName { get; set; } = null!;
    public string CurrencySymbol { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

/// <summary>Respuesta de consulta de tipo de cambio.</summary>
public class ExchangeRateResponse
{
    public string BaseCurrency { get; set; } = null!;
    public string TargetCurrency { get; set; } = null!;
    public decimal Rate { get; set; }
    public decimal? Amount { get; set; }
    public decimal? ConvertedAmount { get; set; }
    public DateOnly Date { get; set; }
}

/// <summary>Respuesta con múltiples tasas de cambio para una moneda base.</summary>
public class ExchangeRateLatestResponse
{
    public string BaseCurrency { get; set; } = null!;
    public DateOnly Date { get; set; }
    public Dictionary<string, decimal> Rates { get; set; } = new();
}
