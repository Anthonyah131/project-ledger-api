using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public interface IIncomeRepository : IRepository<Income>
{
    Task<IEnumerable<Income>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<IEnumerable<Income>> GetByProjectIdAsync(Guid projectId, bool includeDeleted, CancellationToken ct = default);
    Task<(IReadOnlyList<Income> Items, int TotalCount)> GetByProjectIdPagedAsync(Guid projectId, bool includeDeleted, bool? isActive, int skip, int take, string? sortBy, bool descending, CancellationToken ct = default);
    Task<IEnumerable<Income>> GetByCategoryIdAsync(Guid categoryId, CancellationToken ct = default);
    Task<IEnumerable<Income>> GetByPaymentMethodIdAsync(Guid paymentMethodId, CancellationToken ct = default);
    Task<(IReadOnlyList<Income> Items, int TotalCount)> GetByPaymentMethodIdPagedAsync(Guid paymentMethodId, bool? isActive, int skip, int take, string? sortBy, bool descending, DateOnly? from, DateOnly? to, Guid? projectId, CancellationToken ct = default);
    Task<IEnumerable<Income>> GetByPaymentMethodIdsWithDetailsAsync(IEnumerable<Guid> paymentMethodIds, DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<decimal> GetTotalIncomeByProjectIdAsync(Guid projectId, CancellationToken ct = default);
}
