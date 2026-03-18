using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

using ProjectLedger.API.DTOs.Common;

public interface IPartnerSettlementRepository : IRepository<PartnerSettlement>
{
    Task<(IReadOnlyList<PartnerSettlement> Items, int TotalCount)> GetPagedByProjectIdAsync(
        Guid projectId, int skip, int take, CancellationToken ct = default);

    Task<PartnerSettlement?> GetActiveByIdAsync(Guid settlementId, Guid projectId, CancellationToken ct = default);

    Task SaveCurrencyExchangesAsync(Guid settlementId, IReadOnlyList<CurrencyExchangeRequest> exchanges, CancellationToken ct = default);

    Task ReplaceCurrencyExchangesAsync(Guid settlementId, IReadOnlyList<CurrencyExchangeRequest> exchanges, CancellationToken ct = default);
}
