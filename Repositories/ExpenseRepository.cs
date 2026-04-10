using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

/// <summary>
/// Repository implementation for Expense operations.
/// </summary>
public class ExpenseRepository : Repository<Expense>, IExpenseRepository
{
    public ExpenseRepository(AppDbContext context) : base(context) { }

    public override async Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(e => e.Category)
            .Include(e => e.CurrencyExchanges)
            .Include(e => e.Splits).ThenInclude(s => s.Partner)
            .Include(e => e.Splits).ThenInclude(s => s.CurrencyExchanges)
            .FirstOrDefaultAsync(e => e.ExpId == id, ct);

    public async Task<IEnumerable<Expense>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await GetByProjectIdAsync(projectId, false, ct);

    public async Task<IEnumerable<Expense>> GetByProjectIdAsync(Guid projectId, bool includeDeleted, CancellationToken ct = default)
        => await DbSet
            .Include(e => e.Category)
            .Include(e => e.CurrencyExchanges)
            .Where(e => e.ExpProjectId == projectId && (includeDeleted || !e.ExpIsDeleted) && e.ExpIsActive)
            .OrderByDescending(e => e.ExpExpenseDate)
            .ToListAsync(ct);

    public async Task<IEnumerable<Expense>> GetByCategoryIdAsync(Guid categoryId, CancellationToken ct = default)
        => await DbSet
            .Include(e => e.Category)
            .Include(e => e.CurrencyExchanges)
            .Where(e => e.ExpCategoryId == categoryId && !e.ExpIsDeleted && e.ExpIsActive)
            .OrderByDescending(e => e.ExpExpenseDate)
            .ToListAsync(ct);

    public async Task<IEnumerable<Expense>> GetByObligationIdAsync(Guid obligationId, CancellationToken ct = default)
        => await DbSet
            .Include(e => e.Category)
            .Include(e => e.CurrencyExchanges)
            .Where(e => e.ExpObligationId == obligationId && !e.ExpIsDeleted && e.ExpIsActive)
            .OrderByDescending(e => e.ExpExpenseDate)
            .ToListAsync(ct);

    public async Task<IEnumerable<Expense>> GetTemplatesByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await DbSet
            .Include(e => e.Category)
            .Include(e => e.CurrencyExchanges)
            .Where(e => e.ExpProjectId == projectId && e.ExpIsTemplate && !e.ExpIsDeleted)
            .ToListAsync(ct);

    public async Task<IEnumerable<Expense>> GetByPaymentMethodIdAsync(Guid paymentMethodId, CancellationToken ct = default)
        => await DbSet
            .Include(e => e.Category)
            .Include(e => e.Project)
            .Include(e => e.CurrencyExchanges)
            .Where(e => e.ExpPaymentMethodId == paymentMethodId && !e.ExpIsDeleted && e.ExpIsActive)
            .OrderByDescending(e => e.ExpExpenseDate)
            .ToListAsync(ct);

    public async Task<IEnumerable<Expense>> GetByProjectIdWithDetailsAsync(Guid projectId, CancellationToken ct = default)
        => await DbSet
            .Include(e => e.Category)
            .Include(e => e.PaymentMethod)
            .Include(e => e.CurrencyExchanges)
            .Where(e => e.ExpProjectId == projectId && !e.ExpIsDeleted && e.ExpIsActive)
            .OrderByDescending(e => e.ExpExpenseDate)
            .ToListAsync(ct);

