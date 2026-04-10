using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IProjectBudgetService
{
    /// <summary>
    /// Retrieves the current active budget for a project.
    /// </summary>
    Task<ProjectBudget?> GetActiveByProjectIdAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new budget configuration for a project.
    /// </summary>
    Task<ProjectBudget> CreateAsync(ProjectBudget budget, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing active budget.
    /// </summary>
    Task UpdateAsync(ProjectBudget budget, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a budget configuration.
    /// </summary>
    Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default);
}
