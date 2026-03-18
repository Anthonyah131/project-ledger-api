using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class IncomeSplitRepository : Repository<IncomeSplit>, IIncomeSplitRepository
{
    public IncomeSplitRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<IncomeSplit>> GetByIncomeIdAsync(Guid incomeId, CancellationToken ct = default)
        => await DbSet
            .Include(s => s.Partner)
            .Include(s => s.CurrencyExchanges)
            .Where(s => s.InsIncomeId == incomeId)
            .ToListAsync(ct);

    public async Task DeleteByIncomeIdAsync(Guid incomeId, CancellationToken ct = default)
    {
        var splits = await DbSet.Where(s => s.InsIncomeId == incomeId).ToListAsync(ct);
        DbSet.RemoveRange(splits);
    }

    public async Task<bool> ExistsForProjectAsync(Guid projectId, CancellationToken ct = default)
        => await DbSet.AnyAsync(s => s.Income.IncProjectId == projectId, ct);
}
