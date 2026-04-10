using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.Currency;

/// <summary>Request to add an alternative currency to a project.</summary>
public class AddAlternativeCurrencyRequest
{
    [Required]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency code must be a 3-character ISO 4217 code.")]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Currency code must be uppercase ISO 4217 (e.g. USD, EUR, CRC).")]
    public string CurrencyCode { get; set; } = null!;
}

/// <summary>Response with a project's alternative currency.</summary>
public class ProjectAlternativeCurrencyResponse
{
    public Guid Id { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public string CurrencyName { get; set; } = null!;
    public string CurrencySymbol { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

/// <summary>Exchange rate query response.</summary>
public class ExchangeRateResponse
{
    public string BaseCurrency { get; set; } = null!;
    public string TargetCurrency { get; set; } = null!;
    public decimal Rate { get; set; }
    public decimal? Amount { get; set; }
    public decimal? ConvertedAmount { get; set; }
    public DateOnly Date { get; set; }
}

/// <summary>Response with multiple exchange rates for a base currency.</summary>
public class ExchangeRateLatestResponse
{
    public string BaseCurrency { get; set; } = null!;
    public DateOnly Date { get; set; }
    public Dictionary<string, decimal> Rates { get; set; } = new();
}
