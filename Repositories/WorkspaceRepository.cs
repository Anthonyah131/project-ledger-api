using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class WorkspaceRepository : Repository<Workspace>, IWorkspaceRepository
{
    public WorkspaceRepository(AppDbContext context) : base(context) { }

    public override async Task<Workspace?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .FirstOrDefaultAsync(w => w.WksId == id && !w.WksIsDeleted, ct);

    public async Task<Workspace?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(w => w.Members.Where(m => !m.WkmIsDeleted))
                .ThenInclude(m => m.User)
            .Include(w => w.Projects.Where(p => !p.PrjIsDeleted))
            .FirstOrDefaultAsync(w => w.WksId == id && !w.WksIsDeleted, ct);

    public async Task<IEnumerable<Workspace>> GetByMemberUserIdAsync(Guid userId, CancellationToken ct = default)
        => await DbSet
            .Where(w => !w.WksIsDeleted
                && w.Members.Any(m => m.WkmUserId == userId && !m.WkmIsDeleted))
            .OrderBy(w => w.WksName)
            .ToListAsync(ct);

    public async Task<bool> HasActiveProjectsAsync(Guid workspaceId, CancellationToken ct = default)
        => await Context.Set<Project>()
            .AnyAsync(p => p.PrjWorkspaceId == workspaceId && !p.PrjIsDeleted, ct);

    public async Task<int> CountProjectsAsync(Guid workspaceId, CancellationToken ct = default)
        => await Context.Set<Project>()
            .CountAsync(p => p.PrjWorkspaceId == workspaceId && !p.PrjIsDeleted, ct);

    public async Task<string?> GetMemberRoleAsync(Guid workspaceId, Guid userId, CancellationToken ct = default)
        => await Context.Set<WorkspaceMember>()
            .Where(m => m.WkmWorkspaceId == workspaceId && m.WkmUserId == userId && !m.WkmIsDeleted)
            .Select(m => m.WkmRole)
            .FirstOrDefaultAsync(ct);

    public async Task<Workspace?> GetGeneralWorkspaceForUserAsync(Guid userId, CancellationToken ct = default)
        => await DbSet
            .Where(w => w.WksOwnerUserId == userId && w.WksName == "General" && !w.WksIsDeleted)
            .OrderBy(w => w.WksCreatedAt)
            .FirstOrDefaultAsync(ct);
}
