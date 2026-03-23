using ProjectLedger.API.DTOs.Report;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

public class WorkspaceReportService : IWorkspaceReportService
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IExpenseRepository _expenseRepo;
    private readonly IIncomeRepository _incomeRepo;
    private readonly IPlanAuthorizationService _planAuth;

    public WorkspaceReportService(
        IWorkspaceService workspaceService,
        IExpenseRepository expenseRepo,
        IIncomeRepository incomeRepo,
        IPlanAuthorizationService planAuth)
    {
        _workspaceService = workspaceService;
        _expenseRepo = expenseRepo;
        _incomeRepo = incomeRepo;
        _planAuth = planAuth;
    }

    public async Task<WorkspaceReportResponse> GetSummaryAsync(
        Guid workspaceId, Guid userId, DateOnly? from, DateOnly? to,
        string? referenceCurrency, CancellationToken ct = default)
    {
        var workspace = await _workspaceService.GetByIdWithDetailsAsync(workspaceId, ct)
            ?? throw new KeyNotFoundException("WorkspaceNotFound");

        // Verify membership
        var role = await _workspaceService.GetMemberRoleAsync(workspaceId, userId, ct);
        if (role is null)
            throw new UnauthorizedAccessException("WorkspaceAccessDenied");

        var projects = workspace.Projects.Where(p => !p.PrjIsDeleted).ToList();

        // Determine if we can consolidate (same currency or reference currency provided)
        var canConsolidate = !string.IsNullOrWhiteSpace(referenceCurrency)
            || projects.Select(p => p.PrjCurrencyCode).Distinct().Count() <= 1;

        var effectiveCurrency = referenceCurrency
            ?? (projects.Count > 0 ? projects[0].PrjCurrencyCode : null);

        // Load data for all projects in parallel
        var projectSummaries = new List<WorkspaceProjectSummary>();
        var allExpensesByProject = new Dictionary<Guid, List<Models.Expense>>();
        var allIncomesByProject = new Dictionary<Guid, List<Models.Income>>();

        foreach (var project in projects)
        {
            var expenses = (await _expenseRepo.GetByProjectIdWithDetailsAsync(project.PrjId, ct))
                .Where(e => !e.ExpIsTemplate)
                .Where(e => from is null || e.ExpExpenseDate >= from.Value)
                .Where(e => to is null || e.ExpExpenseDate <= to.Value)
                .ToList();

            var incomes = (await _incomeRepo.GetByProjectIdAsync(project.PrjId, ct))
                .Where(i => from is null || i.IncIncomeDate >= from.Value)
                .Where(i => to is null || i.IncIncomeDate <= to.Value)
                .ToList();

            allExpensesByProject[project.PrjId] = expenses;
            allIncomesByProject[project.PrjId] = incomes;

            var totalSpent = expenses.Sum(e => e.ExpConvertedAmount);
            var totalIncome = incomes.Sum(i => i.IncConvertedAmount);

            projectSummaries.Add(new WorkspaceProjectSummary
            {
                ProjectId = project.PrjId,
                ProjectName = project.PrjName,
                CurrencyCode = project.PrjCurrencyCode,
                TotalSpent = totalSpent,
                TotalIncome = totalIncome,
                NetBalance = totalIncome - totalSpent,
                ExpenseCount = expenses.Count,
                IncomeCount = incomes.Count
            });
        }

        // Consolidated totals (only when all projects share the same currency or reference is set)
        WorkspaceConsolidatedTotals? consolidatedTotals = null;
        if (canConsolidate && projects.Count > 0)
        {
            // When all projects use the same currency, we can safely consolidate.
            // When a referenceCurrency is provided but differs from project currencies,
            // we still consolidate using ConvertedAmount (which is in project currency).
            // NOTE: Cross-currency consolidation with proper exchange rates would require
            // TransactionCurrencyExchanges — for now we consolidate when currencies match.
            var allSameCurrency = projects.All(p =>
                string.Equals(p.PrjCurrencyCode, effectiveCurrency, StringComparison.OrdinalIgnoreCase));

            if (allSameCurrency)
            {
                var totalSpent = projectSummaries.Sum(p => p.TotalSpent);
                var totalIncome = projectSummaries.Sum(p => p.TotalIncome);

                consolidatedTotals = new WorkspaceConsolidatedTotals
                {
                    TotalSpent = totalSpent,
                    TotalIncome = totalIncome,
                    NetBalance = totalIncome - totalSpent,
                    TotalExpenseCount = projectSummaries.Sum(p => p.ExpenseCount),
                    TotalIncomeCount = projectSummaries.Sum(p => p.IncomeCount)
                };

                // Calculate percentages
                foreach (var ps in projectSummaries)
                {
                    ps.Percentage = totalSpent > 0
                        ? Math.Round(ps.TotalSpent / totalSpent * 100, 2)
                        : 0;
                }
            }
        }

        // Consolidated by category (cross-project, grouped by name)
        var allExpenses = allExpensesByProject.Values.SelectMany(e => e).ToList();
        var consolidatedByCategory = allExpenses
            .GroupBy(e => e.Category?.CatName ?? "Unknown")
            .Select(g =>
            {
                var catTotal = g.Sum(e => e.ExpConvertedAmount);
                var totalSpent = allExpenses.Sum(e => e.ExpConvertedAmount);
                return new WorkspaceCategoryBreakdown
                {
                    CategoryName = g.Key,
                    TotalAmount = catTotal,
                    Percentage = totalSpent > 0 ? Math.Round(catTotal / totalSpent * 100, 2) : 0,
                    ProjectCount = g.Select(e => e.ExpProjectId).Distinct().Count(),
                    ExpenseCount = g.Count()
                };
            })
            .OrderByDescending(c => c.TotalAmount)
            .ToList();

        // Monthly trend
        var allIncomes = allIncomesByProject.Values.SelectMany(i => i).ToList();
        var allMonths = allExpenses
            .Select(e => new { e.ExpExpenseDate.Year, e.ExpExpenseDate.Month })
            .Union(allIncomes.Select(i => new { Year = i.IncIncomeDate.Year, Month = i.IncIncomeDate.Month }))
            .Distinct()
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToList();

        var monthlyTrend = allMonths.Select(m =>
        {
            var monthExpenses = allExpenses
                .Where(e => e.ExpExpenseDate.Year == m.Year && e.ExpExpenseDate.Month == m.Month)
                .ToList();
            var monthIncomes = allIncomes
                .Where(i => i.IncIncomeDate.Year == m.Year && i.IncIncomeDate.Month == m.Month)
                .ToList();

            var monthTotal = monthExpenses.Sum(e => e.ExpConvertedAmount);
            var monthIncome = monthIncomes.Sum(i => i.IncConvertedAmount);

            var byProject = projects
                .Select(p =>
                {
                    var pExp = monthExpenses.Where(e => e.ExpProjectId == p.PrjId).ToList();
                    var pInc = monthIncomes.Where(i => i.IncProjectId == p.PrjId).ToList();

                    if (pExp.Count == 0 && pInc.Count == 0)
                        return null;

                    return new WorkspaceProjectMonthBreakdown
                    {
                        ProjectId = p.PrjId,
                        ProjectName = p.PrjName,
                        CurrencyCode = p.PrjCurrencyCode,
                        TotalSpent = pExp.Sum(e => e.ExpConvertedAmount),
                        TotalIncome = pInc.Sum(i => i.IncConvertedAmount),
                        NetBalance = pInc.Sum(i => i.IncConvertedAmount) - pExp.Sum(e => e.ExpConvertedAmount)
                    };
                })
                .Where(x => x is not null)
                .Cast<WorkspaceProjectMonthBreakdown>()
                .ToList();

            return new WorkspaceMonthlyRow
            {
                Year = m.Year,
                Month = m.Month,
                MonthLabel = $"{new DateTime(m.Year, m.Month, 1):MMMM yyyy}",
                TotalSpent = monthTotal,
                TotalIncome = monthIncome,
                NetBalance = monthIncome - monthTotal,
                ExpenseCount = monthExpenses.Count,
                IncomeCount = monthIncomes.Count,
                ByProject = byProject
            };
        }).ToList();

        return new WorkspaceReportResponse
        {
            WorkspaceId = workspace.WksId,
            WorkspaceName = workspace.WksName,
            DateFrom = from,
            DateTo = to,
            GeneratedAt = DateTime.UtcNow,
            ReferenceCurrency = effectiveCurrency,
            ConsolidatedTotals = consolidatedTotals,
            ProjectCount = projects.Count,
            Projects = projectSummaries.OrderByDescending(p => p.TotalSpent).ToList(),
            ConsolidatedByCategory = consolidatedByCategory,
            MonthlyTrend = monthlyTrend
        };
    }
}
