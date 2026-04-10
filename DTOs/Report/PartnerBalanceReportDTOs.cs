using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Partner;

namespace ProjectLedger.API.DTOs.Report;

// ── Partner Balance Report ───────────────────────────────────

/// <summary>
/// Partner balances report for a project.
/// Uses the same structure and logic as GET /projects/:id/partners/balance.
/// Requires PrjPartnersEnabled + CanUseAdvancedReports.
/// </summary>
public class PartnerBalanceReportResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public DateTime GeneratedAt { get; set; }

    /// <summary>Same data as GET /projects/:id/partners/balance → partners[].</summary>
    public List<PartnerBalanceItem> Partners { get; set; } = [];

    /// <summary>Same data as GET /projects/:id/partners/balance → pairwiseBalances[].</summary>
    public List<PairwiseBalanceItem> PairwiseBalances { get; set; } = [];

    /// <summary>Detailed list of settlements (for export).</summary>
    public List<SettlementRow> Settlements { get; set; } = [];

    /// <summary>Splits without currency exchanges when others in the project have them.</summary>
    public List<MissingCurrencyExchangeWarning> Warnings { get; set; } = [];
}

public class SettlementRow
{
    public Guid SettlementId { get; set; }
    public Guid FromPartnerId { get; set; }
    public string FromPartnerName { get; set; } = null!;
    public Guid ToPartnerId { get; set; }
    public string ToPartnerName { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = null!;
    public decimal ExchangeRate { get; set; }
    public decimal ConvertedAmount { get; set; }
    public DateOnly SettlementDate { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public List<CurrencyExchangeResponse>? CurrencyExchanges { get; set; }
}
