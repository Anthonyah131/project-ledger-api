using System.ComponentModel.DataAnnotations;
using ProjectLedger.API.DTOs.Common;

namespace ProjectLedger.API.DTOs.Partner;

// ── POST /projects/:id/partner-settlements ────────────────

public class CreateSettlementRequest
{
    [Required]
    public Guid FromPartnerId { get; set; }

    [Required]
    public Guid ToPartnerId { get; set; }

    [Required]
    [Range(0.01, 999999999999.99, ErrorMessage = "Amount must be between 0.01 and 999,999,999,999.99.")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency must be a 3-character ISO 4217 code.")]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Currency must be uppercase ISO 4217 (e.g. USD, EUR, CRC).")]
    public string Currency { get; set; } = null!;

    [Range(0.000001, 999999999999.999999, ErrorMessage = "Exchange rate must be between 0.000001 and 999,999,999,999.999999.")]
    public decimal ExchangeRate { get; set; } = 1.000000m;

    [Required]
    public DateOnly SettlementDate { get; set; }

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Settlement amount in alternative currencies of the project.
    /// If omitted or null, no alternative conversions are saved.
    /// </summary>
    public List<CurrencyExchangeRequest>? CurrencyExchanges { get; set; }
}

// ── PATCH /projects/:id/partner-settlements/:id ───────────

public class UpdateSettlementRequest
{
    [Range(0.01, 999999999999.99, ErrorMessage = "Amount must be between 0.01 and 999,999,999,999.99.")]
    public decimal? Amount { get; set; }

    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency must be a 3-character ISO 4217 code.")]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Currency must be uppercase ISO 4217 (e.g. USD, EUR, CRC).")]
    public string? Currency { get; set; }

    [Range(0.000001, 999999999999.999999, ErrorMessage = "Exchange rate must be between 0.000001 and 999,999,999,999.999999.")]
    public decimal? ExchangeRate { get; set; }

    public DateOnly? SettlementDate { get; set; }

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// If provided (even an empty list), replaces all existing conversions.
    /// If omitted (null), does not modify existing conversions.
    /// </summary>
    public List<CurrencyExchangeRequest>? CurrencyExchanges { get; set; }
}

// ── GET /projects/:id/partner-settlements (list item) ─────

public record SettlementResponse(
    Guid Id,
    Guid ProjectId,
    Guid FromPartnerId,
    string FromPartnerName,
    Guid ToPartnerId,
    string ToPartnerName,
    decimal Amount,
    string Currency,
    decimal ExchangeRate,
    decimal ConvertedAmount,
    DateOnly SettlementDate,
    string? Description,
    string? Notes,
    DateTime CreatedAt,
    IReadOnlyList<SettlementCurrencyExchangeItem> CurrencyExchanges
);

public record SettlementCurrencyExchangeItem(
    string CurrencyCode,
    decimal ExchangeRate,
    decimal ConvertedAmount
);
