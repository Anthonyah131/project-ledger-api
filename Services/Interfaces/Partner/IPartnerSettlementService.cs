using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Partner;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IPartnerSettlementService
{
    Task<(IReadOnlyList<PartnerSettlement> Items, int TotalCount)> GetPagedByProjectIdAsync(
        Guid projectId, int skip, int take, CancellationToken ct = default);

    Task<PartnerSettlement> CreateAsync(
        PartnerSettlement settlement,
        IReadOnlyList<CurrencyExchangeRequest>? currencyExchanges = null,
        CancellationToken ct = default);

    Task<PartnerSettlement> UpdateAsync(
        Guid settlementId, Guid projectId,
        UpdateSettlementRequest request,
        CancellationToken ct = default);

    Task SoftDeleteAsync(Guid settlementId, Guid projectId, Guid deletedByUserId, CancellationToken ct = default);
}
