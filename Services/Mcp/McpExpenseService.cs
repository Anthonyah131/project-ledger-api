using ProjectLedger.API.DTOs.Mcp;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public partial class McpService
{
    public async Task<McpExpenseTotalsResponse> GetExpenseTotalsAsync(
        Guid userId,
        McpExpenseTotalsQuery query,
        CancellationToken ct = default)
    {
        EnsureValidDateRange(query.From, query.To);

        var scope = await ResolveScopeAsync(userId, query.ProjectId, query.ProjectName, ct);
        var expenses = await LoadExpensesAsync(scope.SelectedProjects, query.From, query.To, ct);

        string? categorySearchNote = null;
        if (query.CategoryId.HasValue)
        {
            expenses = expenses.Where(e => e.ExpCategoryId == query.CategoryId.Value).ToList();
        }
        else if (!string.IsNullOrWhiteSpace(query.CategoryName))
        {
            expenses = FilterByNameWithPriority(expenses, e => e.Category?.CatName, query.CategoryName).ToList();
            if (expenses.Count == 0)
                categorySearchNote = $"No categories matched categoryName '{query.CategoryName}'. Returned empty results.";
        }

        var total = expenses.Sum(e => e.ExpConvertedAmount);
        var count = expenses.Count;

        var response = new McpExpenseTotalsResponse
        {
            ProjectId = query.ProjectId,
            From = query.From,
            To = query.To,
            TotalSpent = total,
            TransactionCount = count,
            AverageExpense = count > 0 ? Math.Round(total / count, 2) : 0m,
            SearchNote = CombineSearchNotes(scope.SearchNote, categorySearchNote)
        };

        if (query.IncludeTopCategories == true && expenses.Count > 0)
        {
            response.TopCategories = expenses
                .GroupBy(e => new { e.ExpCategoryId, Name = e.Category?.CatName ?? "Unknown" })
                .Select(g =>
                {
                    var amount = g.Sum(x => x.ExpConvertedAmount);
                    return new McpExpenseByCategoryItemResponse
                    {
                        CategoryId = g.Key.ExpCategoryId,
                        CategoryName = g.Key.Name,
                        TotalAmount = amount,
                        ExpenseCount = g.Count(),
                        Percentage = total > 0 ? Math.Round(amount / total * 100m, 2) : 0m
                    };
                })
                .OrderByDescending(x => x.TotalAmount)
                .Take(5)
                .ToList();
        }

        if (query.ComparePreviousPeriod == true && query.From.HasValue && query.To.HasValue)
        {
            var length = query.To.Value.DayNumber - query.From.Value.DayNumber + 1;
            var prevFrom = query.From.Value.AddDays(-length);
            var prevTo = query.From.Value.AddDays(-1);

            var previousExpenses = await LoadExpensesAsync(scope.SelectedProjects, prevFrom, prevTo, ct);
            if (query.CategoryId.HasValue)
                previousExpenses = previousExpenses.Where(e => e.ExpCategoryId == query.CategoryId.Value).ToList();
            else if (!string.IsNullOrWhiteSpace(query.CategoryName))
                previousExpenses = FilterByNameWithPriority(previousExpenses, e => e.Category?.CatName, query.CategoryName).ToList();

            var previousTotal = previousExpenses.Sum(e => e.ExpConvertedAmount);

            response.PreviousPeriodTotal = previousTotal;
            response.DeltaAmount = total - previousTotal;
            response.DeltaPercentage = previousTotal == 0
                ? null
                : Math.Round((total - previousTotal) / previousTotal * 100m, 2);
        }

        return response;
    }

    public async Task<McpExpenseByCategoryResponse> GetExpenseByCategoryAsync(
        Guid userId,
        McpExpenseByCategoryQuery query,
        CancellationToken ct = default)
    {
        EnsureValidDateRange(query.From, query.To);
        var includeTrend = query.IncludeTrend == true;
        var includeOthers = query.IncludeOthers == true;
        var top = query.Top ?? 10;

        var scope = await ResolveScopeAsync(userId, query.ProjectId, query.ProjectName, ct);
        var expenses = await LoadExpensesAsync(scope.SelectedProjects, query.From, query.To, ct);
        var totalSpent = expenses.Sum(e => e.ExpConvertedAmount);

        Dictionary<Guid, decimal>? previousPeriodByCategory = null;
        if (includeTrend && query.From.HasValue && query.To.HasValue)
        {
            var length = query.To.Value.DayNumber - query.From.Value.DayNumber + 1;
            var prevFrom = query.From.Value.AddDays(-length);
            var prevTo = query.From.Value.AddDays(-1);

            var previous = await LoadExpensesAsync(scope.SelectedProjects, prevFrom, prevTo, ct);
            previousPeriodByCategory = previous
                .GroupBy(e => e.ExpCategoryId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.ExpConvertedAmount));
        }

        var grouped = expenses
            .GroupBy(e => new { e.ExpCategoryId, Name = e.Category?.CatName ?? "Unknown" })
            .Select(g =>
            {
                var amount = g.Sum(x => x.ExpConvertedAmount);
                decimal? trendDelta = null;
                if (previousPeriodByCategory is not null)
                {
                    var previousAmount = previousPeriodByCategory.GetValueOrDefault(g.Key.ExpCategoryId, 0m);
                    trendDelta = amount - previousAmount;
                }

                return new McpExpenseByCategoryItemResponse
                {
                    CategoryId = g.Key.ExpCategoryId,
                    CategoryName = g.Key.Name,
                    TotalAmount = amount,
                    ExpenseCount = g.Count(),
                    Percentage = totalSpent > 0 ? Math.Round(amount / totalSpent * 100m, 2) : 0m,
                    TrendDelta = trendDelta
                };
            })
            .OrderByDescending(x => x.TotalAmount)
            .ToList();

        var topItems = grouped.Take(top).ToList();
        if (includeOthers && grouped.Count > top)
        {
            var rest = grouped.Skip(top).ToList();
            topItems.Add(new McpExpenseByCategoryItemResponse
            {
                CategoryId = Guid.Empty,
                CategoryName = "Others",
                TotalAmount = rest.Sum(r => r.TotalAmount),
                ExpenseCount = rest.Sum(r => r.ExpenseCount),
                Percentage = totalSpent > 0 ? Math.Round(rest.Sum(r => r.TotalAmount) / totalSpent * 100m, 2) : 0m
            });
        }

        return new McpExpenseByCategoryResponse
        {
            ProjectId = query.ProjectId,
            From = query.From,
            To = query.To,
            TotalSpent = totalSpent,
            SearchNote = scope.SearchNote,
            Items = topItems
        };
    }

    public async Task<McpExpenseByProjectResponse> GetExpenseByProjectAsync(
        Guid userId,
        McpExpenseByProjectQuery query,
        CancellationToken ct = default)
    {
        EnsureValidDateRange(query.From, query.To);
        var includeBudgetContext = query.IncludeBudgetContext ?? true;
        var top = query.Top ?? 10;

        var scope = await ResolveScopeAsync(userId, query.ProjectId, query.ProjectName, ct);
        var expenses = await LoadExpensesAsync(scope.SelectedProjects, query.From, query.To, ct);
        var totalSpent = expenses.Sum(e => e.ExpConvertedAmount);

        var budgetMap = new Dictionary<Guid, ProjectBudget?>();
        if (includeBudgetContext)
        {
            foreach (var project in scope.SelectedProjects)
                budgetMap[project.PrjId] = await _budgetRepo.GetActiveByProjectIdAsync(project.PrjId, ct);
        }

        var grouped = scope.SelectedProjects
            .Select(project =>
            {
                var projectExpenses = expenses.Where(e => e.ExpProjectId == project.PrjId).ToList();
                var projectSpent = projectExpenses.Sum(e => e.ExpConvertedAmount);
                var budget = budgetMap.GetValueOrDefault(project.PrjId);

                return new McpExpenseByProjectItemResponse
                {
                    ProjectId = project.PrjId,
                    ProjectName = project.PrjName,
                    CurrencyCode = project.PrjCurrencyCode,
                    TotalSpent = projectSpent,
                    ExpenseCount = projectExpenses.Count,
                    Budget = budget?.PjbTotalBudget,
                    BudgetUsedPercentage = budget is not null && budget.PjbTotalBudget > 0
                        ? Math.Round(projectSpent / budget.PjbTotalBudget * 100m, 2)
                        : null
                };
            })
            .OrderByDescending(x => x.TotalSpent)
            .Take(top)
            .ToList();

        return new McpExpenseByProjectResponse
        {
            From = query.From,
            To = query.To,
            TotalSpent = totalSpent,
            Items = grouped
        };
    }

    public async Task<McpExpenseTrendsResponse> GetExpenseTrendsAsync(
        Guid userId,
        McpExpenseTrendsQuery query,
        CancellationToken ct = default)
    {
        EnsureValidDateRange(query.From, query.To);
        var granularity = NormalizeGranularity(query.Granularity);

        var (from, to) = ResolveRangeOrDefaults(query.From, query.To, granularity);

        var scope = await ResolveScopeAsync(userId, query.ProjectId, query.ProjectName, ct);
        var expenses = await LoadExpensesAsync(scope.SelectedProjects, from, to, ct);

        string? categorySearchNote = null;
        if (query.CategoryId.HasValue)
        {
            expenses = expenses.Where(e => e.ExpCategoryId == query.CategoryId.Value).ToList();
        }
        else if (!string.IsNullOrWhiteSpace(query.CategoryName))
        {
            expenses = FilterByNameWithPriority(expenses, e => e.Category?.CatName, query.CategoryName).ToList();

            if (expenses.Count == 0)
                categorySearchNote = $"No categories matched categoryName '{query.CategoryName}'. Returned empty results.";
        }

        var points = expenses
            .GroupBy(e => GetPeriodStart(e.ExpExpenseDate, granularity))
            .OrderBy(g => g.Key)
            .Select(g => new McpExpenseTrendPointResponse
            {
                PeriodStart = g.Key,
                PeriodLabel = BuildPeriodLabel(g.Key, granularity),
                TotalSpent = g.Sum(x => x.ExpConvertedAmount),
                ExpenseCount = g.Count()
            })
            .ToList();

        return new McpExpenseTrendsResponse
        {
            ProjectId = query.ProjectId,
            From = from,
            To = to,
            Granularity = granularity,
            SearchNote = CombineSearchNotes(scope.SearchNote, categorySearchNote),
            Points = points
        };
    }
}
