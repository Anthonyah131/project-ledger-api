using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class ObligationRepository : Repository<Obligation>, IObligationRepository
{
    public ObligationRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Obligation>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await DbSet
            .Where(o => o.OblProjectId == projectId && !o.OblIsDeleted)
            .OrderByDescending(o => o.OblCreatedAt)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<Obligation> Items, int TotalCount)> GetByProjectIdPagedAsync(
        Guid projectId, int skip, int take, string? sortBy, bool descending, CancellationToken ct = default)
    {
        var query = DbSet
            .Where(o => o.OblProjectId == projectId && !o.OblIsDeleted);

        var totalCount = await query.CountAsync(ct);

        query = sortBy?.ToLowerInvariant() switch
        {
            "title" => descending ? query.OrderByDescending(o => o.OblTitle) : query.OrderBy(o => o.OblTitle),
            "amount" => descending ? query.OrderByDescending(o => o.OblTotalAmount) : query.OrderBy(o => o.OblTotalAmount),
            "duedate" => descending ? query.OrderByDescending(o => o.OblDueDate) : query.OrderBy(o => o.OblDueDate),
            _ => descending ? query.OrderByDescending(o => o.OblCreatedAt) : query.OrderBy(o => o.OblCreatedAt),
        };

        var items = await query.Skip(skip).Take(take).ToListAsync(ct);
        return (items, totalCount);
    }
}
