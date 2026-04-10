using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.Common;

/// <summary>
/// Request for an alternative-currency exchange rate value.
/// Used when creating or updating expenses and incomes.
/// </summary>
public class CurrencyExchangeRequest
{
    [Required]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency code must be a 3-character ISO 4217 code.")]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Currency code must be uppercase ISO 4217 (e.g. USD, EUR, CRC).")]
    public string CurrencyCode { get; set; } = null!;

    [Required]
    [Range(0.000001, 999999999999.999999, ErrorMessage = "Exchange rate must be greater than 0.")]
    public decimal ExchangeRate { get; set; }

    [Required]
    [Range(0.0001, 99999999999999.9999, ErrorMessage = "Converted amount must be between 0.0001 and 99,999,999,999,999.9999.")]
    public decimal ConvertedAmount { get; set; }
}

/// <summary>
/// Response containing an alternative-currency exchange rate value.
/// </summary>
public class CurrencyExchangeResponse
{
    public Guid Id { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public decimal ExchangeRate { get; set; }
    public decimal ConvertedAmount { get; set; }
}
