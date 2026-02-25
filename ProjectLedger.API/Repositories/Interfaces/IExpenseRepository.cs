using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public interface IExpenseRepository : IRepository<Expense>
{
    Task<IEnumerable<Expense>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<IEnumerable<Expense>> GetByProjectIdAsync(Guid projectId, bool includeDeleted, CancellationToken ct = default);
    Task<(IReadOnlyList<Expense> Items, int TotalCount)> GetByProjectIdPagedAsync(Guid projectId, bool includeDeleted, int skip, int take, string? sortBy, bool descending, CancellationToken ct = default);
    Task<IEnumerable<Expense>> GetByCategoryIdAsync(Guid categoryId, CancellationToken ct = default);
    Task<IEnumerable<Expense>> GetByObligationIdAsync(Guid obligationId, CancellationToken ct = default);
    Task<IEnumerable<Expense>> GetTemplatesByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<IEnumerable<Expense>> GetByPaymentMethodIdAsync(Guid paymentMethodId, CancellationToken ct = default);
    Task<(IReadOnlyList<Expense> Items, int TotalCount)> GetByPaymentMethodIdPagedAsync(Guid paymentMethodId, int skip, int take, string? sortBy, bool descending, CancellationToken ct = default);
    Task<IEnumerable<Expense>> GetByProjectIdWithDetailsAsync(Guid projectId, CancellationToken ct = default);
    Task<decimal> GetSpentAmountByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<Dictionary<Guid, decimal>> GetPaidAmountsByObligationIdsAsync(IEnumerable<Guid> obligationIds, CancellationToken ct = default);
}
