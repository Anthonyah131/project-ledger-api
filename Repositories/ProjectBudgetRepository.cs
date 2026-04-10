using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

/// <summary>
/// Repository implementation for ProjectBudget operations.
/// </summary>
public class ProjectBudgetRepository : Repository<ProjectBudget>, IProjectBudgetRepository
{
    public ProjectBudgetRepository(AppDbContext context) : base(context) { }

    public async Task<ProjectBudget?> GetActiveByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(
            b => b.PjbProjectId == projectId && !b.PjbIsDeleted, ct);
}
