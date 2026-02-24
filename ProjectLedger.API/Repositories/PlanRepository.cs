using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class PlanRepository : Repository<Plan>, IPlanRepository
{
    public PlanRepository(AppDbContext context) : base(context) { }

    public async Task<Plan?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(
            p => p.PlnSlug == slug && p.PlnIsActive, ct);

    public async Task<IEnumerable<Plan>> GetActiveAsync(CancellationToken ct = default)
        => await DbSet
            .Where(p => p.PlnIsActive)
            .OrderBy(p => p.PlnDisplayOrder)
            .ToListAsync(ct);
}
