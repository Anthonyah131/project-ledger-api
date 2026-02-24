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
}
