using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class ExpenseRepository : Repository<Expense>, IExpenseRepository
{
    public ExpenseRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Expense>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await GetByProjectIdAsync(projectId, false, ct);

    public async Task<IEnumerable<Expense>> GetByProjectIdAsync(Guid projectId, bool includeDeleted, CancellationToken ct = default)
        => await DbSet
            .Include(e => e.Category)
            .Where(e => e.ExpProjectId == projectId && (includeDeleted || !e.ExpIsDeleted))
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

    public async Task<IEnumerable<Expense>> GetByPaymentMethodIdAsync(Guid paymentMethodId, CancellationToken ct = default)
        => await DbSet
            .Include(e => e.Category)
            .Include(e => e.Project)
            .Where(e => e.ExpPaymentMethodId == paymentMethodId && !e.ExpIsDeleted)
            .OrderByDescending(e => e.ExpExpenseDate)
            .ToListAsync(ct);

    public async Task<IEnumerable<Expense>> GetByProjectIdWithDetailsAsync(Guid projectId, CancellationToken ct = default)
        => await DbSet
            .Include(e => e.Category)
            .Include(e => e.PaymentMethod)
            .Where(e => e.ExpProjectId == projectId && !e.ExpIsDeleted)
            .OrderByDescending(e => e.ExpExpenseDate)
            .ToListAsync(ct);
}
