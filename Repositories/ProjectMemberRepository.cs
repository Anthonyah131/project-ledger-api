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
}
