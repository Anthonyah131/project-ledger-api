using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

/// <summary>
/// Repository implementation for SplitCurrencyExchange operations.
/// </summary>
public class SplitCurrencyExchangeRepository : Repository<SplitCurrencyExchange>, ISplitCurrencyExchangeRepository
{
    public SplitCurrencyExchangeRepository(AppDbContext context) : base(context) { }

    public async Task DeleteByExpenseIdAsync(Guid expenseId, CancellationToken ct = default)
    {
        var splitIds = await Context.ExpenseSplits
            .Where(s => s.ExsExpenseId == expenseId)
            .Select(s => s.ExsId)
            .ToListAsync(ct);

        if (splitIds.Count == 0) return;

        var exchanges = await DbSet
            .Where(e => e.SceExpenseSplitId != null && splitIds.Contains(e.SceExpenseSplitId.Value))
            .ToListAsync(ct);

        DbSet.RemoveRange(exchanges);
    }

    public async Task DeleteByIncomeIdAsync(Guid incomeId, CancellationToken ct = default)
    {
        var splitIds = await Context.IncomeSplits
            .Where(s => s.InsIncomeId == incomeId)
            .Select(s => s.InsId)
            .ToListAsync(ct);

        if (splitIds.Count == 0) return;

        var exchanges = await DbSet
            .Where(e => e.SceIncomeSplitId != null && splitIds.Contains(e.SceIncomeSplitId.Value))
            .ToListAsync(ct);

        DbSet.RemoveRange(exchanges);
    }
}
