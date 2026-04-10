using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

/// <summary>
/// Repository interface for ProjectBudget operations.
/// </summary>
public interface IProjectBudgetRepository : IRepository<ProjectBudget>
{
    Task<ProjectBudget?> GetActiveByProjectIdAsync(Guid projectId, CancellationToken ct = default);
}
