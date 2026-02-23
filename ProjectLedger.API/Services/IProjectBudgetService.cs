using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IProjectBudgetService
{
    Task<ProjectBudget?> GetActiveByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<ProjectBudget> CreateAsync(ProjectBudget budget, CancellationToken ct = default);
    Task UpdateAsync(ProjectBudget budget, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default);
}
