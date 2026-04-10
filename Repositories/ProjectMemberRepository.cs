using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class ProjectMemberRepository : Repository<ProjectMember>, IProjectMemberRepository
{
    public ProjectMemberRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<ProjectMember>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await DbSet
            .Include(m => m.User)
            .Where(m => m.PrmProjectId == projectId && !m.PrmIsDeleted)
            .ToListAsync(ct);

    public async Task<ProjectMember?> GetByProjectAndUserAsync(Guid projectId, Guid userId, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(
            m => m.PrmProjectId == projectId &&
                 m.PrmUserId == userId &&
                 !m.PrmIsDeleted, ct);

    public async Task<IEnumerable<ProjectMember>> GetPinnedByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await DbSet
            .Include(m => m.Project)
                .ThenInclude(p => p.Workspace)
            .Where(m => m.PrmUserId == userId &&
                        m.PrmIsPinned &&
                        !m.PrmIsDeleted &&
                        !m.Project.PrjIsDeleted)
            .OrderByDescending(m => m.PrmPinnedAt)
            .ToListAsync(ct);

    public async Task<IEnumerable<ProjectMember>> GetPinnedByUserIdWithSearchAsync(Guid userId, string? search, CancellationToken ct = default)
    {
        var query = DbSet
            .Include(m => m.Project)
                .ThenInclude(p => p.Workspace)
            .Where(m => m.PrmUserId == userId &&
                        m.PrmIsPinned &&
                        !m.PrmIsDeleted &&
                        !m.Project.PrjIsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(m => m.Project.PrjName.ToLower().Contains(search.ToLower()));

        return await query
            .OrderByDescending(m => m.PrmPinnedAt)
            .ToListAsync(ct);
    }

    public async Task<int> GetPinnedCountAsync(Guid userId, CancellationToken ct = default)
        => await DbSet
            .CountAsync(m => m.PrmUserId == userId &&
                             m.PrmIsPinned &&
                             !m.PrmIsDeleted, ct);
}
