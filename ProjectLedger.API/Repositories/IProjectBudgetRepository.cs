using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public interface IProjectBudgetRepository : IRepository<ProjectBudget>
{
    Task<ProjectBudget?> GetActiveByProjectIdAsync(Guid projectId, CancellationToken ct = default);
}
