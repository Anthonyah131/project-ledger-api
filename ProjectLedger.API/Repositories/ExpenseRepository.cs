using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class ExpenseRepository : Repository<Expense>, IExpenseRepository
{
    public ExpenseRepository(AppDbContext context) : base(context) { }

    public override async Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(e => e.Category)
            .FirstOrDefaultAsync(e => e.ExpId == id, ct);

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
            .Include(e => e.Category)
            .Where(e => e.ExpCategoryId == categoryId && !e.ExpIsDeleted)
            .OrderByDescending(e => e.ExpExpenseDate)
            .ToListAsync(ct);

    public async Task<IEnumerable<Expense>> GetByObligationIdAsync(Guid obligationId, CancellationToken ct = default)
        => await DbSet
            .Include(e => e.Category)
            .Where(e => e.ExpObligationId == obligationId && !e.ExpIsDeleted)
            .OrderByDescending(e => e.ExpExpenseDate)
            .ToListAsync(ct);

    public async Task<IEnumerable<Expense>> GetTemplatesByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await DbSet
            .Include(e => e.Category)
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

    public async Task<(IReadOnlyList<Expense> Items, int TotalCount)> GetByProjectIdPagedAsync(
        Guid projectId, bool includeDeleted, int skip, int take, string? sortBy, bool descending, CancellationToken ct = default)
    {
        var query = DbSet
            .Include(e => e.Category)
            .Where(e => e.ExpProjectId == projectId && (includeDeleted || !e.ExpIsDeleted));

        var totalCount = await query.CountAsync(ct);

        query = ApplyExpenseSort(query, sortBy, descending);
        var items = await query.Skip(skip).Take(take).ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<Expense> Items, int TotalCount)> GetByPaymentMethodIdPagedAsync(
        Guid paymentMethodId, int skip, int take, string? sortBy, bool descending, CancellationToken ct = default)
    {
        var query = DbSet
            .Include(e => e.Category)
            .Include(e => e.Project)
            .Where(e => e.ExpPaymentMethodId == paymentMethodId && !e.ExpIsDeleted);

        var totalCount = await query.CountAsync(ct);

        query = ApplyExpenseSort(query, sortBy, descending);
        var items = await query.Skip(skip).Take(take).ToListAsync(ct);

        return (items, totalCount);
    }

    // ── Sorting helper ──────────────────────────────────────

    public async Task<decimal> GetSpentAmountByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await DbSet
            .Where(e => e.ExpProjectId == projectId && !e.ExpIsDeleted && !e.ExpIsTemplate)
            .SumAsync(e => e.ExpConvertedAmount, ct);

    private static IQueryable<Expense> ApplyExpenseSort(IQueryable<Expense> query, string? sortBy, bool descending)
    {
        return sortBy?.ToLowerInvariant() switch
        {
            "title" => descending ? query.OrderByDescending(e => e.ExpTitle) : query.OrderBy(e => e.ExpTitle),
            "amount" => descending ? query.OrderByDescending(e => e.ExpConvertedAmount) : query.OrderBy(e => e.ExpConvertedAmount),
            "createdat" => descending ? query.OrderByDescending(e => e.ExpCreatedAt) : query.OrderBy(e => e.ExpCreatedAt),
            _ => descending ? query.OrderByDescending(e => e.ExpExpenseDate) : query.OrderBy(e => e.ExpExpenseDate),
        };
    }

    public async Task<Dictionary<Guid, decimal>> GetPaidAmountsByObligationIdsAsync(
        IEnumerable<Guid> obligationIds, CancellationToken ct = default)
    {
        var ids = obligationIds.ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, decimal>();

        return await DbSet
            .Where(e => e.ExpObligationId.HasValue
                     && ids.Contains(e.ExpObligationId.Value)
                     && !e.ExpIsDeleted)
            .GroupBy(e => e.ExpObligationId!.Value)
            .Select(g => new { ObligationId = g.Key, PaidAmount = g.Sum(e => e.ExpConvertedAmount) })
            .ToDictionaryAsync(x => x.ObligationId, x => x.PaidAmount, ct);
    }
}
