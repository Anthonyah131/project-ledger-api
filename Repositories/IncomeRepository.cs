using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class IncomeRepository : Repository<Income>, IIncomeRepository
{
    public IncomeRepository(AppDbContext context) : base(context) { }

    public override async Task<Income?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(e => e.Category)
            .Include(e => e.Project)
            .Include(e => e.CurrencyExchanges)
            .FirstOrDefaultAsync(e => e.IncId == id, ct);

    public async Task<IEnumerable<Income>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await GetByProjectIdAsync(projectId, false, ct);

    public async Task<IEnumerable<Income>> GetByProjectIdAsync(Guid projectId, bool includeDeleted, CancellationToken ct = default)
        => await DbSet
            .Include(e => e.Category)
            .Include(e => e.Project)
            .Include(e => e.CurrencyExchanges)
            .Where(e => e.IncProjectId == projectId && (includeDeleted || !e.IncIsDeleted))
            .OrderByDescending(e => e.IncIncomeDate)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<Income> Items, int TotalCount)> GetByProjectIdPagedAsync(
        Guid projectId, bool includeDeleted, int skip, int take, string? sortBy, bool descending, CancellationToken ct = default)
    {
        var query = DbSet
            .Include(e => e.Category)
            .Include(e => e.Project)
            .Include(e => e.CurrencyExchanges)
            .Where(e => e.IncProjectId == projectId && (includeDeleted || !e.IncIsDeleted));

        var totalCount = await query.CountAsync(ct);

        query = ApplyIncomeSort(query, sortBy, descending);
        var items = await query.Skip(skip).Take(take).ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<IEnumerable<Income>> GetByCategoryIdAsync(Guid categoryId, CancellationToken ct = default)
        => await DbSet
            .Include(e => e.Category)
            .Include(e => e.Project)
            .Include(e => e.CurrencyExchanges)
            .Where(e => e.IncCategoryId == categoryId && !e.IncIsDeleted)
            .OrderByDescending(e => e.IncIncomeDate)
            .ToListAsync(ct);

    public async Task<IEnumerable<Income>> GetByPaymentMethodIdAsync(Guid paymentMethodId, CancellationToken ct = default)
        => await DbSet
            .Include(e => e.Category)
            .Include(e => e.Project)
            .Include(e => e.CurrencyExchanges)
            .Where(e => e.IncPaymentMethodId == paymentMethodId && !e.IncIsDeleted)
            .OrderByDescending(e => e.IncIncomeDate)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<Income> Items, int TotalCount)> GetByPaymentMethodIdPagedAsync(
        Guid paymentMethodId,
        int skip,
        int take,
        string? sortBy,
        bool descending,
        DateOnly? from,
        DateOnly? to,
        Guid? projectId,
        CancellationToken ct = default)
    {
        var query = DbSet
            .Include(e => e.Category)
            .Include(e => e.Project)
            .Include(e => e.CurrencyExchanges)
            .Where(e => e.IncPaymentMethodId == paymentMethodId && !e.IncIsDeleted);

        if (from.HasValue)
            query = query.Where(e => e.IncIncomeDate >= from.Value);

        if (to.HasValue)
            query = query.Where(e => e.IncIncomeDate <= to.Value);

        if (projectId.HasValue)
            query = query.Where(e => e.IncProjectId == projectId.Value);

        var totalCount = await query.CountAsync(ct);

        query = ApplyIncomeSort(query, sortBy, descending);
        var items = await query.Skip(skip).Take(take).ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<IEnumerable<Income>> GetByPaymentMethodIdsWithDetailsAsync(
        IEnumerable<Guid> paymentMethodIds, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var ids = paymentMethodIds.ToList();
        if (ids.Count == 0)
            return [];

        var query = DbSet
            .Include(e => e.Category)
            .Include(e => e.Project)
            .Include(e => e.PaymentMethod)
            .Include(e => e.CurrencyExchanges)
            .Where(e => ids.Contains(e.IncPaymentMethodId) && !e.IncIsDeleted);

        if (from.HasValue)
            query = query.Where(e => e.IncIncomeDate >= from.Value);
        if (to.HasValue)
            query = query.Where(e => e.IncIncomeDate <= to.Value);

        return await query
            .OrderByDescending(e => e.IncIncomeDate)
            .ToListAsync(ct);
    }

    public async Task<decimal> GetTotalIncomeByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await DbSet
            .Where(e => e.IncProjectId == projectId && !e.IncIsDeleted)
            .SumAsync(e => e.IncConvertedAmount, ct);

    private static IQueryable<Income> ApplyIncomeSort(IQueryable<Income> query, string? sortBy, bool descending)
    {
        return sortBy?.ToLowerInvariant() switch
        {
            "title" => descending ? query.OrderByDescending(e => e.IncTitle) : query.OrderBy(e => e.IncTitle),
            "convertedamount" => descending ? query.OrderByDescending(e => e.IncConvertedAmount) : query.OrderBy(e => e.IncConvertedAmount),
            "amount" => descending ? query.OrderByDescending(e => e.IncConvertedAmount) : query.OrderBy(e => e.IncConvertedAmount),
            "createdat" => descending ? query.OrderByDescending(e => e.IncCreatedAt) : query.OrderBy(e => e.IncCreatedAt),
            "incomedate" => descending ? query.OrderByDescending(e => e.IncIncomeDate) : query.OrderBy(e => e.IncIncomeDate),
            _ => descending ? query.OrderByDescending(e => e.IncIncomeDate) : query.OrderBy(e => e.IncIncomeDate),
        };
    }
}
