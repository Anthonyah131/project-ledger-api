using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IIncomeService
{
    Task<Income?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Income>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<IEnumerable<Income>> GetByProjectIdAsync(Guid projectId, bool includeDeleted, CancellationToken ct = default);
    Task<(IReadOnlyList<Income> Items, int TotalCount)> GetByProjectIdPagedAsync(Guid projectId, bool includeDeleted, bool? isActive, int skip, int take, string? sortBy, bool descending, CancellationToken ct = default);
    Task<IEnumerable<Income>> GetByPaymentMethodIdAsync(Guid paymentMethodId, CancellationToken ct = default);
    Task<(IReadOnlyList<Income> Items, int TotalCount, decimal TotalActiveAmount)> GetByPaymentMethodIdPagedAsync(Guid paymentMethodId, bool? isActive, int skip, int take, string? sortBy, bool descending, DateOnly? from, DateOnly? to, Guid? projectId, CancellationToken ct = default);
    Task<Income> CreateAsync(Income income, IReadOnlyList<SplitInput>? splits = null, CancellationToken ct = default);
    Task UpdateAsync(Income income, IReadOnlyList<SplitInput>? splits = null, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default);
}
