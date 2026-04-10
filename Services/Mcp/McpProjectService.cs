using ProjectLedger.API.DTOs.Mcp;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

/// <summary>
/// Partial implementation of McpService focusing on project portfolio health, activity status, and upcoming deadlines.
/// </summary>
public partial class McpService
{
    public async Task<McpPagedResponse<McpProjectPortfolioItemResponse>> GetProjectPortfolioAsync(
        Guid userId,
        McpProjectPortfolioQuery query,
        CancellationToken ct = default)
    {
        var scope = await ResolveScopeAsync(userId, query.ProjectId, query.ProjectName, ct);
        var activityDays = query.ActivityDays ?? 30;
        var items = await BuildPortfolioItemsAsync(userId, scope.SelectedProjects, activityDays, ct);

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var statusFilter = Normalize(query.Status)!;
            items = items
                .Where(i => i.Status == statusFilter)
                .ToList();
        }

        var ordered = ApplyProjectPortfolioSorting(items, query.SortBy, query.IsDescending);
        return ToMcpPagedResponse(ordered, query, scope.SearchNote);
    }

    public async Task<McpPagedResponse<McpProjectDeadlineItemResponse>> GetProjectDeadlinesAsync(
        Guid userId,
        McpProjectDeadlinesQuery query,
        CancellationToken ct = default)
    {
        EnsureValidDateRange(query.DueFrom, query.DueTo);

        var scope = await ResolveScopeAsync(userId, query.ProjectId, query.ProjectName, ct);
        var obligations = await LoadObligationsWithPaymentsAsync(scope.SelectedProjects, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var includeOverdue = query.IncludeOverdue ?? true;

        var items = new List<McpProjectDeadlineItemResponse>();
        foreach (var obligation in obligations)
        {
            if (!obligation.OblDueDate.HasValue)
                continue;

            var paid = ComputePaidAmount(obligation);
            var remaining = Math.Max(0m, obligation.OblTotalAmount - paid);
            var status = ComputeObligationStatus(obligation, paid, today);
            if (remaining <= 0)
                continue;

            if (!includeOverdue && obligation.OblDueDate.Value < today)
                continue;

            if (query.DueFrom.HasValue && obligation.OblDueDate.Value < query.DueFrom.Value)
                continue;

            if (query.DueTo.HasValue && obligation.OblDueDate.Value > query.DueTo.Value)
                continue;

            if (!string.IsNullOrWhiteSpace(query.Search)
                && !ContainsText(obligation.OblTitle, query.Search)
                && !ContainsText(obligation.OblDescription, query.Search))
                continue;

            items.Add(new McpProjectDeadlineItemResponse
            {
                ProjectId = obligation.OblProjectId,
                ProjectName = obligation.Project.PrjName,
                ObligationId = obligation.OblId,
                Title = obligation.OblTitle,
                DueDate = obligation.OblDueDate.Value,
                DaysUntilDue = obligation.OblDueDate.Value.DayNumber - today.DayNumber,
                RemainingAmount = remaining,
                Currency = obligation.OblCurrency,
                Status = status
            });
        }

        var ordered = query.IsDescending
            ? items.OrderByDescending(i => i.DueDate).ThenBy(i => i.ProjectName)
            : items.OrderBy(i => i.DueDate).ThenBy(i => i.ProjectName);

        return ToMcpPagedResponse(ordered, query, scope.SearchNote);
    }

    public async Task<McpProjectActivitySplitResponse> GetProjectActivitySplitAsync(
        Guid userId,
        McpProjectActivitySplitQuery query,
        CancellationToken ct = default)
    {
        var scope = await ResolveScopeAsync(userId, query.ProjectId, query.ProjectName, ct);
        var activityDays = query.ActivityDays ?? 30;
        var portfolio = await BuildPortfolioItemsAsync(userId, scope.SelectedProjects, activityDays, ct);

        var response = new McpProjectActivitySplitResponse
        {
            ActiveCount = portfolio.Count(p => p.Status == "active"),
            CompletedCount = portfolio.Count(p => p.Status == "completed"),
            AtRiskCount = portfolio.Count(p => p.Status == "at_risk"),
            InactiveCount = portfolio.Count(p => p.Status == "inactive"),
            SearchNote = scope.SearchNote,
            Items = portfolio
                .OrderBy(p => p.ProjectName)
                .Select(p => new McpProjectActivityItemResponse
                {
                    ProjectId = p.ProjectId,
                    ProjectName = p.ProjectName,
                    Status = p.Status
                })
                .ToList()
        };

        return response;
    }

    /// <summary>Maps a list of projects into portfolio data payloads.</summary>
    private async Task<List<McpProjectPortfolioItemResponse>> BuildPortfolioItemsAsync(
        Guid userId,
        IReadOnlyCollection<Project> selectedProjects,
        int activityDays,
        CancellationToken ct)
    {
        var roleMap = await BuildRoleMapAsync(userId, selectedProjects, ct);
        var expenses = await LoadExpensesAsync(selectedProjects, null, null, ct);
        var incomes = await LoadIncomesAsync(selectedProjects, null, null, ct);
        var obligations = await LoadObligationsWithPaymentsAsync(selectedProjects, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activityThreshold = DateTime.UtcNow.AddDays(-activityDays);

        var obligationByProject = obligations.GroupBy(o => o.OblProjectId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<McpProjectPortfolioItemResponse>();

        foreach (var project in selectedProjects)
        {
            var projectExpenses = expenses.Where(e => e.ExpProjectId == project.PrjId).ToList();
            var projectIncomes = incomes.Where(i => i.IncProjectId == project.PrjId).ToList();
            var projectObligations = obligationByProject.GetValueOrDefault(project.PrjId, []);

            var totalSpent = projectExpenses.Sum(e => e.ExpConvertedAmount);
            var totalIncome = projectIncomes.Sum(i => i.IncConvertedAmount);

            var expenseLastActivity = projectExpenses
                .OrderByDescending(e => e.ExpUpdatedAt)
                .Select(e => (DateTime?)e.ExpUpdatedAt)
                .FirstOrDefault();

            var incomeLastActivity = projectIncomes
                .OrderByDescending(i => i.IncUpdatedAt)
                .Select(i => (DateTime?)i.IncUpdatedAt)
                .FirstOrDefault();

            var lastActivity = MaxDate(expenseLastActivity, incomeLastActivity);

            var budget = await _budgetRepo.GetActiveByProjectIdAsync(project.PrjId, ct);
            var budgetUsedPercentage = budget is not null && budget.PjbTotalBudget > 0
                ? Math.Round(totalSpent / budget.PjbTotalBudget * 100m, 2)
                : (decimal?)null;

            var obligationStatuses = projectObligations
                .Select(o =>
                {
                    var paid = ComputePaidAmount(o);
                    var status = ComputeObligationStatus(o, paid, today);
                    var remaining = Math.Max(0m, o.OblTotalAmount - paid);
                    return new { Obligation = o, Status = status, Remaining = remaining };
                })
                .ToList();

            var nextDeadline = obligationStatuses
                .Where(x => x.Remaining > 0 && x.Obligation.OblDueDate.HasValue)
                .Select(x => x.Obligation.OblDueDate!.Value)
                .OrderBy(d => d)
                .FirstOrDefault();

            var overdueCount = obligationStatuses.Count(x => x.Status == "overdue");
            var openCount = obligationStatuses.Count(x => x.Remaining > 0);

            var progressPercent = 0m;
            if (budgetUsedPercentage.HasValue)
            {
                progressPercent = Math.Min(100m, budgetUsedPercentage.Value);
            }
            else if (projectObligations.Count > 0)
            {
                var totalObligations = projectObligations.Sum(o => o.OblTotalAmount);
                var paidObligations = projectObligations.Sum(o => ComputePaidAmount(o));
                progressPercent = totalObligations > 0
                    ? Math.Min(100m, Math.Round(paidObligations / totalObligations * 100m, 2))
                    : 0m;
            }

            var status = ComputeProjectStatus(
                lastActivity,
                activityThreshold,
                overdueCount,
                openCount,
                budgetUsedPercentage,
                totalSpent,
                totalIncome);

            result.Add(new McpProjectPortfolioItemResponse
            {
                ProjectId = project.PrjId,
                ProjectName = project.PrjName,
                UserRole = roleMap.GetValueOrDefault(project.PrjId, ProjectRoles.Viewer),
                CurrencyCode = project.PrjCurrencyCode,
                Description = project.PrjDescription,
                CreatedAtUtc = project.PrjCreatedAt,
                LastActivityAtUtc = lastActivity,
                NextDeadline = nextDeadline == default ? null : nextDeadline,
                Status = status,
                ProgressPercent = progressPercent,
                TotalSpent = totalSpent,
                TotalIncome = totalIncome,
                NetBalance = totalIncome - totalSpent,
                BudgetUsedPercentage = budgetUsedPercentage,
                OpenObligations = openCount,
                OverdueObligations = overdueCount
            });
        }

        return result;
    }

    /// <summary>Determines the project's health status based on budget constraints.</summary>
    private static string ComputeProjectStatus(
        DateTime? lastActivity,
        DateTime activityThreshold,
        int overdueObligations,
        int openObligations,
        decimal? budgetUsedPercentage,
        decimal totalSpent,
        decimal totalIncome)
    {
        if (overdueObligations > 0 || budgetUsedPercentage is >= 100m)
            return "at_risk";

        if (openObligations == 0 && (totalSpent > 0 || totalIncome > 0))
            return "completed";

        if (!lastActivity.HasValue || lastActivity.Value < activityThreshold)
            return "inactive";

        return "active";
    }

    /// <summary>Applies specific sorting logic to the project portfolio.</summary>
    private static IEnumerable<McpProjectPortfolioItemResponse> ApplyProjectPortfolioSorting(
        IEnumerable<McpProjectPortfolioItemResponse> source,
        string? sortBy,
        bool desc)
    {
        return sortBy?.ToLowerInvariant() switch
        {
            "name" => desc ? source.OrderByDescending(x => x.ProjectName) : source.OrderBy(x => x.ProjectName),
            "status" => desc ? source.OrderByDescending(x => x.Status) : source.OrderBy(x => x.Status),
            "totalspent" => desc ? source.OrderByDescending(x => x.TotalSpent) : source.OrderBy(x => x.TotalSpent),
            "totalincome" => desc ? source.OrderByDescending(x => x.TotalIncome) : source.OrderBy(x => x.TotalIncome),
            "netbalance" => desc ? source.OrderByDescending(x => x.NetBalance) : source.OrderBy(x => x.NetBalance),
            "progress" => desc ? source.OrderByDescending(x => x.ProgressPercent) : source.OrderBy(x => x.ProgressPercent),
            _ => desc
                ? source.OrderByDescending(x => x.LastActivityAtUtc ?? DateTime.MinValue)
                : source.OrderBy(x => x.LastActivityAtUtc ?? DateTime.MinValue)
        };
    }
}