    public async Task<IEnumerable<Expense>> GetDetailedByProjectIdAsync(
        Guid projectId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var query = DbSet
            .Include(e => e.Category)
            .Include(e => e.PaymentMethod)
            .Include(e => e.Obligation)
            .Include(e => e.CurrencyExchanges)
            .Include(e => e.Splits).ThenInclude(s => s.Partner)
            .Where(e => e.ExpProjectId == projectId && !e.ExpIsDeleted && !e.ExpIsTemplate && e.ExpIsActive);

        if (from.HasValue)
            query = query.Where(e => e.ExpExpenseDate >= from.Value);
        if (to.HasValue)
            query = query.Where(e => e.ExpExpenseDate <= to.Value);

        return await query
            .OrderBy(e => e.ExpExpenseDate)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Expense>> GetByProjectIdForPartnerAsync(
        Guid projectId, Guid partnerId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var query = DbSet
            .Include(e => e.Category)
            .Include(e => e.PaymentMethod).ThenInclude(pm => pm.OwnerPartner)
            .Include(e => e.Splits).ThenInclude(s => s.Partner)
            .Include(e => e.Splits).ThenInclude(s => s.CurrencyExchanges)
            .Where(e => e.ExpProjectId == projectId
                && !e.ExpIsDeleted && !e.ExpIsTemplate && e.ExpIsActive
                && e.Splits.Any(s => s.ExsPartnerId == partnerId));

        if (from.HasValue)
            query = query.Where(e => e.ExpExpenseDate >= from.Value);
        if (to.HasValue)
            query = query.Where(e => e.ExpExpenseDate <= to.Value);

        return await query.OrderBy(e => e.ExpExpenseDate).ToListAsync(ct);
    }

    public async Task<IEnumerable<Expense>> GetByPaymentMethodIdsWithDetailsAsync(
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
            .Where(e => ids.Contains(e.ExpPaymentMethodId) && !e.ExpIsDeleted && !e.ExpIsTemplate && e.ExpIsActive);

        if (from.HasValue)
            query = query.Where(e => e.ExpExpenseDate >= from.Value);
        if (to.HasValue)
            query = query.Where(e => e.ExpExpenseDate <= to.Value);

        return await query
            .OrderByDescending(e => e.ExpExpenseDate)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<Expense> Items, int TotalCount)> GetByProjectIdPagedAsync(
        Guid projectId, bool includeDeleted, bool? isActive, int skip, int take, string? sortBy, bool descending, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var query = DbSet
            .Include(e => e.Category)
            .Include(e => e.CurrencyExchanges)
            .Include(e => e.Splits).ThenInclude(s => s.Partner)
            .Include(e => e.Splits).ThenInclude(s => s.CurrencyExchanges)
            .Where(e => e.ExpProjectId == projectId && (includeDeleted || !e.ExpIsDeleted));

        if (isActive.HasValue)
            query = query.Where(e => e.ExpIsActive == isActive.Value);

        if (from.HasValue)
            query = query.Where(e => e.ExpExpenseDate >= from.Value);

        if (to.HasValue)
            query = query.Where(e => e.ExpExpenseDate <= to.Value);

        var totalCount = await query.CountAsync(ct);

        query = ApplyExpenseSort(query, sortBy, descending);
        var items = await query.Skip(skip).Take(take).ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<Expense> Items, int TotalCount, decimal TotalActiveAmount)> GetByPaymentMethodIdPagedAsync(
        Guid paymentMethodId,
        bool? isActive,
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
            .Where(e => e.ExpPaymentMethodId == paymentMethodId && !e.ExpIsDeleted);

        if (isActive.HasValue)
            query = query.Where(e => e.ExpIsActive == isActive.Value);

        if (from.HasValue)
            query = query.Where(e => e.ExpExpenseDate >= from.Value);

        if (to.HasValue)
            query = query.Where(e => e.ExpExpenseDate <= to.Value);

        if (projectId.HasValue)
            query = query.Where(e => e.ExpProjectId == projectId.Value);

        var totalCount = await query.CountAsync(ct);

        // Total active movements with the same date and project filters
        var activeAmountQuery = DbSet
            .Where(e => e.ExpPaymentMethodId == paymentMethodId && !e.ExpIsDeleted && e.ExpIsActive);

        if (from.HasValue)
            activeAmountQuery = activeAmountQuery.Where(e => e.ExpExpenseDate >= from.Value);
        if (to.HasValue)
            activeAmountQuery = activeAmountQuery.Where(e => e.ExpExpenseDate <= to.Value);
        if (projectId.HasValue)
            activeAmountQuery = activeAmountQuery.Where(e => e.ExpProjectId == projectId.Value);

        var totalActiveAmount = await activeAmountQuery.SumAsync(
            e => (decimal?)(e.ExpAccountAmount ?? e.ExpConvertedAmount), ct) ?? 0m;

        query = ApplyExpenseSort(query, sortBy, descending);
        var items = await query.Skip(skip).Take(take).ToListAsync(ct);

        return (items, totalCount, totalActiveAmount);
    }

    // ── Sorting helper ──────────────────────────────────────

    public async Task<decimal> GetSpentAmountByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await DbSet
            .Where(e => e.ExpProjectId == projectId && !e.ExpIsDeleted && !e.ExpIsTemplate && e.ExpIsActive)
            .SumAsync(e => e.ExpConvertedAmount, ct);

    private static IQueryable<Expense> ApplyExpenseSort(IQueryable<Expense> query, string? sortBy, bool descending)
    {
        return sortBy?.ToLowerInvariant() switch
        {
            "title" => descending ? query.OrderByDescending(e => e.ExpTitle) : query.OrderBy(e => e.ExpTitle),
            "amount" or "convertedamount" => descending ? query.OrderByDescending(e => e.ExpConvertedAmount) : query.OrderBy(e => e.ExpConvertedAmount),
            "originalamount" => descending ? query.OrderByDescending(e => e.ExpOriginalAmount) : query.OrderBy(e => e.ExpOriginalAmount),
            "createdat" => descending ? query.OrderByDescending(e => e.ExpCreatedAt) : query.OrderBy(e => e.ExpCreatedAt),
            "date" or "expensedate" => descending ? query.OrderByDescending(e => e.ExpExpenseDate) : query.OrderBy(e => e.ExpExpenseDate),
            _ => descending ? query.OrderByDescending(e => e.ExpExpenseDate) : query.OrderBy(e => e.ExpExpenseDate),
        };
    }

    public async Task<Dictionary<Guid, decimal>> GetPaidAmountsByObligationIdsAsync(
        IEnumerable<Guid> obligationIds, CancellationToken ct = default)
    {
        var ids = obligationIds.ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, decimal>();

        // Project expenses with obligation currency to compute paid amounts
        // in the obligation's own currency (not the project's)
        var payments = await DbSet
            .Where(e => e.ExpObligationId.HasValue
                     && ids.Contains(e.ExpObligationId.Value)
                     && !e.ExpIsDeleted
                     && e.ExpIsActive)
            .Select(e => new
            {
                ObligationId = e.ExpObligationId!.Value,
                e.ExpOriginalAmount,
                e.ExpOriginalCurrency,
                e.ExpConvertedAmount,
                e.ExpObligationEquivalentAmount,
                ObligationCurrency = e.Obligation!.OblCurrency
            })
            .ToListAsync(ct);

        return payments
            .GroupBy(e => e.ObligationId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(e => e.ExpOriginalCurrency == e.ObligationCurrency
                    ? e.ExpOriginalAmount
                    : e.ExpObligationEquivalentAmount ?? e.ExpConvertedAmount));
    }
}
