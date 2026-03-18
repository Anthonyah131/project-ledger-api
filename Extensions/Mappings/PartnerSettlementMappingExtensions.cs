using ProjectLedger.API.DTOs.Partner;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Extensions.Mappings;

public static class PartnerSettlementMappingExtensions
{
    public static SettlementResponse ToResponse(this PartnerSettlement s)
        => new(
            Id: s.PstId,
            ProjectId: s.PstProjectId,
            FromPartnerId: s.PstFromPartnerId,
            FromPartnerName: s.FromPartner.PtrName,
            ToPartnerId: s.PstToPartnerId,
            ToPartnerName: s.ToPartner.PtrName,
            Amount: s.PstAmount,
            Currency: s.PstCurrency,
            ExchangeRate: s.PstExchangeRate,
            ConvertedAmount: s.PstConvertedAmount,
            SettlementDate: s.PstSettlementDate,
            Description: s.PstDescription,
            Notes: s.PstNotes,
            CreatedAt: s.PstCreatedAt,
            CurrencyExchanges: s.CurrencyExchanges
                .Select(ce => new SettlementCurrencyExchangeItem(ce.SceCurrencyCode, ce.SceExchangeRate, ce.SceConvertedAmount))
                .ToList()
        );

    public static PartnerSettlement ToEntity(this CreateSettlementRequest r, Guid projectId, Guid createdByUserId)
        => new()
        {
            PstProjectId = projectId,
            PstFromPartnerId = r.FromPartnerId,
            PstToPartnerId = r.ToPartnerId,
            PstAmount = r.Amount,
            PstCurrency = r.Currency,
            PstExchangeRate = r.ExchangeRate,
            PstConvertedAmount = 0,  // calculated in service
            PstSettlementDate = r.SettlementDate,
            PstDescription = r.Description,
            PstNotes = r.Notes,
            PstCreatedByUserId = createdByUserId
        };
}
