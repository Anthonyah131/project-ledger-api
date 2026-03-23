using System.ComponentModel.DataAnnotations;
using ProjectLedger.API.DTOs.Common;

namespace ProjectLedger.API.DTOs.Split;

/// <summary>Split explícito enviado por el frontend al crear o actualizar un movimiento.</summary>
public class SplitInputDto
{
    [Required]
    public Guid PartnerId { get; set; }

    [Required]
    [RegularExpression("^(percentage|fixed)$", ErrorMessage = "Split type must be 'percentage' or 'fixed'.")]
    public string SplitType { get; set; } = null!;

    [Range(0.0001, 9999999999.9999, ErrorMessage = "Split value must be between 0.0001 and 9,999,999,999.9999.")]
    public decimal SplitValue { get; set; }

    /// <summary>Monto resuelto del split en la moneda original del movimiento. Calculado por el frontend.</summary>
    [Range(0.01, 999999999999.99, ErrorMessage = "Resolved amount must be between 0.01 and 999,999,999,999.99.")]
    public decimal ResolvedAmount { get; set; }

    /// <summary>Equivalencias en monedas alternativas del proyecto. Calculadas por el frontend, igual que en el movimiento padre.</summary>
    public List<CurrencyExchangeRequest>? CurrencyExchanges { get; set; }
}

/// <summary>Split retornado en la respuesta de detalle de un gasto o ingreso.</summary>
public class SplitResponseDto
{
    public Guid PartnerId { get; set; }
    public string PartnerName { get; set; } = null!;
    public string SplitType { get; set; } = null!;
    public decimal SplitValue { get; set; }
    public decimal ResolvedAmount { get; set; }
    /// <summary>Equivalencias del monto del split en las monedas alternativas del proyecto. Null si no hay monedas alternativas configuradas.</summary>
    public List<CurrencyExchangeResponse>? CurrencyExchanges { get; set; }
}
