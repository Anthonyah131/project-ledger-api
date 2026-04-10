using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Partner;

namespace ProjectLedger.API.Services;

public interface IPartnerBalanceService
{
    /// <summary>
    /// Calculates the current balances for all partners in a project, 
    /// including individual totals and pairwise debt relationships.
    /// </summary>
    Task<PartnerBalanceResponse> GetBalancesAsync(Guid projectId, string projectCurrency, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the transaction and settlement history for a specific partner in a project.
    /// </summary>
    Task<PartnerHistoryResponse> GetPartnerHistoryAsync(
        Guid projectId, Guid partnerId,
        PagedRequest pagination,
        CancellationToken ct = default);

    /// <summary>
    /// Generates suggested settlements to minimize the number of payments required 
    /// to clear all individual net balances in a project.
    /// </summary>
    Task<SettlementSuggestionsResponse> GetSettlementSuggestionsAsync(
        Guid projectId, string projectCurrency, CancellationToken ct = default);
}
