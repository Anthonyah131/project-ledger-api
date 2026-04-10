using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

/// <summary>
/// Repository interface for IncomeSplit operations.
/// </summary>
public interface IIncomeSplitRepository : IRepository<IncomeSplit>
{
    Task<IEnumerable<IncomeSplit>> GetByIncomeIdAsync(Guid incomeId, CancellationToken ct = default);
    Task DeleteByIncomeIdAsync(Guid incomeId, CancellationToken ct = default);
    Task<bool> ExistsForProjectAsync(Guid projectId, CancellationToken ct = default);
}
