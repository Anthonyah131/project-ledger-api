using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Partner;
using ProjectLedger.API.DTOs.Report;

namespace ProjectLedger.API.Services;

public partial class ReportService
{
    public async Task<PartnerBalanceReportResponse> GetPartnerBalancesAsync(
        Guid projectId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var project = await _projectService.GetByIdAsync(projectId, ct)
            ?? throw new KeyNotFoundException("ProjectNotFound");

        if (!project.PrjPartnersEnabled)
            throw new InvalidOperationException("PartnersNotEnabled");

        // Delegar al mismo servicio que usa el endpoint /partners/balance
        // Garantiza consistencia total con lo que ve el frontend
        var balances = await _partnerBalanceService.GetBalancesAsync(
            projectId, project.PrjCurrencyCode, ct);

        // Cargar settlements para incluirlos como detalle exportable
        var allSettlements = await _settlementRepo.GetByProjectIdAsync(projectId, ct);
        var settlements = allSettlements
            .Where(s => from is null || s.PstSettlementDate >= from.Value)
            .Where(s => to is null || s.PstSettlementDate <= to.Value)
            .Select(s => new SettlementRow
            {
                SettlementId = s.PstId,
                FromPartnerId = s.PstFromPartnerId,
                FromPartnerName = s.FromPartner?.PtrName ?? "Unknown",
                ToPartnerId = s.PstToPartnerId,
                ToPartnerName = s.ToPartner?.PtrName ?? "Unknown",
                Amount = s.PstAmount,
                Currency = s.PstCurrency,
                ExchangeRate = s.PstExchangeRate,
                ConvertedAmount = s.PstConvertedAmount,
                SettlementDate = s.PstSettlementDate,
                Description = s.PstDescription,
                Notes = s.PstNotes,
                CurrencyExchanges = s.CurrencyExchanges?.Count > 0
                    ? s.CurrencyExchanges.Select(ce => new CurrencyExchangeResponse
                    {
                        Id = ce.SceId,
                        CurrencyCode = ce.SceCurrencyCode,
                        ExchangeRate = ce.SceExchangeRate,
                        ConvertedAmount = ce.SceConvertedAmount
                    }).ToList()
                    : null
            })
            .ToList();

        return new PartnerBalanceReportResponse
        {
            ProjectId = project.PrjId,
            ProjectName = project.PrjName,
            CurrencyCode = project.PrjCurrencyCode,
            DateFrom = from,
            DateTo = to,
            GeneratedAt = DateTime.UtcNow,
            Partners = balances.Partners.ToList(),
            PairwiseBalances = balances.PairwiseBalances.ToList(),
            Settlements = settlements,
            Warnings = balances.Warnings.ToList()
        };
    }
}
