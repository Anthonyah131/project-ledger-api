using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

/// <summary>
/// Repository interface for PaymentMethod operations.
/// </summary>
public interface IPaymentMethodRepository : IRepository<PaymentMethod>
{
    Task<IEnumerable<PaymentMethod>> GetByOwnerUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<(IEnumerable<PaymentMethod> Items, int TotalCount)> GetByOwnerUserIdPagedWithSearchAsync(
        Guid userId, string? search, int skip, int take, CancellationToken ct = default);
    Task<(decimal TotalIncome, decimal TotalExpenses)> GetProjectBalanceAsync(Guid pmtId, Guid projectId, CancellationToken ct = default);
    Task<bool> IsLinkedToAnyProjectAsync(Guid pmtId, CancellationToken ct = default);
}
