using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class CurrencyRepository : Repository<Currency>, ICurrencyRepository
{
    public CurrencyRepository(AppDbContext context) : base(context) { }

    public async Task<Currency?> GetByCodeAsync(string code, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(c => c.CurCode == code, ct);

    public async Task<IEnumerable<Currency>> GetActiveAsync(CancellationToken ct = default)
        => await DbSet
            .Where(c => c.CurIsActive)
            .OrderBy(c => c.CurName)
            .ToListAsync(ct);
}
