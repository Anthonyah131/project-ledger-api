using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class ExpenseSplitRepository : Repository<ExpenseSplit>, IExpenseSplitRepository
{
    public ExpenseSplitRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<ExpenseSplit>> GetByExpenseIdAsync(Guid expenseId, CancellationToken ct = default)
        => await DbSet
            .Include(s => s.Partner)
            .Include(s => s.CurrencyExchanges)
            .Where(s => s.ExsExpenseId == expenseId)
            .ToListAsync(ct);

    public async Task DeleteByExpenseIdAsync(Guid expenseId, CancellationToken ct = default)
    {
        var splits = await DbSet.Where(s => s.ExsExpenseId == expenseId).ToListAsync(ct);
        DbSet.RemoveRange(splits);
    }

    public async Task<bool> ExistsForProjectAsync(Guid projectId, CancellationToken ct = default)
        => await DbSet.AnyAsync(s => s.Expense.ExpProjectId == projectId, ct);
}
