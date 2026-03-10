using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class ProjectRepository : Repository<Project>, IProjectRepository
{
    public ProjectRepository(AppDbContext context) : base(context) { }

    public override async Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(
            p => p.PrjId == id && !p.PrjIsDeleted, ct);

    public async Task<IEnumerable<Project>> GetByOwnerUserIdAsync(Guid userId, CancellationToken ct = default)
        => await DbSet
            .Where(p => p.PrjOwnerUserId == userId && !p.PrjIsDeleted)
            .OrderByDescending(p => p.PrjCreatedAt)
            .ToListAsync(ct);

    public async Task<IEnumerable<Project>> GetByMemberUserIdAsync(Guid userId, CancellationToken ct = default)
        => await DbSet
            .Where(p => !p.PrjIsDeleted &&
                        p.Members.Any(m => m.PrmUserId == userId && !m.PrmIsDeleted))
            .OrderByDescending(p => p.PrjCreatedAt)
            .ToListAsync(ct);
}
