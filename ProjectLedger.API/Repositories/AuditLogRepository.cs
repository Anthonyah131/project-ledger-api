using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class AuditLogRepository : Repository<AuditLog>, IAuditLogRepository
{
    public AuditLogRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<AuditLog>> GetByEntityAsync(
        string entityName, Guid entityId, CancellationToken ct = default)
        => await DbSet
            .Include(a => a.PerformedByUser)
            .Where(a => a.AudEntityName == entityName && a.AudEntityId == entityId)
            .OrderByDescending(a => a.AudPerformedAt)
            .ToListAsync(ct);

    public async Task<IEnumerable<AuditLog>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await DbSet
            .Include(a => a.PerformedByUser)
            .Where(a => a.AudPerformedByUserId == userId)
            .OrderByDescending(a => a.AudPerformedAt)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetByEntityPagedAsync(
        string entityName, Guid entityId, int skip, int take, CancellationToken ct = default)
    {
        var query = DbSet
            .Include(a => a.PerformedByUser)
            .Where(a => a.AudEntityName == entityName && a.AudEntityId == entityId);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.AudPerformedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetByUserIdPagedAsync(
        Guid userId, int skip, int take, CancellationToken ct = default)
    {
        var query = DbSet
            .Include(a => a.PerformedByUser)
            .Where(a => a.AudPerformedByUserId == userId);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.AudPerformedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
