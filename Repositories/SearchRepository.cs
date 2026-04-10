using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.DTOs.Search;

namespace ProjectLedger.API.Repositories;

/// <summary>
/// Repository implementation for generic Search operations across entities.
/// </summary>
public class SearchRepository : ISearchRepository
{
    private readonly AppDbContext _context;

    public SearchRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ExpenseSearchResult>> SearchExpensesAsync(
        Guid userId, string query, int pageSize, CancellationToken ct = default)
    {
        var pattern = $"%{query}%";

        return await _context.Expenses
            .Include(e => e.Project)
            .Include(e => e.Category)
            .Where(e =>
                !e.ExpIsDeleted &&
                !e.ExpIsTemplate &&
                e.ExpIsActive &&
                EF.Functions.ILike(e.ExpTitle, pattern) &&
                _context.ProjectMembers.Any(pm =>
                    pm.PrmProjectId == e.ExpProjectId &&
                    pm.PrmUserId == userId &&
                    !pm.PrmIsDeleted))
            .OrderByDescending(e => e.ExpExpenseDate)
            .Take(pageSize)
            .Select(e => new ExpenseSearchResult
            {
                Id = e.ExpId,
                Title = e.ExpTitle,
                Amount = e.ExpOriginalAmount,
                Currency = e.ExpOriginalCurrency,
                Date = e.ExpExpenseDate,
                ProjectId = e.ExpProjectId,
                ProjectName = e.Project.PrjName,
                CategoryName = e.Category.CatName
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<IncomeSearchResult>> SearchIncomesAsync(
        Guid userId, string query, int pageSize, CancellationToken ct = default)
    {
        var pattern = $"%{query}%";

        return await _context.Incomes
            .Include(i => i.Project)
            .Include(i => i.Category)
            .Where(i =>
                !i.IncIsDeleted &&
                i.IncIsActive &&
                EF.Functions.ILike(i.IncTitle, pattern) &&
                _context.ProjectMembers.Any(pm =>
                    pm.PrmProjectId == i.IncProjectId &&
                    pm.PrmUserId == userId &&
                    !pm.PrmIsDeleted))
            .OrderByDescending(i => i.IncIncomeDate)
            .Take(pageSize)
            .Select(i => new IncomeSearchResult
            {
                Id = i.IncId,
                Title = i.IncTitle,
                Amount = i.IncOriginalAmount,
                Currency = i.IncOriginalCurrency,
                Date = i.IncIncomeDate,
                ProjectId = i.IncProjectId,
                ProjectName = i.Project.PrjName,
                CategoryName = i.Category.CatName
            })
            .ToListAsync(ct);
    }
}
