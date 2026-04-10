using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

/// <summary>
/// Repository interface for ExpenseSplit operations.
/// </summary>
public interface IExpenseSplitRepository : IRepository<ExpenseSplit>
{
    Task<IEnumerable<ExpenseSplit>> GetByExpenseIdAsync(Guid expenseId, CancellationToken ct = default);
    Task DeleteByExpenseIdAsync(Guid expenseId, CancellationToken ct = default);
    Task<bool> ExistsForProjectAsync(Guid projectId, CancellationToken ct = default);
}
