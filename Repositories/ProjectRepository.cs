using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class ProjectRepository : Repository<Project>, IProjectRepository
{
    public ProjectRepository(AppDbContext context) : base(context) { }

    public override async Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(p => p.Workspace)
            .FirstOrDefaultAsync(p => p.PrjId == id && !p.PrjIsDeleted, ct);

    public async Task<IEnumerable<Project>> GetByOwnerUserIdAsync(Guid userId, CancellationToken ct = default)
        => await DbSet
            .Include(p => p.Workspace)
            .Where(p => p.PrjOwnerUserId == userId && !p.PrjIsDeleted)
            .OrderByDescending(p => p.PrjCreatedAt)
            .ToListAsync(ct);

    public async Task<IEnumerable<Project>> GetByMemberUserIdAsync(Guid userId, CancellationToken ct = default)
        => await DbSet
            .Include(p => p.Workspace)
            .Where(p => !p.PrjIsDeleted &&
                        p.Members.Any(m => m.PrmUserId == userId && !m.PrmIsDeleted))
            .OrderByDescending(p => p.PrjCreatedAt)
            .ToListAsync(ct);

    public async Task<(IEnumerable<Project> Items, int TotalCount)> GetByUserIdPagedAsync(
        Guid userId, int skip, int take, string? sortBy = null, bool isDescending = true, CancellationToken ct = default)
    {
        var query = DbSet
            .Include(p => p.Workspace)
            .Where(p => !p.PrjIsDeleted &&
                        (p.PrjOwnerUserId == userId ||
                         p.Members.Any(m => m.PrmUserId == userId && !m.PrmIsDeleted)));

        var sorted = ApplySort(query, sortBy, isDescending);
        var total = await sorted.CountAsync(ct);
        var items = await sorted.Skip(skip).Take(take).ToListAsync(ct);
        return (items, total);
    }

    public async Task<(IEnumerable<Project> Items, int TotalCount)> GetByWorkspaceIdPagedAsync(
        Guid workspaceId, Guid userId, int skip, int take, string? sortBy = null, bool isDescending = true, CancellationToken ct = default)
    {
        var query = DbSet
            .Include(p => p.Workspace)
            .Where(p => p.PrjWorkspaceId == workspaceId && !p.PrjIsDeleted &&
                        (p.PrjOwnerUserId == userId ||
                         p.Members.Any(m => m.PrmUserId == userId && !m.PrmIsDeleted)));

        var sorted = ApplySort(query, sortBy, isDescending);
        var total = await sorted.CountAsync(ct);
        var items = await sorted.Skip(skip).Take(take).ToListAsync(ct);
        return (items, total);
    }

    // ── Helpers ─────────────────────────────────────────────

    /// <summary>
    /// Aplica ordenamiento al query de proyectos.
    /// Campos soportados: name, createdAt (default), updatedAt, currencyCode.
    /// </summary>
    private static IQueryable<Project> ApplySort(IQueryable<Project> query, string? sortBy, bool isDescending)
        => sortBy?.ToLowerInvariant() switch
        {
            "name"         => isDescending ? query.OrderByDescending(p => p.PrjName)        : query.OrderBy(p => p.PrjName),
            "updatedat"    => isDescending ? query.OrderByDescending(p => p.PrjUpdatedAt)   : query.OrderBy(p => p.PrjUpdatedAt),
            "currencycode" => isDescending ? query.OrderByDescending(p => p.PrjCurrencyCode): query.OrderBy(p => p.PrjCurrencyCode),
            _              => isDescending ? query.OrderByDescending(p => p.PrjCreatedAt)   : query.OrderBy(p => p.PrjCreatedAt)
        };
}
