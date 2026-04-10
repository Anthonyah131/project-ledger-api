using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Partner;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IPartnerSettlementService
{
    /// <summary>
    /// Gets a paginated list of settlements for a specific project.
    /// </summary>
    Task<(IReadOnlyList<PartnerSettlement> Items, int TotalCount)> GetPagedByProjectIdAsync(
        Guid projectId, int skip, int take, CancellationToken ct = default);

    /// <summary>
    /// Creates a new settlement between two partners and saves related currency exchanges.
    /// </summary>
    Task<PartnerSettlement> CreateAsync(
        PartnerSettlement settlement,
        IReadOnlyList<CurrencyExchangeRequest>? currencyExchanges = null,
        CancellationToken ct = default);

    /// <summary>
    /// Updates an existing settlement and its currency exchanges.
    /// </summary>
    Task<PartnerSettlement> UpdateAsync(
        Guid settlementId, Guid projectId,
        UpdateSettlementRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a settlement.
    /// </summary>
    Task SoftDeleteAsync(Guid settlementId, Guid projectId, Guid deletedByUserId, CancellationToken ct = default);
}
