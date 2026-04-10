using System.ComponentModel.DataAnnotations;
using ProjectLedger.API.DTOs.Common;

namespace ProjectLedger.API.DTOs.Split;

/// <summary>Explicit split sent by the frontend when creating or updating a transaction.</summary>
public class SplitInputDto
{
    [Required]
    public Guid PartnerId { get; set; }

    [Required]
    [RegularExpression("^(percentage|fixed)$", ErrorMessage = "Split type must be 'percentage' or 'fixed'.")]
    public string SplitType { get; set; } = null!;

    [Range(0.0001, 9999999999.9999, ErrorMessage = "Split value must be between 0.0001 and 9,999,999,999.9999.")]
    public decimal SplitValue { get; set; }

    /// <summary>Resolved amount of the split in the original currency of the transaction. Calculated by the frontend.</summary>
    [Range(0.01, 999999999999.99, ErrorMessage = "Resolved amount must be between 0.01 and 999,999,999,999.99.")]
    public decimal ResolvedAmount { get; set; }

    /// <summary>Equivalents in alternative currencies of the project. Calculated by the frontend, same as in the parent movement.</summary>
    public List<CurrencyExchangeRequest>? CurrencyExchanges { get; set; }
}

/// <summary>Split returned in the detail response of an expense or income.</summary>
public class SplitResponseDto
{
    public Guid PartnerId { get; set; }
    public string PartnerName { get; set; } = null!;
    public string SplitType { get; set; } = null!;
    public decimal SplitValue { get; set; }
    public decimal ResolvedAmount { get; set; }
    /// <summary>Equivalents of the split amount in the project's alternative currencies. Null if no alternative currencies are configured.</summary>
    public List<CurrencyExchangeResponse>? CurrencyExchanges { get; set; }
}
