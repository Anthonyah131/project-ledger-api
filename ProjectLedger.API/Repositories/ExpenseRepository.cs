using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class ExpenseRepository : Repository<Expense>, IExpenseRepository
{
    public ExpenseRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Expense>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await DbSet
            .Include(e => e.Category)
            .Where(e => e.ExpProjectId == projectId && !e.ExpIsDeleted)
            .OrderByDescending(e => e.ExpExpenseDate)
            .ToListAsync(ct);

    public async Task<IEnumerable<Expense>> GetByCategoryIdAsync(Guid categoryId, CancellationToken ct = default)
        => await DbSet
            .Where(e => e.ExpCategoryId == categoryId && !e.ExpIsDeleted)
            .OrderByDescending(e => e.ExpExpenseDate)
            .ToListAsync(ct);

    public async Task<IEnumerable<Expense>> GetByObligationIdAsync(Guid obligationId, CancellationToken ct = default)
        => await DbSet
            .Where(e => e.ExpObligationId == obligationId && !e.ExpIsDeleted)
            .OrderByDescending(e => e.ExpExpenseDate)
            .ToListAsync(ct);

    public async Task<IEnumerable<Expense>> GetTemplatesByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await DbSet
            .Where(e => e.ExpProjectId == projectId && e.ExpIsTemplate && !e.ExpIsDeleted)
            .ToListAsync(ct);
}
