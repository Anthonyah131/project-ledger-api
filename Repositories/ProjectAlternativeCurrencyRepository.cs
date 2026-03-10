using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class ProjectAlternativeCurrencyRepository : Repository<ProjectAlternativeCurrency>, IProjectAlternativeCurrencyRepository
{
    public ProjectAlternativeCurrencyRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<ProjectAlternativeCurrency>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await DbSet
            .Include(e => e.Currency)
            .Where(e => e.PacProjectId == projectId)
            .OrderBy(e => e.PacCreatedAt)
            .ToListAsync(ct);

    public async Task<ProjectAlternativeCurrency?> GetByProjectAndCurrencyAsync(Guid projectId, string currencyCode, CancellationToken ct = default)
        => await DbSet
            .Include(e => e.Currency)
            .FirstOrDefaultAsync(e => e.PacProjectId == projectId && e.PacCurrencyCode == currencyCode, ct);

    public async Task<int> CountByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await DbSet.CountAsync(e => e.PacProjectId == projectId, ct);
}
