using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Partner;

namespace ProjectLedger.API.Services;

public interface IPartnerBalanceService
{
    Task<PartnerBalanceResponse> GetBalancesAsync(Guid projectId, string projectCurrency, CancellationToken ct = default);

    Task<PartnerHistoryResponse> GetPartnerHistoryAsync(
        Guid projectId, Guid partnerId,
        PagedRequest pagination,
        CancellationToken ct = default);

    Task<SettlementSuggestionsResponse> GetSettlementSuggestionsAsync(
        Guid projectId, string projectCurrency, CancellationToken ct = default);
}
